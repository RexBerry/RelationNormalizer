namespace RelationNormalizer.SqlTypes;

internal class SqlTime(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "time";
}
