using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using RelationNormalizer.SqlTypes;

namespace RelationNormalizer.Parsers;

/// <summary>
/// Infers the SQL type of a column of data.
/// </summary>
internal class SqlTypeInferrer
{
    private const int MaxDecimalPrecision = 38; // From MS SQL Server

    /// <summary>
    /// Regex used to match the <c>bit</c> type.
    /// </summary>
    private static readonly Regex BitRegex;

    /// <summary>
    /// Regex used to match integer types.
    /// </summary>
    private static readonly Regex IntRegex;

    /// <summary>
    /// Regex used to match float and decimal types.
    /// </summary>
    private static readonly Regex FloatRegex;

    /// <summary>
    /// Regex used to match the <c>date</c> type.
    /// </summary>
    private static readonly Regex DateRegex;

    /// <summary>
    /// Regex used to match the <c>time</c> type.
    /// </summary>
    private static readonly Regex TimeRegex;

    /// <summary>
    /// Regex used to match the <c>datetime</c> type.
    /// </summary>
    private static readonly Regex DateTimeRegex;

    static SqlTypeInferrer()
    {
        const RegexOptions options = RegexOptions.Compiled;
        var timeout = TimeSpan.FromSeconds(1.0);

        // These are intentionally looser than the ISO 8601 standard
        var datePattern =
            @"\d{1,2}([\/\-])\d{1,2}\1\d{2,}|\d{2,}([\/\-])\d{1,2}\2\d{1,2}";
        var timePattern =
            @"\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?: ?(?:AM|PM))?(?:Z|[\+\-−±]\d{1,2}:\d{2})?";

        BitRegex = new(@"^(?:0|1|TRUE|FALSE)$", options, timeout);
        IntRegex = new(@"^\d+$", options, timeout);
        FloatRegex = new(
            @"^[\-\+]?(?:\d+(\.\d*)?|\d*\.\d+)(?:E[\+\-]?\d+)?$",
            options,
            timeout
        );
        DateRegex = new($"^{datePattern}$", options, timeout);
        TimeRegex = new($"^T?{timePattern}$", options, timeout);
        DateTimeRegex = new(
            $"^{datePattern}(?:\\s+|T)?{timePattern}$",
            options,
            timeout
        );
    }

    /// <summary>
    /// Whether this column accepts <c>null</c> values.
    /// </summary>
    private bool _nullable = false;

    /// <summary>
    /// Whether this column could be of the <c>bit</c> type.
    /// </summary>
    private bool _possiblyBit = true;

    /// <summary>
    /// Whether this column could be of a numeric type.
    /// </summary>
    private bool _possiblyNumber = true;

    /// <summary>
    /// Whether this column could be of the <c>date</c> type.
    /// </summary>
    private bool _possiblyDate = true;

    /// <summary>
    /// Whether this column could be of the <c>time</c> type.
    /// </summary>
    private bool _possiblyTime = true;

    /// <summary>
    /// Whether this column could be of the <c>datetime</c> type.
    /// </summary>
    private bool _possiblyDateTime = true;

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
    /// <exception cref="OverflowException">A number is too big.</exception>
    public void AddValue(string value)
    {
        value = NormalizeString(value);
        var utf8Length = Encoding.UTF8.GetByteCount(value);

        _minLength = _minLength is null
            ? utf8Length
            : Math.Min((int)_minLength, utf8Length);
        _maxLength = _maxLength is null
            ? utf8Length
            : Math.Max((int)_maxLength, utf8Length);

        if (value == "NULL")
        {
            _nullable = true;
            return;
        }

        _possiblyBit = _possiblyBit && BitRegex.IsMatch(value);
        _possiblyDate = _possiblyDate && DateRegex.IsMatch(value);
        _possiblyTime = _possiblyTime && TimeRegex.IsMatch(value);
        // Do not allow dates or times to be datetimes
        _possiblyDateTime = _possiblyDateTime && DateTimeRegex.IsMatch(value);

        if (!_possiblyNumber)
        {
            return;
        }

        var hasSign = value[0] is '+' or '-';

        if (IntRegex.IsMatch(value))
        {
            var precision = value.Length - (hasSign ? 1 : 0);
            var intValue = BigInteger.Parse(value);

            _minValue = _minValue is null
                ? intValue
                : BigInteger.Min((BigInteger)_minValue, intValue);
            _maxValue = _maxValue is null
                ? intValue
                : BigInteger.Max((BigInteger)_maxValue, intValue);

            _precision = Math.Max(_precision, precision + _scale);
        }
        else if (FloatRegex.IsMatch(value))
        {
            if (value.Contains('E'))
            {
                // Must be a float
                _precision = MaxDecimalPrecision + 1;
                _scale = 1;
            }
            else
            {
                var decimalPointIndex = value.IndexOf('.');
                if (decimalPointIndex == -1)
                {
                    decimalPointIndex = value.Length;
                }

                var leftDigitCount = decimalPointIndex - (hasSign ? 1 : 0);
                var rightDigitCount = Math.Max(
                    0,
                    value.Length - (decimalPointIndex + 1)
                );

                var newScale = Math.Max(_scale, rightDigitCount);
                _precision += newScale - _scale;
                _scale = newScale;
                _precision = Math.Max(
                    _precision,
                    leftDigitCount + rightDigitCount
                );

                var doubleValue = double.Parse(value);

                // Some rounding stuff

                if (doubleValue > 0.0)
                {
                    doubleValue = Math.BitIncrement(doubleValue);
                }
                else if (doubleValue < 0.0)
                {
                    doubleValue = Math.BitDecrement(doubleValue);
                }

                var intValue = new BigInteger(doubleValue);

                if (intValue > 0)
                {
                    intValue += 1;
                }
                else
                {
                    intValue -= 1;
                }

                _minValue = _minValue is null
                    ? intValue
                    : BigInteger.Min((BigInteger)_minValue, intValue);
                _maxValue = _maxValue is null
                    ? intValue
                    : BigInteger.Max((BigInteger)_maxValue, intValue);
            }
        }
        else
        {
            _possiblyNumber = false;
        }
    }

