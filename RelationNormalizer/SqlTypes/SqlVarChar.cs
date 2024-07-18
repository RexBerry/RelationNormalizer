namespace RelationNormalizer.SqlTypes;

internal class SqlVarChar(bool notNull, int lengthLimit) : SqlType(notNull)
{
    public int LengthLimit { get; private init; } = lengthLimit;

    protected override string GetSql() => $"varchar({LengthLimit})";
}
