﻿using FreeSql.Internal.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FreeSql.Internal.CommonProvider
{
    public class SelectGroupingProvider : BaseDiyMemberExpression
    {
        public IFreeSql _orm;
        public Select0Provider _select;
        public CommonExpression _comonExp;
        public List<SelectTableInfo> _tables;
        public int _groupByLimit, _groupBySkip;
        public bool _addFieldAlias;
        public bool _flagNestedFieldAlias;

        public SelectGroupingProvider(IFreeSql orm, Select0Provider select, ReadAnonymousTypeInfo map, string field, CommonExpression comonExp, List<SelectTableInfo> tables)
        {
            _orm = orm;
            _select = select;
            _map = map;
            _field = field;
            _comonExp = comonExp;
            _tables = tables;
        }

        public static ThreadLocal<string> _ParseExpOnlyDbField = new ThreadLocal<string>();
        public override string ParseExp(Expression[] members)
        {
            ParseExpMapResult = null;
            if (members.Any() == false)
            {
                ParseExpMapResult = _map;
                return _map.DbField;
            }
            var firstMember = ((members.FirstOrDefault() as MemberExpression)?.Expression as MemberExpression);
            var parentName = firstMember?.Member.Name;
            switch (parentName)
            {
                case "Key":
                    var read = _map;
                    for (var a = 0; a < members.Length; a++)
                    {
                        read = read.Childs.Where(z => z.CsName == (members[a] as MemberExpression)?.Member.Name).FirstOrDefault();
                        if (read == null) return null;
                    }
                    ParseExpMapResult = read;
                    if (!_addFieldAlias) return read.DbField;
                    if (_flagNestedFieldAlias) return read.DbField;
                    if (_comonExp.EndsWithDbNestedField(read.DbField, read.DbNestedField) == false)
                    {
                        _ParseExpOnlyDbField.Value = read.DbField;
                        return $"{read.DbField}{_comonExp._common.FieldAsAlias(read.DbNestedField)}";
                    }
                    return read.DbField;
                case "Value":
                    var curtables = _tables;
                    SelectTableInfo curtable = null;
                    var foridx = 0;
                    if (_select._diymemexpWithTempQuery != null && _select._diymemexpWithTempQuery is Select0Provider.WithTempQueryParser tempQueryParser)
                    {
                        if (_select._tables.Count == 1)
                            curtable = _select._tables[0];
                        else
                        {
                            curtables = _select._tables;
                            LocalValueInitData();
                        }
                        if (tempQueryParser._outsideTable.Contains(curtable))
                        {
                            var replaceMember = firstMember.Type == curtable.Parameter.Type ? firstMember : members[0];
                            var replaceVistor = new CommonExpression.ReplaceVisitor();
                            for (var a = 0; a < members.Length; a++)
                                members[a] = replaceVistor.Modify(members[a], replaceMember, curtable.Parameter);
                            var ret = _select._diymemexpWithTempQuery.ParseExp(members);
                            ParseExpMapResult = _select._diymemexpWithTempQuery.ParseExpMapResult;
                            return ret;
                        }
                    }
                    else
                    {
                        LocalValueInitData();
                    }

                    void LocalValueInitData()
                    {
                        curtable = curtables.First();
                        if (members.Length > 1)
                        {
                            var mem0 = (members.FirstOrDefault() as MemberExpression);
                            var mem0Name = mem0?.Member.Name;
                            if (mem0Name?.StartsWith("Item") == true && int.TryParse(mem0Name.Substring(4), out var tryitemidx))
                            {
                                if (tryitemidx == 1) foridx++;
                                else
                                {
                                    //var alias = $"SP10{(char)(96 + tryitemidx)}";
                                    var tmptb = curtables.Where((a, idx) => //a.AliasInit == alias && 
                                        a.Table.Type == mem0.Type && idx == tryitemidx - 1).FirstOrDefault();
                                    if (tmptb != null)
                                    {
                                        curtable = tmptb;
                                        foridx++;
                                    }
                                }
                            }
                        }
                    }
                    var parmExp = Expression.Parameter(curtable.Table.Type, curtable.Alias);
                    Expression retExp = parmExp;
                    for (var a = foridx; a < members.Length; a++)
                    {
                        switch (members[a].NodeType)
                        {
                            case ExpressionType.Call:
                                retExp = Expression.Call(retExp, (members[a] as MethodCallExpression).Method);
                                break;
                            case ExpressionType.MemberAccess:
                                retExp = Expression.MakeMemberAccess(retExp, (members[a] as MemberExpression).Member);
                                break;
                            default:
                                return null;
                        }
                    }
                    return _comonExp.ExpressionLambdaToSql(retExp, new CommonExpression.ExpTSC { _tables = _tables, _tableRule = _select._tableRule, tbtype = SelectTableInfoType.From, isQuoteName = true, isDisableDiyParse = true, style = CommonExpression.ExpressionStyle.Where });
            }
            return null;
        }

        public void InternalHaving(Expression exp)
        {
            var sql = _comonExp.ExpressionWhereLambda(null, _select._tableRule, exp, this, null, null);
            var method = _select.GetType().GetMethod("Having", new[] { typeof(string), typeof(object) });
            method.Invoke(_select, new object[] { sql, null });
        }
        public void InternalOrderBy(Expression exp, bool isDescending)
        {
            if (exp.NodeType == ExpressionType.Lambda) exp = (exp as LambdaExpression)?.Body;
            if (exp?.NodeType == ExpressionType.New)
            {
                var newExp = exp as NewExpression;
                if (newExp != null)
                    for (var a = 0; a < newExp.Members.Count; a++)
                        InternalOrderBy(newExp.Arguments[a], isDescending);
                return;
            }
            var sql = _comonExp.ExpressionWhereLambda(null, _select._tableRule, exp, this, null, null);
            var method = _select.GetType().GetMethod("OrderBy", new[] { typeof(string), typeof(object) });
            method.Invoke(_select, new object[] { isDescending ? $"{sql} DESC" : sql, null });
        }
        public string InternalToSql(Expression select, FieldAliasOptions fieldAlias = FieldAliasOptions.AsIndex)
        {
            var map = new ReadAnonymousTypeInfo();
            var field = new StringBuilder();
            var index = fieldAlias == FieldAliasOptions.AsProperty ? CommonExpression.ReadAnonymousFieldAsCsName : 0;

            _comonExp.ReadAnonymousField(null, _select._tableRule, field, map, ref index, select, _select, this, null, null, null, false);
            var fieldSql = field.Length > 0 ? field.Remove(0, 2).ToString() : null;
            return InternalToSql(fieldSql);
        }
        public string InternalToSql(string field)
        {
            if (string.IsNullOrEmpty(field))
                throw new ArgumentException(CoreErrorStrings.Parameter_Field_NotSpecified);

            var isNestedPageSql = false;
            switch (_orm.Ado.DataType)
            {
                case DataType.Oracle:
                case DataType.OdbcOracle:
                case DataType.CustomOracle:
                case DataType.Dameng: //Oracle、Dameng 分组时，嵌套分页
                case DataType.GBase:
                    isNestedPageSql = true;
                    break;
                default:
                    _select._limit = _groupByLimit;
                    _select._skip = _groupBySkip;
                    break;
            }
            var sql = _select.ToSqlBase(field);
            if (isNestedPageSql == false)
            {
                _select._limit = 0;
                _select._skip = 0;
                return sql;
            }
            if (_groupByLimit == 0 && _groupBySkip == 0) return sql;
            return _orm.Select<object>().As("t").WithSql(sql).Limit(_groupByLimit).Skip(_groupBySkip).ToSql("t.*");
        }
    }

    public class SelectGroupingProvider<TKey, TValue> : SelectGroupingProvider, ISelectGrouping<TKey, TValue>
    {
        public SelectGroupingProvider(IFreeSql orm, Select0Provider select, ReadAnonymousTypeInfo map, string field, CommonExpression comonExp, List<SelectTableInfo> tables)
            : base(orm, select, map, field, comonExp, tables) { }

        public string ToSql<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select, FieldAliasOptions fieldAlias = FieldAliasOptions.AsIndex)
        {
            _lambdaParameter = select?.Parameters[0];
            return InternalToSql(select, fieldAlias);
        }
        public string ToSql(string field) => InternalToSql(field);

        public ISelect<TDto> WithTempQuery<TDto>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TDto>> selector)
        {
            if (_orm.CodeFirst.IsAutoSyncStructure)
                (_orm.CodeFirst as CodeFirstProvider)._dicSycedTryAdd(typeof(TDto)); //._dicSyced.TryAdd(typeof(TReturn), true);
            var ret = (_orm as BaseDbProvider).CreateSelectProvider<TDto>(null) as Select1Provider<TDto>;
            ret._commandTimeout = _select._commandTimeout;
            ret._connection = _select._connection;
            ret._transaction = _select._transaction;
            ret._whereGlobalFilter = new List<GlobalFilter.Item>(_select._whereGlobalFilter.ToArray());
            ret._cancel = _select._cancel;
            //ret._params.AddRange(_select._params); //#1965 WithTempQueryParser 子查询参数化，押后添加参数
            if (ret._tables[0].Table == null) ret._tables[0].Table = TableInfo.GetDefaultTable(typeof(TDto));
            Select0Provider.WithTempQueryParser parser = null;
            _addFieldAlias = true; //解决：[Column(Name = "flevel") 与属性名不一致时，嵌套查询 bug
            _flagNestedFieldAlias = true;//解决重复设置别名问题：.GroupBy((l, p) => new { p.ID, ShopType=l.ShopType??0 }).WithTempQuery(a => new { Money = a.Sum(a.Value.Item1.Amount)* a.Key.ShopType })
            var old_field = _field;
            var fieldsb = new StringBuilder();
            if (_map.Childs.Any() == false) fieldsb.Append(", ").Append(_map.DbField).Append(_comonExp.EndsWithDbNestedField(_map.DbField, _map.DbNestedField) ? "" : _comonExp._common.FieldAsAlias(_map.DbNestedField));
            foreach (var child in _map.GetAllChilds())
                fieldsb.Append(", ").Append(child.DbField).Append(_comonExp.EndsWithDbNestedField(child.DbField, child.DbNestedField) ? "" : _comonExp._common.FieldAsAlias(child.DbNestedField));
            _field = fieldsb.ToString();
            fieldsb.Clear();
            try
            {
                parser = new Select0Provider.WithTempQueryParser(_select, this, selector, ret._tables[0]);
            }
            finally
            {
                fieldsb.Clear();
                _field = old_field;
                _addFieldAlias = false;
                _flagNestedFieldAlias = false;
            }
            var sql = $"\r\n{this.ToSql(parser._insideSelectList[0].InsideField)}";
            ret.WithSql(sql);
            ret._diymemexpWithTempQuery = parser;
            ret._params.AddRange(_select._params);
            return ret;
        }

        public ISelectGrouping<TKey, TValue> Skip(int offset)
        {
            _groupBySkip = offset;
            return this;
        }
        public ISelectGrouping<TKey, TValue> Offset(int offset) => this.Skip(offset);
        public ISelectGrouping<TKey, TValue> Limit(int limit)
        {
            _groupByLimit = limit;
            return this;
        }
        public ISelectGrouping<TKey, TValue> Take(int limit) => this.Limit(limit);
        public ISelectGrouping<TKey, TValue> Page(int pageNumber, int pageSize)
        {
            _groupBySkip = Math.Max(0, pageNumber - 1) * pageSize;
            _groupByLimit = pageSize;
            return this;
        }

        public ISelectGrouping<TKey, TValue> Page(BasePagingInfo pagingInfo)
        {
            pagingInfo.Count = this.Count();
            _groupBySkip = Math.Max(0, pagingInfo.PageNumber - 1) * pagingInfo.PageSize;
            _groupByLimit = pagingInfo.PageSize;
            return this;
        }

        public long Count() => _select._cancel?.Invoke() == true ? 0 : long.TryParse(string.Concat(_orm.Ado.ExecuteScalar(_select._connection, _select._transaction, CommandType.Text, $"select count(1) from ({this.ToSql($"1{_comonExp._common.FieldAsAlias("as1")}")}) fta", _select._commandTimeout, _select._params.ToArray())), out var trylng) ? trylng : default(long);
        public ISelectGrouping<TKey, TValue> Count(out long count)
        {
            count = this.Count();
            return this;
        }

        public ISelectGrouping<TKey, TValue> HavingIf(bool condition, Expression<Func<ISelectGroupingAggregate<TKey, TValue>, bool>> exp) => condition ? Having(exp) : this;
        public ISelectGrouping<TKey, TValue> Having(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, bool>> exp)
        {
            _lambdaParameter = exp?.Parameters[0];
            InternalHaving(exp);
            return this;
        }
        public ISelectGrouping<TKey, TValue> OrderBy<TMember>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TMember>> column)
        {
            _lambdaParameter = column?.Parameters[0];
            InternalOrderBy(column, false);
            return this;
        }
        public ISelectGrouping<TKey, TValue> OrderByDescending<TMember>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TMember>> column)
        {
            _lambdaParameter = column?.Parameters[0];
            InternalOrderBy(column, true);
            return this;
        }

        public List<TReturn> Select<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select) => ToList(select);
        public TReturn First<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select) => ToList<TReturn>(select).FirstOrDefault();
        public List<TReturn> ToList<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select)
        {
            var map = new ReadAnonymousTypeInfo();
            var field = new StringBuilder();
            var index = 0;
            
            _lambdaParameter = select?.Parameters[0];
            _comonExp.ReadAnonymousField(null, _select._tableRule, field, map, ref index, select, _select, this, null, null, null, false);
            if (map.Childs.Any() == false && map.MapType == null) map.MapType = typeof(TReturn);
            var fieldSql = field.Length > 0 ? field.Remove(0, 2).ToString() : null;
            return _select.ToListMrPrivate<TReturn>(InternalToSql(fieldSql), new ReadAnonymousTypeAfInfo(map, fieldSql), null);
        }
        public Dictionary<TKey, TElement> ToDictionary<TElement>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TElement>> elementSelector)
        {
            var map = new ReadAnonymousTypeInfo();
            var field = new StringBuilder();
            var index = 0;

            _lambdaParameter = elementSelector?.Parameters[0];
            _comonExp.ReadAnonymousField(null, _select._tableRule, field, map, ref index, elementSelector, _select, this, null, null, null, false);
            if (map.Childs.Any() == false && map.MapType == null) map.MapType = typeof(TElement);
            var fieldSql = field.Length > 0 ? field.Remove(0, 2).ToString() : null;
            var otherAf = new ReadAnonymousTypeOtherInfo(_field, _map, new List<object>());
            var values = _select.ToListMrPrivate<TElement>(InternalToSql($"{fieldSql}{_field}"), new ReadAnonymousTypeAfInfo(map, fieldSql), new[] { otherAf });
            return otherAf.retlist.Select((a, b) => new KeyValuePair<TKey, TElement>((TKey)a, values[b])).ToDictionary(a=>a.Key,a=>a.Value);
        }

