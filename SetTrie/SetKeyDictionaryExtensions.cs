namespace SetTrie;

public static class SetKeyDictionaryExtensions
{
    public static SetKeyDictionary<TKey, TValue> ToSetKeyDictionary<
        TKey,
        TValue
    >(this IEnumerable<(IReadOnlySet<TKey>, TValue)> source)
        where TKey : IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SetKeyDictionary<TKey, TValue> dict)
        {
            return new(dict);
        }

        var setKeyDict = new SetKeyDictionary<TKey, TValue>();

        foreach (var (set, value) in source)
        {
            setKeyDict[set] = value;
        }

        return setKeyDict;
    }

    public static SetKeyDictionary<TKey, TValue> ToSetKeyDictionary<
        TKey,
        TValue
    >(this IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>> source)
        where TKey : IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SetKeyDictionary<TKey, TValue> dict)
        {
            return new(dict);
        }

        var setKeyDict = new SetKeyDictionary<TKey, TValue>();

        foreach (var (set, value) in source)
        {
            setKeyDict[set] = value;
        }

        return setKeyDict;
    }
}