    /// <summary>
    /// Infers the most appropriate SQL type for this column.
    /// </summary>
    /// <param name="options">The options to use for type inference, or null to
    /// use the default options.</param>
    /// <returns>The inferred SQL type of the column data added.</returns>
    /// <exception cref="InvalidOperationException">
    /// No values have been added.</exception>
    public SqlType InferSqlType(SqlTypeInferenceOptions? options = null)
    {
        if (_minLength is null)
        {
            throw new InvalidOperationException(
                "Can't infer type of empty column."
            );
        }

        if (options is null)
        {
            options = new();
        }

        var notNull = !_nullable;

        if (_possiblyBit)
        {
            return new SqlBit(notNull);
        }

        var minLength = (int)_minLength!;
        var maxLength = (int)_maxLength!;
        var precision = _precision + options.PrecisionIncrement;
        var scale = _scale;
        var possiblyNumber =
            _possiblyNumber
            && !(precision > MaxDecimalPrecision && _scale == 0);

        if (possiblyNumber)
        {
            var minValue = (BigInteger)_minValue! * options.IntRangeFactor;
            var maxValue = (BigInteger)_maxValue! * options.IntRangeFactor;

            if (precision > MaxDecimalPrecision)
            {
                return new SqlFloat(notNull);
            }
            else if (scale > 0)
            {
                return new SqlDecimal(notNull, precision, scale);
            }
            // Don't use tinyint and mediumint, because they're not widely supported
            else if (
                options.UseSmallInt
                && minValue >= short.MinValue
                && maxValue <= short.MaxValue
            )
            {
                return new SqlSmallInt(notNull);
            }
            else if (minValue >= int.MinValue && maxValue <= int.MaxValue)
            {
                return new SqlInt(notNull);
            }
            else if (minValue >= long.MinValue && maxValue <= long.MaxValue)
            {
                return new SqlBigInt(notNull);
            }
            else
            {
                // Won't fit any integer type, instead use decimal with scale 0
                return new SqlDecimal(notNull, precision, 0);
            }
        }
        else if (_possiblyDate)
        {
            return new SqlDate(notNull);
        }
        else if (_possiblyTime)
        {
            return new SqlTime(notNull);
        }
        else if (_possiblyDateTime)
        {
            return new SqlDateTime(notNull);
        }
        else if (minLength == maxLength)
        {
            // Assume it'll always be the same length
            return new SqlChar(notNull, maxLength);
        }
        else
        {
            var sizeLimit = Math.Max(
                options.VarCharMinLengthLimit,
                options.VarCharSizeFactor * maxLength
            );
            return new SqlVarChar(notNull, sizeLimit);
        }
    }

    /// <summary>
    /// Converts a string to a normal form to remove extraneous information
    /// that would be detrimental for type inference.
    /// </summary>
    /// <param name="value">The string to normalize.</param>
    /// <returns>A string without leading and trailing whitespace,
    /// all uppercase, and in Unicode NFD.</returns>
    private static string NormalizeString(string value) =>
        value.Normalize(NormalizationForm.FormD).ToUpperInvariant().Trim();
    // Use NFD because it's longer than NFC, so varchar will have enough
    // room if a string in NFD makes it into the database
}
