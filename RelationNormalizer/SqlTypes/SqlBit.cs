namespace RelationNormalizer.SqlTypes;

internal class SqlBit(bool notNull) : SqlType(notNull)
{
    protected override string GetSql() => "bit";
}
