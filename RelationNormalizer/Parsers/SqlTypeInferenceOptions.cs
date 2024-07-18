namespace RelationNormalizer.Parsers;

/// <summary>
/// Options for SQL type inference.
/// </summary>
/// <param name="UseSmallInt">Whether to use <c>smallint</c>.</param>
/// <param name="IntRangeFactor">The factor to multiple the minimum and maximum
/// integer values by before determining an appropriate integer type.</param>
/// <param name="PrecisionIncrement">The value to increase the precision by
/// before determining an appropriate <c>decimal</c> type.</param>
/// <param name="VarCharMinLengthLimit">The minimum length to use with
/// <c>varchar</c>.</param>
/// <param name="VarCharSizeFactor">The ratio between the chosen <c>varchar</c>
/// length limit and the maximum string length seen.</param>
internal record SqlTypeInferenceOptions(
    bool UseSmallInt = false,
    int IntRangeFactor = 2,
    int PrecisionIncrement = 1,
    int VarCharMinLengthLimit = 255,
    int VarCharSizeFactor = 2
);
