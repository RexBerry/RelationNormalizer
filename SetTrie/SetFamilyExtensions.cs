namespace SetTrie;

/// <summary>
/// Extension methods for <see cref="SetFamily{T}"/>.
/// </summary>
public static class SetFamilyExtensions
{
    /// <summary>
    /// Creates a <see cref="SetFamily{T}"/>
    /// from an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements
    /// of <c>source</c>.</typeparam>
    /// <param name="source">The <see cref="IEnumerable{T}"/>
    /// to create a <see cref="SetFamily{T}"/> from.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    /// <c>source</c> is <c>null</c>.</exception>
    public static SetFamily<TSource> ToSetFamily<TSource>(
        this IEnumerable<IReadOnlySet<TSource>> source
    )
        where TSource : IComparable<TSource>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SetFamily<TSource> sets)
        {
            return new(sets);
        }

        var setFamily = new SetFamily<TSource>();

        foreach (var set in source)
        {
            setFamily.Add(set);
        }

        return setFamily;
    }
}
