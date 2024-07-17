namespace SetTrie;

public static class SetFamilyExtensions
{
    public static SetFamily<T> ToSetFamily<T>(
        this IEnumerable<IReadOnlySet<T>> source
    )
        where T : IComparable<T>
    {
        if (source is SetFamily<T> sets)
        {
            return new(sets);
        }

        var setFamily = new SetFamily<T>();

        foreach (var set in source)
        {
            setFamily.Add(set);
        }

        return setFamily;
    }
}
