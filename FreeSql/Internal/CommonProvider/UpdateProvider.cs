﻿using FreeSql.Extensions.EntityUtil;
using FreeSql.Internal.Model;
using FreeSql.Internal.ObjectPool;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FreeSql.Internal.CommonProvider
{
    public abstract partial class UpdateProvider
    {
        public IFreeSql _orm;
        public CommonUtils _commonUtils;
        public CommonExpression _commonExpression;
        public Dictionary<string, bool> _ignore = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
        public Dictionary<string, bool> _auditValueChangedDict = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
        public TableInfo _table;
        public ColumnInfo[] _tempPrimarys;
        public ColumnInfo _versionColumn;
        public bool _ignoreVersion = false;
        public Func<string, string> _tableRule;
        public StringBuilder _where = new StringBuilder();
        public List<GlobalFilter.Item> _whereGlobalFilter;
        public StringBuilder _set = new StringBuilder();
        public StringBuilder _setIncr = new StringBuilder();
        public List<DbParameter> _params = new List<DbParameter>(); //已经固定的
        public List<DbParameter> _paramsSource = new List<DbParameter>(); //每次ToSql重新生成的
        public bool _noneParameter;
        public int _batchRowsLimit, _batchParameterLimit;
        public bool _batchAutoTransaction = true;
        public DbTransaction _transaction;
        public DbConnection _connection;
        public int _commandTimeout = 0;
        public Action<StringBuilder> _interceptSql;
        public string _tableAlias;
        public object _updateVersionValue;
        public bool _isAutoSyncStructure;


        public static int ExecuteBulkUpdate<T1>(UpdateProvider<T1> update, NativeTuple<string, string, string, string, string[]> state, Action<IInsert<T1>> funcBulkCopy) where T1 : class =>
            ExecuteBulkCommand(update._source, update._tempPrimarys, update._orm, update._connection, update._transaction, update._table, state, funcBulkCopy);
        public static int ExecuteBulkUpsert<T1>(InsertOrUpdateProvider<T1> upsert, NativeTuple<string, string, string, string, string[]> state, Action<IInsert<T1>> funcBulkCopy) where T1 : class =>
            ExecuteBulkCommand(upsert._source, upsert._tempPrimarys, upsert._orm, upsert._connection, upsert._transaction, upsert._table, state, funcBulkCopy);

        public static int ExecuteBulkCommand<T1>(List<T1> _source, ColumnInfo[] _tempPrimarys, IFreeSql _orm, DbConnection _connection, DbTransaction _transaction, TableInfo _table,
            NativeTuple<string, string, string, string, string[]> state, Action<IInsert<T1>> funcBulkCopy) where T1 : class
        {
            if (_source.Any() != true || _tempPrimarys.Any() == false) return 0;
            var fsql = _orm;
            var connection = _connection;
            var transaction = _transaction;

            Object<DbConnection> poolConn = null;
            if (connection == null)
            {
                poolConn = fsql.Ado.MasterPool.Get();
                connection = poolConn.Value;
            }
            try
            {
                var droped = false;
                fsql.Ado.CommandFluent(state.Item1).WithConnection(connection).WithTransaction(transaction).ExecuteNonQuery();
                try
                {
                    var insert = fsql.Insert<T1>();
                    (insert as InsertProvider<T1>)._source.AddRange(_source); //不能直接 AppendData，防止触发 Aop.AuditValue
                    insert
                        .AsType(_table.Type)
                        .WithConnection(connection)
                        .WithTransaction(transaction)
                        .InsertIdentity()
                        .InsertColumns(state.Item5)
                        .AsTable(state.Item4);
                    (insert as InsertProvider)._isAutoSyncStructure = false;
                    funcBulkCopy(insert);
                    switch (fsql.Ado.DataType)
                    {
                        case DataType.Oracle:
                        case DataType.OdbcOracle:
                        case DataType.CustomOracle:
                        case DataType.Dameng:
                            return fsql.Ado.CommandFluent(state.Item2).WithConnection(connection).WithTransaction(transaction).ExecuteNonQuery();
                    }
                    var affrows = fsql.Ado.CommandFluent(state.Item2 + ";\r\n" + state.Item3).WithConnection(connection).WithTransaction(transaction).ExecuteNonQuery();
                    droped = true;
                    return affrows;
                }
                finally
                {
                    if (droped == false) fsql.Ado.CommandFluent(state.Item3).WithConnection(connection).WithTransaction(transaction).ExecuteNonQuery();
                }
            }
            finally
            {
                poolConn?.Dispose();
            }
        }
#if net40
#else
        public static Task<int> ExecuteBulkUpdateAsync<T1>(UpdateProvider<T1> update, NativeTuple<string, string, string, string, string[]> state, Func<IInsert<T1>, Task> funcBulkCopy) where T1 : class =>
            ExecuteBulkCommandAsync(update._source, update._tempPrimarys, update._orm, update._connection, update._transaction, update._table, state, funcBulkCopy);

        public static Task<int> ExecuteBulkUpsertAsync<T1>(InsertOrUpdateProvider<T1> upsert, NativeTuple<string, string, string, string, string[]> state, Func<IInsert<T1>, Task> funcBulkCopy) where T1 : class =>
            ExecuteBulkCommandAsync(upsert._source, upsert._tempPrimarys, upsert._orm, upsert._connection, upsert._transaction, upsert._table, state, funcBulkCopy);

        async public static Task<int> ExecuteBulkCommandAsync<T1>(List<T1> _source, ColumnInfo[] _tempPrimarys, IFreeSql _orm, DbConnection _connection, DbTransaction _transaction, TableInfo _table,
            NativeTuple<string, string, string, string, string[]> state, Func<IInsert<T1>, Task> funcBulkCopy) where T1 : class
        {
            if (_source.Any() != true || _tempPrimarys.Any() == false) return 0;
            var fsql = _orm;
            var connection = _connection;
            var transaction = _transaction;

            Object<DbConnection> poolConn = null;
            if (connection == null)
            {
                poolConn = await fsql.Ado.MasterPool.GetAsync();
                connection = poolConn.Value;
            }
            try
            {
                var droped = false;
                await fsql.Ado.CommandFluent(state.Item1).WithConnection(connection).WithTransaction(transaction).ExecuteNonQueryAsync();
                try
                {
                    var insert = fsql.Insert<T1>();
                    (insert as InsertProvider<T1>)._source.AddRange(_source); //不能直接 AppendData，防止触发 Aop.AuditValue
                    insert
                        .AsType(_table.Type)
                        .WithConnection(connection)
                        .WithTransaction(transaction)
                        .InsertIdentity()
                        .InsertColumns(state.Item5)
                        .AsTable(state.Item4);
                    (insert as InsertProvider)._isAutoSyncStructure = false;
                    await funcBulkCopy(insert);
                    switch (fsql.Ado.DataType)
                    {
                        case DataType.Oracle:
                        case DataType.OdbcOracle:
                        case DataType.CustomOracle:
                        case DataType.Dameng:
                            return await fsql.Ado.CommandFluent(state.Item2).WithConnection(connection).WithTransaction(transaction).ExecuteNonQueryAsync();
                    }
                    var affrows = await fsql.Ado.CommandFluent(state.Item2 + ";\r\n" + state.Item3).WithConnection(connection).WithTransaction(transaction).ExecuteNonQueryAsync();
                    droped = true;
                    return affrows;
                }
                finally
                {
                    if (droped == false) await fsql.Ado.CommandFluent(state.Item3).WithConnection(connection).WithTransaction(transaction).ExecuteNonQueryAsync();
                }
            }
            finally
            {
                poolConn?.Dispose();
            }
        }
#endif
    }

    public abstract partial class UpdateProvider<T1> : UpdateProvider, IUpdate<T1>
    {
        public List<T1> _source = new List<T1>();
        public List<T1> _sourceOld;
        public Action<BatchProgressStatus<T1>> _batchProgress;

        public UpdateProvider(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere)
        {
            _orm = orm;
            _commonUtils = commonUtils;
            _commonExpression = commonExpression;
            _table = _commonUtils.GetTableByEntity(typeof(T1));
            _tempPrimarys = _table?.Primarys ?? new ColumnInfo[0];
            _versionColumn = _table?.VersionColumn;
            _noneParameter = _orm.CodeFirst.IsNoneCommandParameter;
            _isAutoSyncStructure = _orm.CodeFirst.IsAutoSyncStructure;
            this.Where(_commonUtils.WhereObject(_table, "", dywhere, _params));
            if (_isAutoSyncStructure && typeof(T1) != typeof(object)) _orm.CodeFirst.SyncStructure<T1>();
            IgnoreCanUpdate();
            _whereGlobalFilter = _orm.GlobalFilter.GetFilters().Where(l => (l.FilterType & GlobalFilter.FilterType.Update) == GlobalFilter.FilterType.Update).ToList();
            _sourceOld = _source;
        }

        /// <summary>
        /// AsType, Ctor, ClearData 三处地方需要重新加载
        /// </summary>
        protected void IgnoreCanUpdate()
        {
            if (_table == null || _table.Type == typeof(object)) return;
            foreach (var col in _table?.Columns.Values)
                if (col.Attribute.CanUpdate == false && _ignore.ContainsKey(col.Attribute.Name) == false)
                    _ignore.Add(col.Attribute.Name, true);
        }
        protected void ClearData()
        {
            _batchRowsLimit = _batchParameterLimit = 0;
            _batchAutoTransaction = true;
            _source.Clear();
            _sourceOld = _source;
            _ignore.Clear();
            _auditValueChangedDict.Clear();
            _where.Clear();
            _set.Clear();
            _setIncr.Clear();
            _params.Clear();
            _paramsSource.Clear();
            IgnoreCanUpdate();
            _whereGlobalFilter = _orm.GlobalFilter.GetFilters().Where(l => (l.FilterType & GlobalFilter.FilterType.Update) == GlobalFilter.FilterType.Update).ToList();
            _batchProgress = null;
            _interceptSql = null;
            _tableAlias = null;
            _versionColumn = _table?.VersionColumn;
            _ignoreVersion = false;
        }

        public IUpdateJoin<T1, T2> Join<T2>(Expression<Func<T1, T2, bool>> on) where T2 : class => Join<T2>(_orm.Select<T2>(), on);
        public IUpdateJoin<T1, T2> Join<T2>(ISelect<T2> query, Expression<Func<T1, T2, bool>> on) where T2 : class
        {
            var ctor = typeof(UpdateJoinProvider<,>).MakeGenericType(typeof(T1), typeof(T2))
                .GetConstructor(new[] { typeof(IUpdate<T1>), typeof(ISelect<T2>), typeof(Expression<Func<T1, T2, bool>>) });
            if (ctor == null) throw new Exception(CoreErrorStrings.Type_Cannot_Access_Constructor("UpdateJoinProvider<>"));
            return ctor.Invoke(new object[] { this, query, on }) as IUpdateJoin<T1, T2>;
        }

        public IUpdate<T1> WithTransaction(DbTransaction transaction)
        {
            _transaction = transaction;
            if (transaction != null) _connection = transaction.Connection;
            return this;
        }
        public IUpdate<T1> WithConnection(DbConnection connection)
        {
            if (_transaction?.Connection != connection) _transaction = null;
            _connection = connection;
            return this;
        }
        public IUpdate<T1> CommandTimeout(int timeout)
        {
            _commandTimeout = timeout;
            return this;
        }

        public IUpdate<T1> NoneParameter(bool isNotCommandParameter = true)
        {
            _noneParameter = isNotCommandParameter;
            return this;
        }

        public virtual IUpdate<T1> BatchOptions(int rowsLimit, int parameterLimit, bool autoTransaction = true)
        {
            _batchRowsLimit = rowsLimit;
            _batchParameterLimit = parameterLimit;
            _batchAutoTransaction = autoTransaction;
            return this;
        }

        public IUpdate<T1> BatchProgress(Action<BatchProgressStatus<T1>> callback)
        {
            _batchProgress = callback;
            return this;
        }

        protected void ValidateVersionAndThrow(int affrows, string sql, DbParameter[] dbParms)
        {
            if (_versionColumn != null && _source.Count > 0)
            {
                if (affrows != _source.Count)
                    throw new DbUpdateVersionException(CoreErrorStrings.DbUpdateVersionException_RowLevelOptimisticLock(_source.Count, affrows), _table, sql, dbParms, affrows, _source.Select(a => (object)a));
                foreach (var d in _source)
                {
                    if (d is Dictionary<string, object> dict)
                    {
                        if (dict.ContainsKey(_versionColumn.CsName))
                        {
                            var val = dict[_versionColumn.CsName];
                            if (val == null) continue;
                            var valType = val.GetType();

                            if (valType == typeof(byte[]))
                                dict[_versionColumn.CsName] = _updateVersionValue;
                            else if (valType == typeof(string))
                                dict[_versionColumn.CsName] = _updateVersionValue;
                            else if (int.TryParse(string.Concat(val), out var tryintver))
                                dict[_versionColumn.CsName] = tryintver + 1;
                        }
                        continue;
                    }
                    if (_versionColumn.Attribute.MapType == typeof(byte[]))
                        _orm.SetEntityValueWithPropertyName(_table.Type, d, _versionColumn.CsName, _updateVersionValue);
                    else if (_versionColumn.Attribute.MapType == typeof(string))
                        _orm.SetEntityValueWithPropertyName(_table.Type, d, _versionColumn.CsName, _updateVersionValue);
                    else
                        _orm.SetEntityIncrByWithPropertyName(_table.Type, d, _versionColumn.CsName, 1);
                }
            }
        }

        #region 参数化数据限制，或values数量限制
        protected internal List<T1>[] SplitSource(int valuesLimit, int parameterLimit, bool isAsTableSplited = false)
        {
            valuesLimit = valuesLimit - 1;
            parameterLimit = parameterLimit - 1;
            if (valuesLimit <= 0) valuesLimit = 1;
            if (parameterLimit <= 0) parameterLimit = 999;
            if (_source == null || _source.Any() == false) return new List<T1>[0];
            if (_source.Count == 1) return new[] { _source };

            if (_table.AsTableImpl != null && isAsTableSplited == false)
            {
                var atarr = _source.Select(a => new
                {
                    item = a,
                    splitKey = _table.AsTableImpl.GetTableNameByColumnValue(_table.AsTableColumn.GetValue(a), true)
                }).GroupBy(a => a.splitKey, a => a.item).ToArray();
                if (atarr.Length > 1)
                {
                    var oldSource = _source;
                    var arrret = new List<List<T1>>();
                    foreach (var item in atarr)
                    {
                        _source = item.ToList();
                        var itemret = SplitSource(valuesLimit + 1, parameterLimit + 1, true);
                        arrret.AddRange(itemret);
                    }
                    _source = oldSource;
                    return arrret.ToArray();
                }
            }

            var takeMax = valuesLimit;
            if (_noneParameter == false)
            {
                var colSum = _table.Columns.Count - _ignore.Count;
                if (colSum <= 0) colSum = 1;
                takeMax = parameterLimit / colSum;
                if (takeMax > valuesLimit) takeMax = valuesLimit;
            }
            if (_source.Count <= takeMax) return new[] { _source };

            var execCount = (int)Math.Ceiling(1.0 * _source.Count / takeMax);
            var ret = new List<T1>[execCount];
            for (var a = 0; a < execCount; a++)
                ret[a] = _source.GetRange(a * takeMax, Math.Min(takeMax, _source.Count - a * takeMax));
            return ret;
        }
        protected virtual void SplitExecute(int valuesLimit, int parameterLimit, string traceName, Action execute)
        {
			var ss = SplitSource(valuesLimit, parameterLimit);
			if (ss.Length <= 1)
			{
				if (_source?.Any() == true) _batchProgress?.Invoke(new BatchProgressStatus<T1>(_source, 1, 1));
                execute();
				ClearData();
				return;
			}
			if (_transaction == null)
			{
				var threadTransaction = _orm.Ado.TransactionCurrentThread;
				if (threadTransaction != null) this.WithTransaction(threadTransaction);
			}

			var before = new Aop.TraceBeforeEventArgs(traceName, null);
			_orm.Aop.TraceBeforeHandler?.Invoke(this, before);
			Exception exception = null;
			try
			{
				if (_transaction != null || _batchAutoTransaction == false)
				{
					for (var a = 0; a < ss.Length; a++)
					{
						_source = ss[a];
						_batchProgress?.Invoke(new BatchProgressStatus<T1>(_source, a + 1, ss.Length));
                        execute();
					}
				}
				else
				{
					if (_orm.Ado.MasterPool == null) throw new Exception(CoreErrorStrings.MasterPool_IsNull_UseTransaction);
					using (var conn = _orm.Ado.MasterPool.Get())
					{
						_transaction = conn.Value.BeginTransaction();
						var transBefore = new Aop.TraceBeforeEventArgs("BeginTransaction", null);
						_orm.Aop.TraceBeforeHandler?.Invoke(this, transBefore);
						try
						{
							for (var a = 0; a < ss.Length; a++)
							{
								_source = ss[a];
								_batchProgress?.Invoke(new BatchProgressStatus<T1>(_source, a + 1, ss.Length));
								execute();
							}
							_transaction.Commit();
							_orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, CoreErrorStrings.Commit, null));
						}
						catch (Exception ex)
						{
							_transaction.Rollback();
							_orm.Aop.TraceAfterHandler?.Invoke(this, new Aop.TraceAfterEventArgs(transBefore, CoreErrorStrings.RollBack, ex));
							throw;
						}
						_transaction = null;
					}
				}
			}
			catch (Exception ex)
			{
				exception = ex;
				throw;
			}
			finally
			{
				var after = new Aop.TraceAfterEventArgs(before, null, exception);
				_orm.Aop.TraceAfterHandler?.Invoke(this, after);
			}
			ClearData();
		}

		protected int SplitExecuteAffrows(int valuesLimit, int parameterLimit)
        {
            var ret = 0;
            SplitExecute(valuesLimit, parameterLimit, "SplitExecuteAffrows", () =>
                ret += this.RawExecuteAffrows()
            );
            return ret;
        }
		protected List<TReturn> SplitExecuteUpdated<TReturn>(int valuesLimit, int parameterLimit, IEnumerable<ColumnInfo> columns)
		{
			var ret = new List<TReturn>();
			SplitExecute(valuesLimit, parameterLimit, "SplitExecuteUpdated", () =>
				ret.AddRange(this.RawExecuteUpdated<TReturn>(columns ?? _table.ColumnsByPosition))
			);
			return ret;
		}
		#endregion

		protected int RawExecuteAffrows()
        {
            var affrows = 0;
            DbParameter[] dbParms = null;
            ToSqlFetch(sb =>
            {
                if (dbParms == null) dbParms = _params.Concat(_paramsSource).ToArray();
                var sql = sb.ToString();
                var before = new Aop.CurdBeforeEventArgs(_table.Type, _table, Aop.CurdType.Update, sql, dbParms);
                _orm.Aop.CurdBeforeHandler?.Invoke(this, before);

                Exception exception = null;
                try
                {
                    var affrowstmp = _orm.Ado.ExecuteNonQuery(_connection, _transaction, CommandType.Text, sql, _commandTimeout, dbParms);
                    ValidateVersionAndThrow(affrowstmp, sql, dbParms);
                    affrows += affrowstmp;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw;
                }
                finally
                {
                    var after = new Aop.CurdAfterEventArgs(before, exception, affrows);
                    _orm.Aop.CurdAfterHandler?.Invoke(this, after);
                }
            });
            return affrows;
        }

        protected abstract List<TReturn> RawExecuteUpdated<TReturn>(IEnumerable<ColumnInfo> columns);

		public abstract int ExecuteAffrows();
		protected abstract List<TReturn> ExecuteUpdated<TReturn>(IEnumerable<ColumnInfo> columns);

        public List<T1> ExecuteUpdated()
        {
            var ret = ExecuteUpdated<T1>(_table.Columns.Values);
            if (_table.TypeLazySetOrm != null) ret.ForEach(item => _table.TypeLazySetOrm.Invoke(item, new object[] { _orm }));
            return ret;
        }
        public List<TReturn> ExecuteUpdated<TReturn>(Expression<Func<T1, TReturn>> returnColumns)
		{
            var cols = _commonExpression.ExpressionSelectColumns_MemberAccess_New_NewArrayInit(null, null, returnColumns?.Body, false, null)
                .Distinct().Select(a => _table.ColumnsByCs.TryGetValue(a, out var c) ? c : null).Where(a => a != null).ToArray();
			var ret = ExecuteUpdated<TReturn>(cols);
            if (_table.TypeLazySetOrm != null) ret.ForEach(item => _table.TypeLazySetOrm.Invoke(item, new object[] { _orm }));
            return ret;
        }

		public IUpdate<T1> IgnoreColumns(Expression<Func<T1, object>> columns) => IgnoreColumns(_commonExpression.ExpressionSelectColumns_MemberAccess_New_NewArrayInit(null, null, columns?.Body, false, null));
        public IUpdate<T1> UpdateColumns(Expression<Func<T1, object>> columns) => UpdateColumns(_commonExpression.ExpressionSelectColumns_MemberAccess_New_NewArrayInit(null, null, columns?.Body, false, null));

        public IUpdate<T1> IgnoreColumns(string[] columns)
        {
            var cols = columns.Distinct().ToDictionary(a => a);
            _ignore.Clear();
            IgnoreCanUpdate();
            foreach (var col in _table.Columns.Values)
                if (!_ignore.ContainsKey(col.Attribute.Name) && (cols.ContainsKey(col.Attribute.Name) == true || cols.ContainsKey(col.CsName) == true))
                    _ignore.Add(col.Attribute.Name, true);
            return this;
        }
        public IUpdate<T1> UpdateColumns(string[] columns)
        {
            var cols = columns.Distinct().ToDictionary(a => a);
            _ignore.Clear();
            foreach (var col in _table.Columns.Values)
                if (cols.ContainsKey(col.Attribute.Name) == false && cols.ContainsKey(col.CsName) == false && _auditValueChangedDict.ContainsKey(col.Attribute.Name) == false)
                    _ignore.Add(col.Attribute.Name, true);
            return this;
        }

        public static void AuditDataValue(object sender, IEnumerable<T1> data, IFreeSql orm, TableInfo table, Dictionary<string, bool> changedDict)
        {
            if (data?.Any() != true) return;
            if (orm.Aop.AuditValueHandler == null) return;
            foreach (var d in data)
            {
                if (d == null) continue;
                foreach (var col in table.Columns.Values)
                {
                    object val = col.GetValue(d);
                    var auditArgs = new Aop.AuditValueEventArgs(Aop.AuditValueType.Update, col, table.Properties.TryGetValue(col.CsName, out var tryprop) ? tryprop : null, val, d);
                    orm.Aop.AuditValueHandler(sender, auditArgs);
                    if (auditArgs.ValueIsChanged)
                    {
                        col.SetValue(d, val = auditArgs.Value);
                        if (changedDict != null && changedDict.ContainsKey(col.Attribute.Name) == false)
                            changedDict.Add(col.Attribute.Name, true);
                    }
                    if (auditArgs.ObjectAuditBreak) break;

                    if (val == null && col.Attribute.MapType == typeof(string) && col.Attribute.IsNullable == false)
                        col.SetValue(d, val = "");
                }
            }
        }
        public static void AuditDataValue(object sender, T1 data, IFreeSql orm, TableInfo table, Dictionary<string, bool> changedDict)
        {
            if (orm.Aop.AuditValueHandler == null) return;
            if (data == null || table == null) return;
            if (typeof(T1) == typeof(object) && new[] { table.Type, table.TypeLazy }.Contains(data.GetType()) == false)
                throw new Exception(CoreErrorStrings.DataType_AsType_Inconsistent(data.GetType().DisplayCsharp(), table.Type.DisplayCsharp()));
            foreach (var col in table.Columns.Values)
            {
                object val = col.GetValue(data);
                var auditArgs = new Aop.AuditValueEventArgs(Aop.AuditValueType.Update, col, table.Properties.TryGetValue(col.CsName, out var tryprop) ? tryprop : null, val, data);
                orm.Aop.AuditValueHandler(sender, auditArgs);
                if (auditArgs.ValueIsChanged)
                {
                    col.SetValue(data, val = auditArgs.Value);
                    if (changedDict != null && changedDict.ContainsKey(col.Attribute.Name) == false)
                        changedDict.Add(col.Attribute.Name, true);
                }
                if (auditArgs.ObjectAuditBreak) break;

                if (val == null && col.Attribute.MapType == typeof(string) && col.Attribute.IsNullable == false)
                    col.SetValue(data, val = "");
            }
        }

        public static void GetDictionaryTableInfo(IEnumerable<T1> source, IFreeSql orm, ref TableInfo table)
        {
            if (table == null && typeof(T1) == typeof(Dictionary<string, object>) && source is IEnumerable<Dictionary<string, object>> dicType)
            {
                var tempDict = new Dictionary<string, object>();
                foreach (var item in dicType)
                    foreach (string key in item.Keys)
                    {
                        if (!tempDict.ContainsKey(key)) tempDict[key] = item[key];
                        else if (!(item[key] is null)) tempDict[key] = item[key];
                    }
                UpdateProvider<Dictionary<string, object>>.GetDictionaryTableInfo(tempDict, orm, ref table);
                return;
            }
            GetDictionaryTableInfo(source.FirstOrDefault(), orm, ref table);
        }
        public static void GetDictionaryTableInfo(T1 source, IFreeSql orm, ref TableInfo table)
        {
            if (table == null && typeof(T1) == typeof(Dictionary<string, object>))
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                var dic = source as Dictionary<string, object>;
                table = new TableInfo();
                table.Type = typeof(Dictionary<string, object>);
                table.CsName = dic.TryGetValue("", out var tryval) ? string.Concat(tryval) : "";
                table.DbName = table.CsName;
                if (orm.CodeFirst.IsSyncStructureToLower) table.DbName = table.DbName.ToLower();
                if (orm.CodeFirst.IsSyncStructureToUpper) table.DbName = table.DbName.ToUpper();

                table.DisableSyncStructure = true;
                table.IsDictionaryType = true;
                var colpos = new List<ColumnInfo>();
                foreach (var kv in dic)
                {
                    var colName = kv.Key;
                    if (string.IsNullOrWhiteSpace(colName)) continue;
                    var colType = kv.Value == null ? typeof(object) : kv.Value.GetType();
                    if (orm.CodeFirst.IsSyncStructureToLower) colName = colName.ToLower();
                    if (orm.CodeFirst.IsSyncStructureToUpper) colName = colName.ToUpper();
                    var col = new ColumnInfo
                    {
                        CsName = kv.Key,
                        Table = table,
                        Attribute = new DataAnnotations.ColumnAttribute
                        {
                            Name = colName,
                            MapType = colType
                        },
                        CsType = colType
                    };
                    table.Columns.Add(colName, col);
                    table.ColumnsByCs.Add(kv.Key, col);
                    colpos.Add(col);
                }
                table.ColumnsByPosition = colpos.ToArray();
                colpos.Clear();
            }
        }

        public IUpdate<T1> SetSource(T1 source) => this.SetSource(new[] { source });
        public IUpdate<T1> SetSource(IEnumerable<T1> source, Expression<Func<T1, object>> tempPrimarys = null, bool ignoreVersion = false)
        {
            if (source == null || source.Any() == false) return this;
            GetDictionaryTableInfo(source, _orm, ref _table);
            AuditDataValue(this, source, _orm, _table, _auditValueChangedDict);
            _source.AddRange(source.Where(a => a != null));

            if (tempPrimarys != null)
            {
                var cols = _commonExpression.ExpressionSelectColumns_MemberAccess_New_NewArrayInit(null, null, tempPrimarys?.Body, false, null).Distinct().ToDictionary(a => a);
                _tempPrimarys = cols.Keys.Select(a => _table.Columns.TryGetValue(a, out var col) ? col : null).ToArray().Where(a => a != null).ToArray();
            }
            _ignoreVersion = ignoreVersion;
            _versionColumn = _ignoreVersion ? null : _table?.VersionColumn;
            return this;
        }
        public IUpdate<T1> SetSourceIgnore(T1 source, Func<object, bool> ignore = null)
        {
            if (ignore == null) ignore = val => val == null;
            var columns = _table.Columns.Values
                .Where(col => ignore(_orm.GetEntityValueWithPropertyName(_table.Type, source, col.CsName)))
                .Select(col => col.Attribute.Name).ToArray();
            IgnoreColumns(columns);
            IgnoreCanUpdate();
            return SetSource(source);
        }

        protected void SetPriv(ColumnInfo col, object value)
        {
            object val = null;
            if (col.Attribute.MapType == col.CsType) val = value;
            else val = Utils.GetDataReaderValue(col.Attribute.MapType, value);
            _set.Append(", ").Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ");

            var colsql = _noneParameter ? _commonUtils.GetNoneParamaterSqlValue(_params, "u", col, col.Attribute.MapType, val) :
                _commonUtils.QuoteWriteParamterAdapter(col.Attribute.MapType, _commonUtils.QuoteParamterName($"p_{_params.Count}"));
            _set.Append(_commonUtils.RewriteColumn(col, colsql));
            if (_noneParameter == false)
                _commonUtils.AppendParamter(_params, null, col, col.Attribute.MapType, val);
        }
        public IUpdate<T1> Set<TMember>(Expression<Func<T1, TMember>> column, TMember value)
        {
            var cols = new List<SelectColumnInfo>();
            _commonExpression.ExpressionSelectColumn_MemberAccess(null, null, cols, SelectTableInfoType.From, column?.Body, true, null);
            if (cols.Count != 1) return this;
            SetPriv(cols.First().Column, value);
            return this;
        }
        public IUpdate<T1> SetIf<TMember>(bool condition, Expression<Func<T1, TMember>> column, TMember value) => condition ? Set(column, value) : this;
        public IUpdate<T1> Set<TMember>(Expression<Func<T1, TMember>> exp)
        {
            var body = exp?.Body;
            var nodeType = body?.NodeType;
            if (nodeType == ExpressionType.Convert)
            {
                body = (body as UnaryExpression)?.Operand;
                nodeType = body?.NodeType;
            }
            switch (nodeType)
            {
                case ExpressionType.Equal:
                    var equalBinaryExp = body as BinaryExpression;
                    var eqval = _commonExpression.ExpressionWhereLambdaNoneForeignObject(null, null, _table, null, body, null, null);
                    if (eqval.EndsWith("  IS  NULL")) eqval = $"{eqval.Remove(eqval.Length - 10)} = NULL"; //#311
                    _set.Append(", ").Append(eqval);
                    return this;
                case ExpressionType.MemberInit:
                    var initExp = body as MemberInitExpression;
                    if (initExp.Bindings?.Count > 0)
                    {
                        for (var a = 0; a < initExp.Bindings.Count; a++)
                        {
                            var initAssignExp = (initExp.Bindings[a] as MemberAssignment);
                            if (initAssignExp == null) continue;
                            var memberName = initExp.Bindings[a].Member.Name;
                            if (_table.ColumnsByCsIgnore.ContainsKey(memberName)) continue;
                            if (_table.ColumnsByCs.TryGetValue(memberName, out var col) == false) throw new Exception(CoreErrorStrings.NotFound_Property(memberName));
                            var memberValue = _commonExpression.ExpressionLambdaToSql(initAssignExp.Expression, new CommonExpression.ExpTSC
                            {
                                isQuoteName = true,
                                mapType = initAssignExp.Expression is BinaryExpression ? null : col.Attribute.MapType
                            });
                            _setIncr.Append(", ").Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ").Append(memberValue);
                        }
                    }
                    return this;
                case ExpressionType.New:
                    var newExp = body as NewExpression;
                    if (newExp.Members?.Count > 0)
                    {
                        for (var a = 0; a < newExp.Members.Count; a++)
                        {
                            var memberName = newExp.Members[a].Name;
                            if (_table.ColumnsByCsIgnore.ContainsKey(memberName)) continue;
                            if (_table.ColumnsByCs.TryGetValue(memberName, out var col) == false) throw new Exception(CoreErrorStrings.NotFound_Property(memberName));
                            var memberValue = _commonExpression.ExpressionLambdaToSql(newExp.Arguments[a], new CommonExpression.ExpTSC
                            {
                                isQuoteName = true,
                                mapType = newExp.Arguments[a] is BinaryExpression ? null : col.Attribute.MapType
                            });
                            _setIncr.Append(", ").Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ").Append(memberValue);
                        }
                    }
                    return this;
            }
            if (body is BinaryExpression == false &&
                nodeType != ExpressionType.Call) return this;
            var cols = new List<SelectColumnInfo>();
            var expt = _commonExpression.ExpressionWhereLambdaNoneForeignObject(null, null, _table, cols, body, null, null);
            if (cols.Any() == false) return this;
            foreach (var col in cols)
            {
                if (col.Column.Attribute.IsNullable == true && col.Column.Attribute.MapType.IsNullableType())
                {
                    var replval = _orm.CodeFirst.GetDbInfo(col.Column.Attribute.MapType.GetGenericArguments().FirstOrDefault())?.defaultValue;
                    if (replval == null) continue;
                    var replname = _commonUtils.QuoteSqlName(col.Column.Attribute.Name);
                    expt = expt.Replace(replname, _commonUtils.IsNull(replname, _commonUtils.FormatSql("{0}", replval)));
                } 
                else if (col.Column.CsType == typeof(string))
                {
                    var replname = _commonUtils.QuoteSqlName(col.Column.Attribute.Name);
                    expt = expt.Replace(replname, _commonUtils.IsNull(replname, _commonUtils.FormatSql("{0}", "")));
                }
            }
            _setIncr.Append(", ").Append(_commonUtils.QuoteSqlName(cols.First().Column.Attribute.Name)).Append(" = ").Append(expt);
            return this;
        }
        public IUpdate<T1> SetIf<TMember>(bool condition, Expression<Func<T1, TMember>> exp) => condition ? Set(exp) : this;
        public IUpdate<T1> SetRaw(string sql, object parms = null)
        {
            if (string.IsNullOrEmpty(sql)) return this;
            _set.Append(", ").Append(sql);
            if (parms != null) _params.AddRange(_commonUtils.GetDbParamtersByObject(sql, parms));
            return this;
        }

        public IUpdate<T1> SetDto(object dto) => SetDtoIgnore(dto, val => false);
        public IUpdate<T1> SetDtoIgnore(object dto, Func<object, bool> ignore = null)
		{
            if (dto == null) return this;
            if (ignore == null) ignore = val => val == null;
            if (dto is Dictionary<string, object>)
            {
                var dic = dto as Dictionary<string, object>;
                foreach (var kv in dic)
                {
                    if (ignore(kv.Value)) continue;
                    if (_table.ColumnsByCs.TryGetValue(kv.Key, out var trycol) == false) continue;
                    if (_ignore.ContainsKey(trycol.Attribute.Name)) continue;
                    SetPriv(trycol, kv.Value);
                }
                return this;
            }
            var dtoProps = dto.GetType().GetProperties();
            foreach (var dtoProp in dtoProps)
            {
                var val = dtoProp.GetValue(dto, null);
				if (ignore(val)) continue;
				if (_table.ColumnsByCs.TryGetValue(dtoProp.Name, out var trycol) == false) continue;
                if (_ignore.ContainsKey(trycol.Attribute.Name)) continue;
                SetPriv(trycol, val);
            }
            return this;
        }

        public IUpdate<T1> Where(Expression<Func<T1, bool>> exp) => WhereIf(true, exp);
        public IUpdate<T1> WhereIf(bool condition, Expression<Func<T1, bool>> exp)
        {
            if (condition == false || exp == null) return this;
            return this.Where(_commonExpression.ExpressionWhereLambdaNoneForeignObject(null, null, _table, null, exp?.Body, null, _params));
        }
        public IUpdate<T1> Where(string sql, object parms = null)
        {
            if (string.IsNullOrEmpty(sql)) return this;
            _where.Append(" AND (").Append(sql).Append(')');
            if (parms != null) _params.AddRange(_commonUtils.GetDbParamtersByObject(sql, parms));
            return this;
        }
        public IUpdate<T1> Where(T1 item) => this.Where(new[] { item });
        public IUpdate<T1> Where(IEnumerable<T1> items) => this.Where(_commonUtils.WhereItems(_table.Primarys, "", items, _params));
        public IUpdate<T1> WhereDynamic(object dywhere, bool not = false) => not == false ?
            this.Where(_commonUtils.WhereObject(_table, "", dywhere, _params)) :
            this.Where($"not({_commonUtils.WhereObject(_table, "", dywhere, _params)})");
		public IUpdate<T1> WhereDynamicFilter(DynamicFilterInfo filter)
		{
			var alias = "t_" + Guid.NewGuid().ToString("n").Substring(0, 8);
			var tempQuery = _orm.Select<object>().AsType(_table.Type).DisableGlobalFilter().As(alias);
			tempQuery.WhereDynamicFilter(filter);
			var where = (tempQuery as Select0Provider)._where.ToString().Replace(alias + ".", "");
			_where.Append(where);
			return this;
		}

		public IUpdate<T1> DisableGlobalFilter(params string[] name)
        {
            if (_whereGlobalFilter.Any() == false) return this;
            if (name?.Any() != true)
            {
                _whereGlobalFilter.Clear();
                return this;
            }
            foreach (var n in name)
            {
                if (n == null) continue;
                var idx = _whereGlobalFilter.FindIndex(a => string.Compare(a.Name, n, true) == 0);
                if (idx == -1) continue;
                _whereGlobalFilter.RemoveAt(idx);
            }
            return this;
        }

        protected string WhereCaseSource(string CsName, Func<string, string> thenValue)
        {
            if (_source.Any() == false) return null;
            if (_table.ColumnsByCs.ContainsKey(CsName) == false) throw new Exception(CoreErrorStrings.NotFound_CsName_Column(CsName));
            if (thenValue == null) throw new ArgumentNullException(nameof(thenValue));

            if (_source.Count == 0) return null;
            if (_source.Count == 1)
            {

                var col = _table.ColumnsByCs[CsName];
                var sb = new StringBuilder();

                sb.Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ");
                sb.Append(thenValue(_commonUtils.RewriteColumn(col, _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, col.GetDbValue(_source.First())))));

                return sb.ToString();

            }
            else
            {
                var caseWhen = new StringBuilder();
                caseWhen.Append("CASE ");
                ToSqlCase(caseWhen, _tempPrimarys);
                var cw = caseWhen.ToString();

                var col = _table.ColumnsByCs[CsName];
                var sb = new StringBuilder();
                sb.Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ");

                var valsameIf = col.Attribute.MapType.IsNumberType() ||
                    new[] { typeof(string), typeof(DateTime), typeof(DateTime?) }.Contains(col.Attribute.MapType) ||
                    col.Attribute.MapType.NullableTypeOrThis().IsEnum;
                var ds = _source.Select(a => col.GetDbValue(a)).ToArray();
                if (valsameIf == false && ds[0] == null) valsameIf = true;
                if (valsameIf && ds.All(a => object.Equals(a, ds[0])))
                {
                    var val = ds.First();
                    var colsql = thenValue(_commonUtils.RewriteColumn(col, _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, val)));
                    sb.Append(colsql);
                }
                else
                {
                    var cwsb = new StringBuilder().Append(cw);
                    foreach (var d in _source)
                    {
                        cwsb.Append(" \r\nWHEN ");
                        ToSqlWhen(cwsb, _tempPrimarys, d);
                        cwsb.Append(" THEN ");
                        var val = col.GetDbValue(d);
                        var colsql = thenValue(_commonUtils.RewriteColumn(col, _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, val)));
                        cwsb.Append(colsql);
                    }
                    cwsb.Append(" END");
                    sb.Append(cwsb);
                    cwsb.Clear();
                }
                return sb.ToString();
            }
        }

        protected abstract void ToSqlCase(StringBuilder caseWhen, ColumnInfo[] primarys);
        protected abstract void ToSqlWhen(StringBuilder sb, ColumnInfo[] primarys, object d);
        protected virtual void ToSqlCaseWhenEnd(StringBuilder sb, ColumnInfo col) { }

        protected string TableRuleInvoke()
        {
            if (_tableRule == null && _table.AsTableImpl == null) return _commonUtils.GetEntityTableAopName(_table, true);
            var tbname = _table?.DbName ?? "";
            string newname = null;
            if (_table.AsTableImpl != null)
            {
                if (_source.Any())
                    newname = _table.AsTableImpl.GetTableNameByColumnValue(_table.AsTableColumn.GetValue(_source.FirstOrDefault()));
                else if (_tableRule == null)
                    newname = _table.AsTableImpl.GetTableNameByColumnValue(DateTime.Now);
                else
                    newname = _tableRule(tbname);
            }
            else
                newname = _tableRule(tbname);
            if (newname == tbname) return tbname;
            if (string.IsNullOrEmpty(newname)) return tbname;
            if (_orm.CodeFirst.IsSyncStructureToLower) newname = newname.ToLower();
            if (_orm.CodeFirst.IsSyncStructureToUpper) newname = newname.ToUpper();
            if (_isAutoSyncStructure) _orm.CodeFirst.SyncStructure(_table.Type, newname);
            return newname;
        }
        public IUpdate<T1> AsTable(Func<string, string> tableRule)
        {
            _tableRule = tableRule;
            return this;
        }
        public IUpdate<T1> AsTable(string tableName)
        {
            _tableRule = (oldname) => tableName;
            return this;
        }
        public IUpdate<T1> AsType(Type entityType)
        {
            if (entityType == typeof(object)) throw new Exception(CoreErrorStrings.TypeAsType_NotSupport_Object("IUpdate"));
            if (entityType == _table.Type) return this;
            var newtb = _commonUtils.GetTableByEntity(entityType);
            _table = newtb ?? throw new Exception(CoreErrorStrings.Type_AsType_Parameter_Error("IUpdate"));
            _tempPrimarys = _table.Primarys;
            _versionColumn = _ignoreVersion ? null : _table.VersionColumn;
            if (_isAutoSyncStructure) _orm.CodeFirst.SyncStructure(entityType);
            IgnoreCanUpdate();
            return this;
        }

        public virtual string ToSql()
        {
            if (_source.Any())
            {
                var sb1 = new StringBuilder();
                ToSqlExtension110(sb1, false);
                return sb1.ToString();
            }

            if (_where.Length == 0) return null;

            var sb2 = new StringBuilder();
            ToSqlFetch(sql =>
            {
                sb2.Append(sql).Append("\r\n\r\n;\r\n\r\n");
            });
            if (sb2.Length > 0) sb2.Remove(sb2.Length - 9, 9);
            if (sb2.Length == 0) return null;
            return sb2.ToString();
        }

        public void ToSqlFetch(Action<StringBuilder> fetch)
        {
            if (_source.Any())
            {
                var sb1 = new StringBuilder();
                ToSqlExtension110(sb1, false);
                if (sb1.Length > 0) fetch(sb1);
                return;
            }
            if (_where.Length == 0) return;
			if (_set.Length == 0 && _setIncr.Length == 0) return;
			var newwhere = new StringBuilder();
            ToSqlWhere(newwhere);

            var sb = new StringBuilder();
            if (_table.AsTableImpl != null && string.IsNullOrWhiteSpace(_tableRule?.Invoke(_table.DbName)) == true)
            {
                var oldTableRule = _tableRule;
                var names = _table.AsTableImpl.GetTableNamesBySqlWhere(newwhere.ToString(), _params, new SelectTableInfo { Table = _table }, _commonUtils).Names;
                foreach (var name in names)
                {
                    _tableRule = old => name;
                    ToSqlExtension110(sb.Clear(), true);
                    fetch(sb);
                }
                _tableRule = oldTableRule;
                return;
            }

            ToSqlExtension110(sb, true);
            fetch(sb);
        }