#if net40
#else
        async public Task<long> CountAsync(CancellationToken cancellationToken = default) => _select._cancel?.Invoke() == true ? 0 : long.TryParse(string.Concat(await _orm.Ado.ExecuteScalarAsync(_select._connection, _select._transaction, CommandType.Text, $"select count(1) from ({this.ToSql($"1{_comonExp._common.FieldAsAlias("as1")}")}) fta", _select._commandTimeout, _select._params.ToArray(), cancellationToken)), out var trylng) ? trylng : default(long);

        async public Task<TReturn> FirstAsync<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select, CancellationToken cancellationToken = default) => (await ToListAsync<TReturn>(select, cancellationToken)).FirstOrDefault();
        public Task<List<TReturn>> ToListAsync<TReturn>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TReturn>> select, CancellationToken cancellationToken = default)
        {
            var map = new ReadAnonymousTypeInfo();
            var field = new StringBuilder();
            var index = 0;

            _lambdaParameter = select?.Parameters[0];
            _comonExp.ReadAnonymousField(null, _select._tableRule, field, map, ref index, select, _select, this, null, null, null, false);
            if (map.Childs.Any() == false && map.MapType == null) map.MapType = typeof(TReturn);
            var fieldSql = field.Length > 0 ? field.Remove(0, 2).ToString() : null;
            return _select.ToListMrPrivateAsync<TReturn>(InternalToSql(fieldSql), new ReadAnonymousTypeAfInfo(map, fieldSql), null, cancellationToken);
        }
        public async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TElement>(Expression<Func<ISelectGroupingAggregate<TKey, TValue>, TElement>> elementSelector, CancellationToken cancellationToken = default)
        {
            var map = new ReadAnonymousTypeInfo();
            var field = new StringBuilder();
            var index = 0;

            _lambdaParameter = elementSelector?.Parameters[0];
            _comonExp.ReadAnonymousField(null, _select._tableRule, field, map, ref index, elementSelector, _select, this, null, null, null, false);
            if (map.Childs.Any() == false && map.MapType == null) map.MapType = typeof(TElement);
            var fieldSql = field.Length > 0 ? field.Remove(0, 2).ToString() : null;
            var otherAf = new ReadAnonymousTypeOtherInfo(_field, _map, new List<object>());
            var values = await _select.ToListMrPrivateAsync<TElement>(InternalToSql($"{fieldSql}{_field}"), new ReadAnonymousTypeAfInfo(map, fieldSql), new[] { otherAf }, cancellationToken);
            return otherAf.retlist.Select((a, b) => new KeyValuePair<TKey, TElement>((TKey)a, values[b])).ToDictionary(a => a.Key, a => a.Value);
        }
#endif
    }
}
