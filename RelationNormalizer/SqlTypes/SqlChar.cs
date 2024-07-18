namespace RelationNormalizer.SqlTypes;

internal class SqlChar(bool notNull, int length) : SqlType(notNull)
{
    public int Length { get; private init; } = length;

    protected override string GetSql() => $"char({Length})";
}
