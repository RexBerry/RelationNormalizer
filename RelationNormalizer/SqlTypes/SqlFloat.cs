namespace RelationNormalizer.SqlTypes;

internal class SqlFloat(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "float";
}
