namespace RelationNormalizer.SqlTypes;

internal class SqlInt(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "int";
}
