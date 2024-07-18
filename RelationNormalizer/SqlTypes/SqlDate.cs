namespace RelationNormalizer.SqlTypes;

internal class SqlDate(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "date";
}
