namespace RelationNormalizer;

/// <summary>
/// Relational schema normal forms.
/// </summary>
internal enum NormalForm
{
    /// <summary>
    /// 1NF
    /// </summary>
    First,

    /// <summary>
    /// 2NF
    /// </summary>
    Second,

    /// <summary>
    /// 3NF
    /// </summary>
    Third,

    /// <summary>
    /// BCNF
    /// </summary>
    BoyceCodd,
}

/// <summary>
/// Methods for <see cref="NormalForm"/>.
/// </summary>
internal static class NormalFormExtensions
{
    /// <summary>
    /// Gets the name of a normal form.
    /// </summary>
    /// <param name="normalForm">The normal form to get the name of.</param>
    /// <returns>The name of the normal form corresponding to <c>normalForm</c>
    /// (e.g., <c>"1NF"</c>).</returns>
    /// <exception cref="ArgumentException">
    /// <c>normalForm</c> is not a valid normal form.</exception>
    public static string Name(this NormalForm normalForm) =>
        normalForm switch
        {
            NormalForm.First => "1NF",
            NormalForm.Second => "2NF",
            NormalForm.Third => "3NF",
            NormalForm.BoyceCodd => "BCNF",
            _
                => throw new ArgumentException(
                    "Invalid normal form.",
                    nameof(normalForm)
                )
        };
}
