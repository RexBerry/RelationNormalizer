namespace RelationNormalizer.Parsers;

/// <summary>
/// An exception that can occur while parsing a row of CSV.
/// </summary>
/// <param name="message">The error message.</param>
internal class CsvParseException(string message) : Exception(message)
{
    /// <summary>
    /// The column the error occurred at, or -1 if no value was set.
    /// </summary>
    public int Column { get; init; } = -1;
}