#if net40
#else
        async public Task ToSqlFetchAsync(Func<StringBuilder, Task> fetchAsync)
        {
            if (_source.Any())
            {
                var sb1 = new StringBuilder();
                ToSqlExtension110(sb1, false);
				if (sb1.Length > 0) await fetchAsync(sb1);
                sb1.Clear();
                return;
            }
            if (_where.Length == 0) return;
			if (_set.Length == 0 && _setIncr.Length == 0) return;
            var newwhere = new StringBuilder();
            ToSqlWhere(newwhere);

            var sb = new StringBuilder();
            if (_table.AsTableImpl != null && string.IsNullOrWhiteSpace(_tableRule?.Invoke(_table.DbName)) == true)
            {
                var oldTableRule = _tableRule;
                var names = _table.AsTableImpl.GetTableNamesBySqlWhere(newwhere.ToString(), _params, new SelectTableInfo { Table = _table }, _commonUtils).Names;
                foreach (var name in names)
                {
                    _tableRule = old => name;
                    ToSqlExtension110(sb.Clear(), true);
                    await fetchAsync(sb);
                }
                _tableRule = oldTableRule;
                return;
            }

            ToSqlExtension110(sb, true);
            await fetchAsync(sb);
            sb.Clear();
        }
#endif
		public virtual void ToSqlExtension110(StringBuilder sb, bool isAsTableSplited)
        {
            if (_where.Length == 0 && _source.Any() == false) return;
            if (_source.Any() == false && _set.Length == 0 && _setIncr.Length == 0) return;

            if (_table.AsTableImpl != null && isAsTableSplited == false && _source == _sourceOld && _source.Any())
            {
                var atarr = _source.Select(a => new
                {
                    item = a,
                    splitKey = _table.AsTableImpl.GetTableNameByColumnValue(_table.AsTableColumn.GetValue(a))
                }).GroupBy(a => a.splitKey, a => a.item).ToArray();
                if (atarr.Length > 1)
                {
                    var oldSource = _source;
                    var arrret = new List<List<T1>>();
                    foreach (var item in atarr)
                    {
                        _source = item.ToList();
                        ToSqlExtension110(sb, true);
                        sb.Append("\r\n\r\n;\r\n\r\n");
                    }
                    _source = oldSource;
                    if (sb.Length > 0) sb.Remove(sb.Length - 9, 9);
                    return;
                }
            }

            sb.Append("UPDATE ").Append(_commonUtils.QuoteSqlName(TableRuleInvoke())).Append(" SET ");

            if (_set.Length > 0)
            { //指定 set 更新
                sb.Append(_set.ToString().Substring(2));

            }
            else if (_source.Count == 1)
            { //保存 Source
                _paramsSource.Clear();
                var colidx = 0;
                foreach (var col in _table.Columns.Values)
                {
                    if (col.Attribute.IsPrimary) continue;
                    if (_tempPrimarys.Any(a => a.CsName == col.CsName)) continue;
                    if (col.Attribute.IsIdentity == false && col.Attribute.IsVersion == false && _ignore.ContainsKey(col.Attribute.Name) == false)
                    {
                        if (colidx > 0) sb.Append(", ");
                        sb.Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ");

                        if (col.Attribute.CanUpdate && string.IsNullOrEmpty(col.DbUpdateValue) == false)
                            sb.Append(col.DbUpdateValue);
                        else
                        {
                            var val = col.GetDbValue(_source.First());

                            var colsql = _noneParameter ? _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, val) :
                                _commonUtils.QuoteWriteParamterAdapter(col.Attribute.MapType, _commonUtils.QuoteParamterName($"p_{_paramsSource.Count}"));
                            sb.Append(_commonUtils.RewriteColumn(col, colsql));
                            if (_noneParameter == false)
                                _commonUtils.AppendParamter(_paramsSource, null, col, col.Attribute.MapType, val);
                        }
                        ++colidx;
                    }
                }
                if (colidx == 0)
                {
                    sb.Clear();
                    return;
                }

            }
            else if (_source.Count > 1)
            { //批量保存 Source
                if (_tempPrimarys.Any() == false) return;

                var caseWhen = new StringBuilder();
                caseWhen.Append("CASE ");
                ToSqlCase(caseWhen, _tempPrimarys);
                var cw = caseWhen.ToString();

                _paramsSource.Clear();
                var colidx = 0;
                foreach (var col in _table.Columns.Values)
                {
                    if (col.Attribute.IsPrimary) continue;
                    if (_tempPrimarys.Any(a => a.CsName == col.CsName)) continue;
                    if (col.Attribute.IsIdentity == false && col.Attribute.IsVersion == false && _ignore.ContainsKey(col.Attribute.Name) == false)
                    {
                        if (colidx > 0) sb.Append(", ");
                        sb.Append(_commonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ");

                        if (col.Attribute.CanUpdate && string.IsNullOrEmpty(col.DbUpdateValue) == false)
                            sb.Append(col.DbUpdateValue);
                        else
                        {
                            var valsameIf = col.Attribute.MapType.IsNumberType() || 
                                new[] { typeof(string), typeof(DateTime), typeof(DateTime?) }.Contains(col.Attribute.MapType) ||
                                col.Attribute.MapType.NullableTypeOrThis().IsEnum;
                            var ds = _source.Select(a => col.GetDbValue(a)).ToArray();
							if (valsameIf == false && ds[0] == null) valsameIf = true;
							if (valsameIf && ds.All(a => object.Equals(a, ds[0])))
                            {
                                var val = ds.First();
                                var colsql = _noneParameter ? _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, val) :
                                    _commonUtils.QuoteWriteParamterAdapter(col.Attribute.MapType, _commonUtils.QuoteParamterName($"p_{_paramsSource.Count}"));
                                sb.Append(_commonUtils.RewriteColumn(col, colsql));
                                if (_noneParameter == false)
                                    _commonUtils.AppendParamter(_paramsSource, null, col, col.Attribute.MapType, val);
                            }
                            else
                            {
                                var cwsb = new StringBuilder().Append(cw);
                                foreach (var d in _source)
                                {
                                    cwsb.Append(" \r\nWHEN ");
                                    ToSqlWhen(cwsb, _tempPrimarys, d);
                                    cwsb.Append(" THEN ");
                                    var val = col.GetDbValue(d);

                                    var colsql = _noneParameter ? _commonUtils.GetNoneParamaterSqlValue(_paramsSource, "u", col, col.Attribute.MapType, val) :
                                        _commonUtils.QuoteWriteParamterAdapter(col.Attribute.MapType, _commonUtils.QuoteParamterName($"p_{_paramsSource.Count}"));
                                    colsql = _commonUtils.RewriteColumn(col, colsql);
                                    cwsb.Append(colsql);
                                    if (_noneParameter == false)
                                        _commonUtils.AppendParamter(_paramsSource, null, col, col.Attribute.MapType, val);
                                }
                                cwsb.Append(" END");
                                ToSqlCaseWhenEnd(cwsb, col);
                                sb.Append(cwsb);
                                cwsb.Clear();
                            }
                        }
                        ++colidx;
                    }
                }
                if (colidx == 0) return;
            }
            else if (_setIncr.Length == 0)
                return;

            if (_setIncr.Length > 0)
                sb.Append(_set.Length > 0 || _source.Any() ? _setIncr.ToString() : _setIncr.ToString().Substring(2));

            if (_source.Any() == false)
            {
                var sbString = "";
                foreach (var col in _table.Columns.Values)
                    if (col.Attribute.CanUpdate && string.IsNullOrEmpty(col.DbUpdateValue) == false)
                    {
                        if (sbString == "") sbString = sb.ToString();
                        var loc3 = _commonUtils.QuoteSqlName(col.Attribute.Name);
                        if (sbString.Contains(loc3)) continue;
                        sb.Append(", ").Append(loc3).Append(" = ").Append(col.DbUpdateValue);
                    }
            }

            if (_versionColumn != null && _versionColumn.Attribute.CanUpdate)
            {
                var vcname = _commonUtils.QuoteSqlName(_versionColumn.Attribute.Name);
                var vcvalue = vcname;
                if (string.IsNullOrWhiteSpace(_tableAlias) == false)
                {
                    switch (_orm.Ado.DataType)
                    {
                        case DataType.PostgreSQL:
                        case DataType.OdbcPostgreSQL:
                        case DataType.CustomPostgreSQL:
                        case DataType.KingbaseES:
                        case DataType.ShenTong:
                        case DataType.Xugu:
                            vcvalue = $"{_tableAlias}.{vcname}";  //set name = b.name
                            break;
                        default:
                            vcname = vcvalue = $"{_tableAlias}.{vcname}";  //set a.name = b.name
                            break;
                    }
                }
                if (_versionColumn.Attribute.MapType == typeof(byte[]))
                {
                    _updateVersionValue = Utils.GuidToBytes(Guid.NewGuid());
                    sb.Append(", ").Append(vcname).Append(" = ").Append(_commonUtils.GetNoneParamaterSqlValue(_paramsSource, "uv", _versionColumn, _versionColumn.Attribute.MapType, _updateVersionValue));
                }
                else if (_versionColumn.Attribute.MapType == typeof(string))
                {
                    _updateVersionValue = Guid.NewGuid().ToString();
                    sb.Append(", ").Append(vcname).Append(" = ").Append(_commonUtils.GetNoneParamaterSqlValue(_paramsSource, "uv", _versionColumn, _versionColumn.Attribute.MapType, _updateVersionValue));
                }
                else
                    sb.Append(", ").Append(vcname).Append(" = ").Append(_commonUtils.IsNull(vcvalue, 0)).Append(" + 1");
            }
            ToSqlWhere(sb);
            _interceptSql?.Invoke(sb);
            return;
        }

        public virtual void ToSqlWhere(StringBuilder sb)
        {
            var andTimes = 0;
            sb.Append(" \r\nWHERE ");
            if (_source.Any())
            {
                if (_tempPrimarys.Any() == false) throw new ArgumentException(CoreErrorStrings.NoPrimaryKey_UseSetDto(_table.Type.DisplayCsharp()));
                sb.Append('(').Append(_commonUtils.WhereItems(_tempPrimarys, "", _source, _paramsSource)).Append(')');
                andTimes++;
            }

            if (_whereGlobalFilter.Any())
            {
                var globalFilterCondi = _commonExpression.GetWhereCascadeSql(new SelectTableInfo { Table = _table, Alias = _tableAlias }, _whereGlobalFilter.Where(a => a.Before == true), false);
                if (string.IsNullOrEmpty(globalFilterCondi) == false)
                {
                    if (andTimes > 0) sb.Append(" AND ");
                    sb.Append(globalFilterCondi);
                    andTimes++;
                }
            }

            if (_where.Length > 0)
            {
                sb.Append(andTimes > 0 ? _where.ToString() : _where.ToString().Substring(5));
                andTimes++;
            }

            if (_whereGlobalFilter.Any())
            {
                var globalFilterCondi = _commonExpression.GetWhereCascadeSql(new SelectTableInfo { Table = _table, Alias = _tableAlias }, _whereGlobalFilter.Where(a => a.Before == false), false);
                if (string.IsNullOrEmpty(globalFilterCondi) == false)
                {
                    if (andTimes > 0) sb.Append(" AND ");
                    sb.Append(globalFilterCondi);
                    andTimes++;
                }
            }

            if (_versionColumn != null)
            {
                var versionCondi = WhereCaseSource(_versionColumn.CsName, sqlval => sqlval);
                if (string.IsNullOrEmpty(versionCondi) == false)
                    sb.Append(" AND ").Append(versionCondi);
            }
        }
    }
}
