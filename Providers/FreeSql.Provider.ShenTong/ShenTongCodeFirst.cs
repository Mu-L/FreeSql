﻿using FreeSql.Internal;
using FreeSql.Internal.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.OscarClient;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace FreeSql.ShenTong
{

    class ShenTongCodeFirst : Internal.CommonProvider.CodeFirstProvider
    {

        public ShenTongCodeFirst(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression) : base(orm, commonUtils, commonExpression) { }

        static object _dicCsToDbLock = new object();
        static Dictionary<string, CsToDb<OscarDbType>> _dicCsToDb = new Dictionary<string, CsToDb<OscarDbType>>() {

                { typeof(sbyte).FullName, CsToDb.New(OscarDbType.SmallInt, "int2","int2 NOT NULL", false, false, 0) },{ typeof(sbyte?).FullName, CsToDb.New(OscarDbType.SmallInt, "int2", "int2", false, true, null) },
                { typeof(short).FullName, CsToDb.New(OscarDbType.SmallInt, "int2","int2 NOT NULL", false, false, 0) },{ typeof(short?).FullName, CsToDb.New(OscarDbType.SmallInt, "int2", "int2", false, true, null) },
                { typeof(int).FullName, CsToDb.New(OscarDbType.Integer, "int4","int4 NOT NULL", false, false, 0) },{ typeof(int?).FullName, CsToDb.New(OscarDbType.Integer, "int4", "int4", false, true, null) },
                { typeof(long).FullName, CsToDb.New(OscarDbType.BigInt, "int8","int8 NOT NULL", false, false, 0) },{ typeof(long?).FullName, CsToDb.New(OscarDbType.BigInt, "int8", "int8", false, true, null) },

                { typeof(byte).FullName, CsToDb.New(OscarDbType.SmallInt, "int2","int2 NOT NULL", false, false, 0) },{ typeof(byte?).FullName, CsToDb.New(OscarDbType.SmallInt, "int2", "int2", false, true, null) },
                { typeof(ushort).FullName, CsToDb.New(OscarDbType.Integer, "int4","int4 NOT NULL", false, false, 0) },{ typeof(ushort?).FullName, CsToDb.New(OscarDbType.Integer, "int4", "int4", false, true, null) },
                { typeof(uint).FullName, CsToDb.New(OscarDbType.BigInt, "int8","int8 NOT NULL", false, false, 0) },{ typeof(uint?).FullName, CsToDb.New(OscarDbType.BigInt, "int8", "int8", false, true, null) },
                { typeof(ulong).FullName, CsToDb.New(OscarDbType.Numeric, "numeric","numeric(20,0) NOT NULL", false, false, 0) },{ typeof(ulong?).FullName, CsToDb.New(OscarDbType.Numeric, "numeric", "numeric(20,0)", false, true, null) },

                { typeof(float).FullName, CsToDb.New(OscarDbType.Real, "float4","float4 NOT NULL", false, false, 0) },{ typeof(float?).FullName, CsToDb.New(OscarDbType.Real, "float4", "float4", false, true, null) },
                { typeof(double).FullName, CsToDb.New(OscarDbType.Double, "float8","float8 NOT NULL", false, false, 0) },{ typeof(double?).FullName, CsToDb.New(OscarDbType.Double, "float8", "float8", false, true, null) },
                { typeof(decimal).FullName, CsToDb.New(OscarDbType.Numeric, "numeric", "numeric(10,2) NOT NULL", false, false, 0) },{ typeof(decimal?).FullName, CsToDb.New(OscarDbType.Numeric, "numeric", "numeric(10,2)", false, true, null) },

                { typeof(string).FullName, CsToDb.New(OscarDbType.VarChar, "varchar", "varchar(255)", false, null, "") },
                { typeof(char).FullName, CsToDb.New(OscarDbType.Char, "bpchar", "bpchar(1)", false, null, '\0') },

                { typeof(TimeSpan).FullName, CsToDb.New(OscarDbType.Time, "time","time NOT NULL", false, false, 0) },{ typeof(TimeSpan?).FullName, CsToDb.New(OscarDbType.Time, "time", "time",false, true, null) },
                { typeof(DateTime).FullName, CsToDb.New(OscarDbType.TimeStamp, "timestamp", "timestamp NOT NULL", false, false, new DateTime(1970,1,1)) },{ typeof(DateTime?).FullName, CsToDb.New(OscarDbType.TimeStamp, "timestamp", "timestamp", false, true, null) },

                { typeof(bool).FullName, CsToDb.New(OscarDbType.Boolean, "bool","bool NOT NULL", null, false, false) },{ typeof(bool?).FullName, CsToDb.New(OscarDbType.Boolean, "bool","bool", null, true, null) },
                { typeof(Byte[]).FullName, CsToDb.New(OscarDbType.Bytea, "bytea", "bytea", false, null, new byte[0]) },
                { typeof(Guid).FullName, CsToDb.New(OscarDbType.Char, "bpchar", "bpchar(36) NOT NULL", false, false, Guid.Empty) },{ typeof(Guid?).FullName, CsToDb.New(OscarDbType.Char, "bpchar", "bpchar(36)", false, true, null) },

            };

        public override DbInfoResult GetDbInfo(Type type)
        {
            var isarray = type.FullName != "System.Byte[]" && type.IsArray;
            var elementType = isarray ? type.GetElementType() : type;
            var info = GetDbInfoNoneArray(elementType);
            if (info == null) return null;
            if (isarray == false) return new DbInfoResult((int)info.type, info.dbtype, info.dbtypeFull, info.isnullable, info.defaultValue);
            var dbtypefull = Regex.Replace(info.dbtypeFull, $@"{info.dbtype}(\s*\([^\)]+\))?", "$0[]").Replace(" NOT NULL", "");
            return new DbInfoResult((int)(info.type | OscarDbType.Array), $"{info.dbtype}[]", dbtypefull, null, Array.CreateInstance(elementType, 0));
        }
        CsToDb<OscarDbType> GetDbInfoNoneArray(Type type)
        {
            if (_dicCsToDb.TryGetValue(type.FullName, out var trydc)) return trydc;
            if (type.IsArray) return null;
            var enumType = type.IsEnum ? type : null;
            if (enumType == null && type.IsNullableType())
            {
                var genericTypes = type.GetGenericArguments();
                if (genericTypes.Length == 1 && genericTypes.First().IsEnum) enumType = genericTypes.First();
            }
            if (enumType != null)
            {
                var newItem = enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Any() ?
                    CsToDb.New(OscarDbType.BigInt, "int8", $"int8{(type.IsEnum ? " NOT NULL" : "")}", false, type.IsEnum ? false : true, enumType.CreateInstanceGetDefaultValue()) :
                    CsToDb.New(OscarDbType.Integer, "int4", $"int4{(type.IsEnum ? " NOT NULL" : "")}", false, type.IsEnum ? false : true, enumType.CreateInstanceGetDefaultValue());
                if (_dicCsToDb.ContainsKey(type.FullName) == false)
                {
                    lock (_dicCsToDbLock)
                    {
                        if (_dicCsToDb.ContainsKey(type.FullName) == false)
                            _dicCsToDb.Add(type.FullName, newItem);
                    }
                }
                return newItem;
            }
            return null;
        }

        protected override string GetComparisonDDLStatements(params TypeSchemaAndName[] objects)
        {
            var sb = new StringBuilder();
            var seqcols = new List<NativeTuple<ColumnInfo, string[], bool>>(); //序列

            foreach (var obj in objects)
            {
                if (sb.Length > 0) sb.Append("\r\n");
                var tb = obj.tableSchema;
                if (tb == null) throw new Exception(CoreErrorStrings.S_Type_IsNot_Migrable(obj.tableSchema.Type.FullName));
                if (tb.Columns.Any() == false) throw new Exception(CoreErrorStrings.S_Type_IsNot_Migrable_0Attributes(obj.tableSchema.Type.FullName));
                var tbname = _commonUtils.SplitTableName(tb.DbName);
                if (tbname?.Length == 1) tbname = new[] { "PUBLIC", tbname[0] };

                var tboldname = _commonUtils.SplitTableName(tb.DbOldName); //旧表名
                if (tboldname?.Length == 1) tboldname = new[] { "PUBLIC", tboldname[0] };
                if (string.IsNullOrEmpty(obj.tableName) == false)
                {
                    var tbtmpname = _commonUtils.SplitTableName(obj.tableName);
                    if (tbtmpname?.Length == 1) tbtmpname = new[] { "PUBLIC", tbtmpname[0] };
                    if (tbname[0] != tbtmpname[0] || tbname[1] != tbtmpname[1])
                    {
                        tbname = tbtmpname;
                        tboldname = null;
                    }
                }
                //codefirst 不支持表名、模式名、数据库名中带 .

                if (string.Compare(tbname[0], "PUBLIC", true) != 0 && _orm.Ado.ExecuteScalar(CommandType.Text, _commonUtils.FormatSql(" select 1 from sys_namespace where nspname={0}", tbname[0])) == null) //创建模式
                    sb.Append("CREATE SCHEMA IF NOT EXISTS ").Append(tbname[0]).Append(";\r\n");

                var sbalter = new StringBuilder();
                var istmpatler = false; //创建临时表，导入数据，删除旧表，修改
                if (_orm.Ado.ExecuteScalar(CommandType.Text, string.Format(" select 1 from tables a inner join sys_namespace b on b.nspname = a.table_schem where b.nspname || '.' || a.table_name = '{0}.{1}'", tbname)) == null)
                { //表不存在
                    if (tboldname != null)
                    {
                        if (_orm.Ado.ExecuteScalar(CommandType.Text, string.Format(" select 1 from tables a inner join sys_namespace b on b.nspname = a.table_schem where b.nspname || '.' || a.table_name = '{0}.{1}'", tboldname)) == null)
                            //旧表不存在
                            tboldname = null;
                    }
                    if (tboldname == null)
                    {
                        //创建表
                        var createTableName = _commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}");
                        sb.Append("CREATE TABLE IF NOT EXISTS ").Append(createTableName).Append(" ( ");
                        foreach (var tbcol in tb.ColumnsByPosition)
                        {
                            sb.Append(" \r\n  ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(" ").Append(tbcol.Attribute.DbType).Append(",");
                            if (tbcol.Attribute.IsIdentity == true) seqcols.Add(NativeTuple.Create(tbcol, tbname, true));
                        }
                        if (tb.Primarys.Any())
                        {
                            var pkname = $"{tbname[0]}_{tbname[1]}_PKEY";
                            sb.Append(" \r\n  CONSTRAINT ").Append(_commonUtils.QuoteSqlName(pkname)).Append(" PRIMARY KEY (");
                            foreach (var tbcol in tb.Primarys) sb.Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(", ");
                            sb.Remove(sb.Length - 2, 2).Append("),");
                        }
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append("\r\n) BINLOG ON;\r\n");
                        //创建表的索引
                        foreach (var uk in tb.Indexes)
                        {
                            sb.Append("CREATE ");
                            if (uk.IsUnique) sb.Append("UNIQUE ");
                            sb.Append("INDEX ").Append(_commonUtils.QuoteSqlName(ReplaceIndexName(uk.Name, tbname[1]))).Append(" ON ").Append(createTableName).Append("(");
                            foreach (var tbcol in uk.Columns)
                            {
                                sb.Append(_commonUtils.QuoteSqlName(tbcol.Column.Attribute.Name));
                                if (tbcol.IsDesc) sb.Append(" DESC");
                                sb.Append(", ");
                            }
                            sb.Remove(sb.Length - 2, 2).Append(");\r\n");
                        }
                        //备注
                        foreach (var tbcol in tb.ColumnsByPosition)
                        {
                            if (string.IsNullOrEmpty(tbcol.Comment) == false)
                                sb.Append("COMMENT ON COLUMN ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}.{tbcol.Attribute.Name}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tbcol.Comment)).Append(";\r\n");
                        }
                        if (string.IsNullOrEmpty(tb.Comment) == false)
                            sb.Append("COMMENT ON TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tb.Comment)).Append(";\r\n");
                        continue;
                    }
                    //如果新表，旧表在一个数据库和模式下，直接修改表名
                    if (string.Compare(tbname[0], tboldname[0], true) == 0)
                        sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tboldname[0]}.{tboldname[1]}")).Append(" RENAME TO ").Append(_commonUtils.QuoteSqlName($"{tbname[1]}")).Append(";\r\n");
                    else
                    {
                        //如果新表，旧表不在一起，创建新表，导入数据，删除旧表
                        istmpatler = true;
                    }
                }
                else
                    tboldname = null; //如果新表已经存在，不走改表名逻辑

                //对比字段，只可以修改类型、增加字段、有限的修改字段名；保证安全不删除字段
                var sql = _commonUtils.FormatSql(@"
select
a.attname,
t.typname,
case when a.atttypmod > 0 and a.atttypmod < 32767 then a.atttypmod - 4 else a.attlen end len,
case when t.typelem > 0 and t.typinput = 'ARRAY_IN' then t2.typname else t.typname end,
case when a.attnotnull then '0' else '1' end as is_nullable,
--e.adsrc,
(select sys_get_expr(adbin, adrelid) from sys_attrdef where adrelid = e.adrelid limit 1) is_identity,
a.attndims,
d.description as comment
from sys_class c
inner join sys_attribute a on a.attnum > 0 and a.attrelid = c.oid
inner join sys_type t on t.oid = a.atttypid
left join sys_type t2 on t2.oid = t.typelem
left join sys_description d on d.objoid = a.attrelid and d.objsubid = a.attnum
left join sys_attrdef e on e.adrelid = a.attrelid and e.adnum = a.attnum
inner join sys_namespace ns on ns.oid = c.relnamespace
inner join sys_namespace ns2 on ns2.oid = t.typnamespace
where ns.nspname = {0} and c.relname = {1}", tboldname ?? tbname);
                var ds = _orm.Ado.ExecuteArray(CommandType.Text, sql);
                var tbstruct = ds.ToDictionary(a => string.Concat(a[0]), a =>
                {
                    var attndims = int.Parse(string.Concat(a[6]));
                    var type = string.Concat(a[1]);
                    var sqlType = string.Concat(a[3]);
                    var max_length = long.Parse(string.Concat(a[2]));
                    switch (sqlType.ToLower())
                    {
                        case "bool": case "name": case "bit": case "varbit": case "bpchar": case "varchar": case "bytea": case "text": case "uuid": break;
                        default: max_length *= 8; break;
                    }
                    if (type.StartsWith("_"))
                    {
                        type = type.Substring(1);
                        if (attndims == 0) attndims++;
                    }
                    if (sqlType.StartsWith("_")) sqlType = sqlType.Substring(1);
                    return new
                    {
                        column = string.Concat(a[0]),
                        sqlType = string.Concat(sqlType),
                        max_length = long.Parse(string.Concat(a[2])),
                        is_nullable = string.Concat(a[4]) == "1",
                        is_identity = string.Concat(a[5]).StartsWith(@"NEXTVAL('") && (string.Concat(a[5]).EndsWith(@"'::text)") || string.Concat(a[5]).EndsWith(@"')")),
                        attndims,
                        comment = string.Concat(a[7])
                    };
                }, StringComparer.CurrentCultureIgnoreCase);

                if (istmpatler == false)
                {
                    foreach (var tbcol in tb.ColumnsByPosition)
                    {
                        if (tbstruct.TryGetValue(tbcol.Attribute.Name, out var tbstructcol) ||
                            string.IsNullOrEmpty(tbcol.Attribute.OldName) == false && tbstruct.TryGetValue(tbcol.Attribute.OldName, out tbstructcol))
                        {
                            var isCommentChanged = tbstructcol.comment != (tbcol.Comment ?? "");
                            var sqlTypeSize = tbstructcol.sqlType;
                            if (sqlTypeSize.Contains("(") == false)
                            {
                                switch (sqlTypeSize.ToLower())
                                {
                                    case "bit":
                                    case "varbit":
                                    case "bpchar":
                                    case "varchar":
                                        sqlTypeSize = $"{sqlTypeSize}({tbstructcol.max_length})"; break;
                                }
                            }
                            if (tbcol.Attribute.DbType.StartsWith(sqlTypeSize, StringComparison.CurrentCultureIgnoreCase) == false ||
                                tbcol.Attribute.DbType.Contains("[]") != (tbstructcol.attndims > 0))
                                sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" ALTER TYPE ").Append(_commonUtils.QuoteSqlName(tbstructcol.column)).Append(" ").Append(tbcol.Attribute.DbType.Split(' ').First()).Append(";\r\n");
                            if (tbcol.Attribute.IsNullable != tbstructcol.is_nullable)
                            {
                                if (tbcol.Attribute.IsNullable != true || tbcol.Attribute.IsNullable == true && tbcol.Attribute.IsPrimary == false)
                                {
                                    if (tbcol.Attribute.IsNullable == false)
                                        sbalter.Append("UPDATE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" SET ").Append(_commonUtils.QuoteSqlName(tbstructcol.column)).Append(" = ").Append(tbcol.DbDefaultValue).Append(" WHERE ").Append(_commonUtils.QuoteSqlName(tbstructcol.column)).Append(" IS NULL;\r\n");
                                    sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" ALTER COLUMN ").Append(_commonUtils.QuoteSqlName(tbstructcol.column)).Append(" ").Append(tbcol.Attribute.IsNullable == true ? "DROP" : "SET").Append(" NOT NULL;\r\n");
                                }
                            }
                            if (tbcol.Attribute.IsIdentity != tbstructcol.is_identity)
                                seqcols.Add(NativeTuple.Create(tbcol, tbname, tbcol.Attribute.IsIdentity == true));
                            if (string.Compare(tbstructcol.column, tbcol.Attribute.OldName, true) == 0)
                                //修改列名
                                sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" RENAME COLUMN ").Append(_commonUtils.QuoteSqlName(tbstructcol.column)).Append(" TO ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(";\r\n");
                            if (isCommentChanged)
                                sbalter.Append("COMMENT ON COLUMN ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}.{tbcol.Attribute.Name}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tbcol.Comment)).Append(";\r\n");
                            continue;
                        }
                        //添加列
                        sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" ADD COLUMN ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(" ").Append(tbcol.Attribute.DbType.Split(' ').First()).Append(";\r\n");
                        sbalter.Append("UPDATE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" SET ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(" = ").Append(tbcol.DbDefaultValue).Append(";\r\n");
                        if (tbcol.Attribute.IsNullable == false) sbalter.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" ALTER COLUMN ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(" SET NOT NULL;\r\n");
                        if (tbcol.Attribute.IsIdentity == true) seqcols.Add(NativeTuple.Create(tbcol, tbname, tbcol.Attribute.IsIdentity == true));
                        if (string.IsNullOrEmpty(tbcol.Comment) == false) sbalter.Append("COMMENT ON COLUMN ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}.{tbcol.Attribute.Name}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tbcol.Comment)).Append(";\r\n");
                    }
                    var dsuksql = _commonUtils.FormatSql(@"
select
c.attname,
b.relname,
--case when sys_index_column_has_property(b.oid, c.attnum, 'desc') = 't' then 1 else 0 end IsDesc,
0,
case when indisunique = 't' then 1 else 0 end IsUnique
from sys_index a
inner join sys_class b on b.oid = a.indexrelid
inner join sys_attribute c on c.attnum > 0 and c.attrelid = b.oid
inner join sys_namespace ns on ns.oid = b.relnamespace
inner join sys_class d on d.oid = a.indrelid
where ns.nspname in ({0}) and d.relname in ({1}) and a.indisprimary = 'f'", tboldname ?? tbname);
                    var dsuk = _orm.Ado.ExecuteArray(CommandType.Text, dsuksql).Select(a => new[] { string.Concat(a[0]), string.Concat(a[1]), string.Concat(a[2]), string.Concat(a[3]) });
                    foreach (var uk in tb.Indexes)
                    {
                        if (string.IsNullOrEmpty(uk.Name) || uk.Columns.Any() == false) continue;
                        var ukname = ReplaceIndexName(uk.Name, tbname[1]);
                        var dsukfind1 = dsuk.Where(a => string.Compare(a[1], ukname, true) == 0).ToArray();
                        if (dsukfind1.Any() == false || dsukfind1.Length != uk.Columns.Length || dsukfind1.Where(a => uk.Columns.Where(b => (a[3] == "1") == uk.IsUnique && string.Compare(b.Column.Attribute.Name, a[0], true) == 0 && (a[2] == "1") == b.IsDesc).Any()).Count() != uk.Columns.Length)
                        {
                            if (dsukfind1.Any()) sbalter.Append("DROP INDEX ").Append(_commonUtils.QuoteSqlName(ukname)).Append(";\r\n");
                            sbalter.Append("CREATE ");
                            if (uk.IsUnique) sbalter.Append("UNIQUE ");
                            sbalter.Append("INDEX ").Append(_commonUtils.QuoteSqlName(ukname)).Append(" ON ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append("(");
                            foreach (var tbcol in uk.Columns)
                            {
                                sbalter.Append(_commonUtils.QuoteSqlName(tbcol.Column.Attribute.Name));
                                if (tbcol.IsDesc) sbalter.Append(" DESC");
                                sbalter.Append(", ");
                            }
                            sbalter.Remove(sbalter.Length - 2, 2).Append(");\r\n");
                        }
                    }
                }
                if (istmpatler == false)
                {
                    var dbcomment = string.Concat(_orm.Ado.ExecuteScalar(CommandType.Text, _commonUtils.FormatSql(@" select
d.description
from sys_class a
inner join sys_namespace b on b.oid = a.relnamespace
left join sys_description d on d.objoid = a.oid and objsubid = 0
where b.nspname not in ('DIRECTORIES', 'INFO_SCHEM', 'REPLICATION', 'STAGENT', 'SYSAUDIT', 'SYSDBA', 'SYSFTSDBA', 'SYSSECURE', 'SYS_GLOBAL_TEMP', 'WMSYS') and a.relkind in ('r') and b.nspname = {0} and a.relname = {1}
and b.nspname || '.' || a.relname not in ('PUBLIC.GEOGRAPHY_COLUMNS','PUBLIC.GEOMETRY_COLUMNS','PUBLIC.RASTER_COLUMNS','PUBLIC.RASTER_OVERVIEWS')", tbname[0], tbname[1])));
                    if (dbcomment != (tb.Comment ?? ""))
                        sbalter.Append("COMMENT ON TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tb.Comment)).Append(";\r\n");

                    sb.Append(sbalter);
                    continue;
                }
                var oldpk = _orm.Ado.ExecuteScalar(CommandType.Text, _commonUtils.FormatSql(@" select sys_constraint.conname as pk_name from sys_constraint
inner join sys_class on sys_constraint.conrelid = sys_class.oid
inner join sys_namespace on sys_namespace.oid = sys_class.relnamespace
where sys_namespace.nspname={0} and sys_class.relname={1} and sys_constraint.contype='p'
", tbname))?.ToString();
                if (string.IsNullOrEmpty(oldpk) == false)
                    sb.Append("ALTER TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}")).Append(" DROP CONSTRAINT ").Append(oldpk).Append(";\r\n");

                //创建临时表，数据导进临时表，然后删除原表，将临时表改名为原表名
                var newtablename = _commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}");
                var tablename = tboldname == null ? newtablename : _commonUtils.QuoteSqlName($"{tboldname[0]}.{tboldname[1]}");
                var tmptablename = _commonUtils.QuoteSqlName($"{tbname[0]}.FTmp_{tbname[1]}");
                //创建临时表
                sb.Append("CREATE TABLE IF NOT EXISTS ").Append(tmptablename).Append(" ( ");
                foreach (var tbcol in tb.ColumnsByPosition)
                {
                    sb.Append(" \r\n  ").Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(" ").Append(tbcol.Attribute.DbType).Append(",");
                    if (tbcol.Attribute.IsIdentity == true) seqcols.Add(NativeTuple.Create(tbcol, tbname, true));
                }
                if (tb.Primarys.Any())
                {
                    var pkname = $"{tbname[0]}_{tbname[1]}_pkey";
                    sb.Append(" \r\n  CONSTRAINT ").Append(_commonUtils.QuoteSqlName(pkname)).Append(" PRIMARY KEY (");
                    foreach (var tbcol in tb.Primarys) sb.Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(", ");
                    sb.Remove(sb.Length - 2, 2).Append("),");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append("\r\n) BINLOG ON;\r\n");
                //备注
                foreach (var tbcol in tb.ColumnsByPosition)
                {
                    if (string.IsNullOrEmpty(tbcol.Comment) == false)
                        sb.Append("COMMENT ON COLUMN ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.FTmp_{tbname[1]}.{tbcol.Attribute.Name}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tbcol.Comment)).Append(";\r\n");
                }
                if (string.IsNullOrEmpty(tb.Comment) == false)
                    sb.Append("COMMENT ON TABLE ").Append(_commonUtils.QuoteSqlName($"{tbname[0]}.FTmp_{tbname[1]}")).Append(" IS ").Append(_commonUtils.FormatSql("{0}", tb.Comment)).Append(";\r\n");

                sb.Append("INSERT INTO ").Append(tmptablename).Append(" (");
                foreach (var tbcol in tb.ColumnsByPosition)
                    sb.Append(_commonUtils.QuoteSqlName(tbcol.Attribute.Name)).Append(", ");
                sb.Remove(sb.Length - 2, 2).Append(")\r\nSELECT ");
                foreach (var tbcol in tb.ColumnsByPosition)
                {
                    var insertvalue = "NULL";
                    if (tbstruct.TryGetValue(tbcol.Attribute.Name, out var tbstructcol) ||
                        string.IsNullOrEmpty(tbcol.Attribute.OldName) == false && tbstruct.TryGetValue(tbcol.Attribute.OldName, out tbstructcol))
                    {
                        insertvalue = _commonUtils.QuoteSqlName(tbstructcol.column);
                        if (tbcol.Attribute.DbType.StartsWith(tbstructcol.sqlType, StringComparison.CurrentCultureIgnoreCase) == false)
                            insertvalue = $"cast({insertvalue} as {tbcol.Attribute.DbType.Split(' ').First()})";
                        if (tbcol.Attribute.IsNullable != tbstructcol.is_nullable)
                            insertvalue = $"coalesce({insertvalue},{tbcol.DbDefaultValue})";
                    }
                    else if (tbcol.Attribute.IsNullable == false)
                        insertvalue = tbcol.DbDefaultValue;
                    sb.Append(insertvalue).Append(", ");
                }
                sb.Remove(sb.Length - 2, 2).Append(" FROM ").Append(tablename).Append(";\r\n");
                sb.Append("DROP TABLE ").Append(tablename).Append(";\r\n");
                sb.Append("ALTER TABLE ").Append(tmptablename).Append(" RENAME TO ").Append(_commonUtils.QuoteSqlName(tbname[1])).Append(";\r\n");
                //创建表的索引
                foreach (var uk in tb.Indexes)
                {
                    sb.Append("CREATE ");
                    if (uk.IsUnique) sb.Append("UNIQUE ");
                    sb.Append("INDEX ").Append(_commonUtils.QuoteSqlName(ReplaceIndexName(uk.Name, tbname[1]))).Append(" ON ").Append(newtablename).Append("(");
                    foreach (var tbcol in uk.Columns)
                    {
                        sb.Append(_commonUtils.QuoteSqlName(tbcol.Column.Attribute.Name));
                        if (tbcol.IsDesc) sb.Append(" DESC");
                        sb.Append(", ");
                    }
                    sb.Remove(sb.Length - 2, 2).Append(");\r\n");
                }
            }
            foreach (var seqcol in seqcols)
            {
                var tbname = seqcol.Item2;
                var seqname = Utils.GetCsName($"{tbname[0]}.{tbname[1]}_{seqcol.Item1.Attribute.Name}_seq").ToUpper(); ;
                var tbname2 = _commonUtils.QuoteSqlName($"{tbname[0]}.{tbname[1]}");
                var colname2 = _commonUtils.QuoteSqlName(seqcol.Item1.Attribute.Name);
                sb.Append("ALTER TABLE ").Append(tbname2).Append(" ALTER COLUMN ").Append(colname2).Append(" SET DEFAULT null;\r\n");
                sb.Append("DROP SEQUENCE IF EXISTS ").Append(seqname).Append(";\r\n");
                if (seqcol.Item3)
                {
                    sb.Append("CREATE SEQUENCE ").Append(seqname).Append(";\r\n");
                    sb.Append("ALTER TABLE ").Append(tbname2).Append(" ALTER COLUMN ").Append(colname2).Append(" SET DEFAULT NEXTVAL('").Append(seqname).Append("'::text);\r\n");
                    sb.Append(" SELECT case when max(").Append(colname2).Append(") is null then 0 else setval('").Append(seqname).Append("', max(").Append(colname2).Append(")) end FROM ").Append(tbname2).Append(";\r\n");
                }
            }
            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}