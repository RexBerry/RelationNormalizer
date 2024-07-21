namespace RelationNormalizer.Utils;

/// <summary>
/// Utility extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
internal static class EnumerableUtils
{
    /// <summary>
    /// Adds an index to an enumerable.
    /// </summary>
    /// <typeparam name="T">The type enumerated from the enumerable.</typeparam>
    /// <param name="source">The enumerable to add an index to.</param>
    /// <returns>An enumerable that enumerates index-value pairs
    /// from <c>source</c>.</returns>
    public static IEnumerable<(int, T)> WithIndex<T>(
        this IEnumerable<T> source
    )
    {
        var i = 0;

        foreach (var value in source)
        {
            yield return (i++, value);
        }
    }
}
