namespace SetTrie;

public static class SetKeyMultimapExtensions
{
    public static SetKeyMultimap<TKey, TValue> ToSetKeyMultimap<TKey, TValue>(
        this IEnumerable<(IReadOnlySet<TKey>, TValue)> source
    )
        where TKey : IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SetKeyMultimap<TKey, TValue> dict)
        {
            return new(dict);
        }

        var setKeyMultimap = new SetKeyMultimap<TKey, TValue>();

        foreach (var (set, value) in source)
        {
            setKeyMultimap.Add(set, value);
        }

        return setKeyMultimap;
    }

    public static SetKeyMultimap<TKey, TValue> ToSetKeyMultimap<TKey, TValue>(
        this IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>> source
    )
        where TKey : IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SetKeyMultimap<TKey, TValue> dict)
        {
            return new(dict);
        }

        var setKeyMultimap = new SetKeyMultimap<TKey, TValue>();

        foreach (var (set, value) in source)
        {
            setKeyMultimap.Add(set, value);
        }

        return setKeyMultimap;
    }
}
