namespace RelationNormalizer.SqlTypes;

internal class SqlDateTime(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "datetime";
}
