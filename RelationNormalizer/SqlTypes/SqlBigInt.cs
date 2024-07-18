namespace RelationNormalizer.SqlTypes;

internal class SqlBigInt(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "bigint";
}
