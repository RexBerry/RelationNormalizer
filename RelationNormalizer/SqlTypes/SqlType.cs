namespace RelationNormalizer.SqlTypes;

/// <summary>
/// Represents the type of an SQL column.
/// </summary>
internal abstract class SqlType
{
    /// <summary>
    /// Whether the column type should contain <c>not null</c>.
    /// </summary>
    public bool NotNull { get; private init; }

    /// <summary>
    /// Constructs a new SqlType object.
    /// </summary>
    /// <param name="notNull">Whether the column type should
    /// contain <c>not null.</c></param>
    protected SqlType(bool notNull)
    {
        NotNull = notNull;
    }

    /// <summary>
    /// Parses a string representing a value of this SQL type
    /// and converts it to a normalized form.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>A string that can be used to check equality.</returns>
    public virtual string ParseValue(string value) => value;

    /// <summary>
    /// Generates the SQL code for this type.
    /// </summary>
    /// <returns>The SQL code for this type, to be used in a <c>create
    /// table</c> statement.</returns>
    public string GenerateSqlCode() =>
        NotNull ? $"{GetSql()} not null" : GetSql();

    /// <summary>
    /// Generates the SQL code for this type, disregarding <c>not null</c>.
    /// </summary>
    /// <returns>The SQL code for this type, not including
    /// <c>not null.</c></returns>
    protected abstract string GetSql();
}
