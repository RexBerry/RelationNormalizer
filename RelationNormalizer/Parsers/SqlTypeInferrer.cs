using System.Numerics;
using RelationNormalizer.SqlTypes;

namespace RelationNormalizer.Parsers;

/// <summary>
/// Infers the SQL type of a column of data.
/// </summary>
internal class SqlTypeInferrer
{
    /// <summary>
    /// Whether this column accepts <c>null</c> values.
    /// </summary>
    private bool _nullable = false;

    /// <summary>
    /// Whether this column could be of the <c>bit</c> type.
    /// </summary>
    private bool _possiblyBit = false;

    /// <summary>
    /// Whether this column could be of a numeric type.
    /// </summary>
    private bool _possiblyNumber = false;

    /// <summary>
    /// Whether this column could be of the <c>date</c> type.
    /// </summary>
    private bool _possiblyDate = false;

    /// <summary>
    /// Whether this column could be of the <c>time</c> type.
    /// </summary>
    private bool _possibleTime = false;

    /// <summary>
    /// Whether this column could be of the <c>datetime</c> type.
    /// </summary>
    private bool _possiblyDateTime = false;

    /// <summary>
    /// The minimum string length of values added, or null if no values have
    /// been added yet.
    /// </summary>
    private int? _minLength = null;

    /// <summary>
    /// The maximum string length of values added, or null if no values have
    /// been added yet.
    /// </summary>
    private int? _maxLength = null;

    /// <summary>
    /// The minimum numeric value added, or null if no numeric values have
    /// been added yet.
    /// </summary>
    private BigInteger? _minValue = null;

    /// <summary>
    /// The maximum numeric value added, or null if no numeric values have
    /// been added yet.
    /// </summary>
    private BigInteger? _maxValue = null;

    /// <summary>
    /// The decimal precision required.
    /// </summary>
    private int _precision = 0;

    /// <summary>
    /// The decimal scale required.
    /// </summary>
    private int _scale = 0;

    /// <summary>
    /// Constructs a new SqlTypeInferrer object with no data added.
    /// </summary>
    public SqlTypeInferrer() { }

    /// <summary>
    /// Adds a value to consider for type inference.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void AddValue(string value) => throw new NotImplementedException();

    /// <summary>
    /// Infers the most appropriate SQL type for this column.
    /// </summary>
    /// <param name="options">The options to use for type inference, or null to
    /// use the default options.</param>
    /// <returns>The inferred SQL type of the column data added.</returns>
    /// <exception cref="InvalidOperationException">
    /// No values have been added.</exception>
    public SqlType InferSqlType(SqlTypeInferenceOptions? options) =>
        throw new NotImplementedException();
}
