namespace RelationNormalizer.SqlTypes;

internal class SqlDecimal(bool notNull, int precision, int scale)
    : SqlType(notNull)
{
    public int Precision { get; private init; } = precision;
    public int Scale { get; private init; } = scale;

    protected override string GetSql() => $"decimal({Precision},{Scale})";
}
