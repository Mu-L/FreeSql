﻿using FreeSql.Aop;
using FreeSql.Internal.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FreeSql.Odbc.PostgreSQL
{
    public class OdbcPostgreSQLOnConflictDoUpdate<T1> where T1 : class
    {
        internal OdbcPostgreSQLInsert<T1> _pgsqlInsert;
        internal OdbcPostgreSQLUpdate<T1> _pgsqlUpdatePriv;
        internal OdbcPostgreSQLUpdate<T1> _pgsqlUpdate => _pgsqlUpdatePriv ?? 
            (_pgsqlUpdatePriv = new OdbcPostgreSQLUpdate<T1>(_pgsqlInsert.InternalOrm, _pgsqlInsert.InternalCommonUtils, _pgsqlInsert.InternalCommonExpression, null) { InternalTableAlias = "EXCLUDED" }
                .NoneParameter().SetSource(_pgsqlInsert.InternalSource) as OdbcPostgreSQLUpdate<T1>);
        internal ColumnInfo[] _tempPrimarys;
        bool _doNothing;

        public OdbcPostgreSQLOnConflictDoUpdate(IInsert<T1> insert, Expression<Func<T1, object>> columns = null)
        {
            _pgsqlInsert = insert as OdbcPostgreSQLInsert<T1>;
            if (_pgsqlInsert == null) throw new Exception(CoreErrorStrings.S_Features_Unique("OnConflictDoUpdate", "Odbc/PostgreSQL"));
            if (_pgsqlInsert._noneParameterFlag == "c") _pgsqlInsert._noneParameterFlag = "cu";

            if (columns != null)
            {
                var colsList = new List<ColumnInfo>();
                var cols = _pgsqlInsert.InternalCommonExpression.ExpressionSelectColumns_MemberAccess_New_NewArrayInit(null, null, columns?.Body, false, null).ToDictionary(a => a, a => true);
                foreach (var col in _pgsqlInsert.InternalTable.Columns.Values)
                    if (cols.ContainsKey(col.Attribute.Name))
                        colsList.Add(col);
                _tempPrimarys = colsList.ToArray();
            }
            if (_tempPrimarys == null || _tempPrimarys.Any() == false)
                _tempPrimarys = _pgsqlInsert.InternalTable.Primarys;
            if (_tempPrimarys.Any() == false) throw new Exception(CoreErrorStrings.S_OnConflictDoUpdate_MustIsPrimary);
        }

        protected void ClearData()
        {
            _pgsqlInsert.InternalClearData();
            _pgsqlUpdatePriv = null;
        }

        public OdbcPostgreSQLOnConflictDoUpdate<T1> IgnoreColumns(Expression<Func<T1, object>> columns)
        {
            _pgsqlUpdate.IgnoreColumns(columns);
            return this;
        }
        public OdbcPostgreSQLOnConflictDoUpdate<T1> UpdateColumns(Expression<Func<T1, object>> columns)
        {
            _pgsqlUpdate.UpdateColumns(columns);
            return this;
        }
        public OdbcPostgreSQLOnConflictDoUpdate<T1> IgnoreColumns(string[] columns)
        {
            _pgsqlUpdate.IgnoreColumns(columns);
            return this;
        }
        public OdbcPostgreSQLOnConflictDoUpdate<T1> UpdateColumns(string[] columns)
        {
            _pgsqlUpdate.UpdateColumns(columns);
            return this;
        }

        public OdbcPostgreSQLOnConflictDoUpdate<T1> Set<TMember>(Expression<Func<T1, TMember>> column, TMember value)
        {
            _pgsqlUpdate.Set(column, value);
            return this;
        }
        //由于表达式解析问题，ON CONFLICT("id") DO UPDATE SET 需要指定表别名，如 Set(a => a.Clicks + 1) 解析会失败
        //暂时不开放这个功能，如有需要使用 SetRaw("click = t.click + 1") 替代该操作
        //public OnConflictDoUpdate<T1> Set<TMember>(Expression<Func<T1, TMember>> exp)
        //{
        //    _pgsqlUpdate.Set(exp);
        //    return this;
        //}
        public OdbcPostgreSQLOnConflictDoUpdate<T1> SetRaw(string sql)
        {
            _pgsqlUpdate.SetRaw(sql);
            return this;
        }

        public OdbcPostgreSQLOnConflictDoUpdate<T1> DoNothing()
        {
            _doNothing = true;
            return this;
        }

        public string ToSql()
        {
            var sb = new StringBuilder();
            sb.Append(_pgsqlInsert.ToSql()).Append("\r\nON CONFLICT(");
            for (var a = 0; a < _tempPrimarys.Length; a++)
            {
                if (a > 0) sb.Append(", ");
                sb.Append(_pgsqlInsert.InternalCommonUtils.QuoteSqlName(_tempPrimarys[a].Attribute.Name));
            }
            if (_doNothing)
            {
                sb.Append(") DO NOTHING");
            }
            else
            {
                sb.Append(") DO UPDATE SET\r\n");

                if (_pgsqlUpdate._tempPrimarys.Any() == false) _pgsqlUpdate._tempPrimarys = _tempPrimarys;
                var sbSetEmpty = _pgsqlUpdate.InternalSbSet.Length == 0;
                var sbSetIncrEmpty = _pgsqlUpdate.InternalSbSetIncr.Length == 0;
                if (sbSetEmpty == false || sbSetIncrEmpty == false)
                {
                    if (sbSetEmpty == false) sb.Append(_pgsqlUpdate.InternalSbSet.ToString().Substring(2));
                    if (sbSetIncrEmpty == false) sb.Append(sbSetEmpty ? _pgsqlUpdate.InternalSbSetIncr.ToString().Substring(2) : _pgsqlUpdate.InternalSbSetIncr.ToString());
                }
                else
                {
                    var colidx = 0;
                    foreach (var col in _pgsqlInsert.InternalTable.Columns.Values)
                    {
                        if (col.Attribute.IsPrimary || _pgsqlUpdate.InternalIgnore.ContainsKey(col.Attribute.Name)) continue;

                        if (colidx > 0) sb.Append(", \r\n");

                        if (col.Attribute.IsVersion == true && col.Attribute.MapType != typeof(byte[]))
                        {
                            var field = _pgsqlInsert.InternalCommonUtils.QuoteSqlName(col.Attribute.Name);
                            sb.Append(field).Append(" = ").Append(_pgsqlInsert.InternalCommonUtils.QuoteSqlName(_pgsqlInsert.InternalTable.DbName)).Append(".").Append(field).Append(" + 1");
                        }
                        else if (_pgsqlInsert.InternalIgnore.ContainsKey(col.Attribute.Name))
                        {
                            if (string.IsNullOrEmpty(col.DbUpdateValue) == false)
                            {
                                sb.Append(_pgsqlInsert.InternalCommonUtils.QuoteSqlName(col.Attribute.Name)).Append(" = ").Append(col.DbUpdateValue);
                            }
                            else
                            {
                                var caseWhen = _pgsqlUpdate.InternalWhereCaseSource(col.CsName, sqlval => sqlval).Trim();
                                sb.Append(caseWhen);
                                if (caseWhen.EndsWith(" END")) _pgsqlUpdate.InternalToSqlCaseWhenEnd(sb, col);
                            }
                        }
                        else
                        {
                            var field = _pgsqlInsert.InternalCommonUtils.QuoteSqlName(col.Attribute.Name);
                            sb.Append(field).Append(" = EXCLUDED.").Append(field);
                        }
                        ++colidx;
                    }
                }
            }

            return sb.ToString();
        }

        public long ExecuteAffrows()
        {
            var sql = this.ToSql();
            if (string.IsNullOrEmpty(sql)) return 0;

            var before = new CurdBeforeEventArgs(_pgsqlInsert.InternalTable.Type, _pgsqlInsert.InternalTable, CurdType.Insert, sql, _pgsqlInsert.InternalParams);
            _pgsqlInsert.InternalOrm.Aop.CurdBeforeHandler?.Invoke(_pgsqlInsert, before);
            long ret = 0;
            Exception exception = null;
            try
            {
                ret = _pgsqlInsert.InternalOrm.Ado.ExecuteNonQuery(_pgsqlInsert.InternalConnection, _pgsqlInsert.InternalTransaction, CommandType.Text, sql, _pgsqlInsert._commandTimeout, _pgsqlInsert.InternalParams);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw ex;
            }
            finally
            {
                var after = new CurdAfterEventArgs(before, exception, ret);
                _pgsqlInsert.InternalOrm.Aop.CurdAfterHandler?.Invoke(_pgsqlInsert, after);
                ClearData();
            }
            return ret;
        }

#if net40
#else
        async public Task<long> ExecuteAffrowsAsync(CancellationToken cancellationToken = default)
        {
            var sql = this.ToSql();
            if (string.IsNullOrEmpty(sql)) return 0;

            var before = new CurdBeforeEventArgs(_pgsqlInsert.InternalTable.Type, _pgsqlInsert.InternalTable, CurdType.Insert, sql, _pgsqlInsert.InternalParams);
            _pgsqlInsert.InternalOrm.Aop.CurdBeforeHandler?.Invoke(_pgsqlInsert, before);
            long ret = 0;
            Exception exception = null;
            try
            {
                ret = await _pgsqlInsert.InternalOrm.Ado.ExecuteNonQueryAsync(_pgsqlInsert.InternalConnection, _pgsqlInsert.InternalTransaction, CommandType.Text, sql, _pgsqlInsert._commandTimeout, _pgsqlInsert.InternalParams, cancellationToken);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw ex;
            }
            finally
            {
                var after = new CurdAfterEventArgs(before, exception, ret);
                _pgsqlInsert.InternalOrm.Aop.CurdAfterHandler?.Invoke(_pgsqlInsert, after);
                ClearData();
            }
            return ret;
        }
#endif
    }
}
