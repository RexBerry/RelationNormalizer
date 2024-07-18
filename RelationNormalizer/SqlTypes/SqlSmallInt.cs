namespace RelationNormalizer.SqlTypes;

internal class SqlSmallInt(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "smallint";
}
