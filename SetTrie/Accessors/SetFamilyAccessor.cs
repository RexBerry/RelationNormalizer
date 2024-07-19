using SetTrie.Utils;

namespace SetTrie.Accessors;

internal class SetFamilyAccessor : ISetTrieAccessor<Nothing, Nothing>
{
    public static void Add(
        Nothing value,
        ref Nothing storage,
        ref bool hasValue,
        ref int count
    )
    {
        _ = value;
        _ = storage;
        hasValue = true;
        count = 1;
    }

    public static void AddFrom(
        Nothing source,
        ref Nothing storage,
        ref bool hasValue,
        ref int count
    )
    {
        _ = source;
        _ = storage;
        hasValue = true;
        count = 1;
    }

    public static void Remove(
        Nothing value,
        ref Nothing storage,
        ref bool hasValue,
        ref int count
    )
    {
        _ = value;
        _ = storage;
        hasValue = false;
        count = 0;
    }

    public static bool Contains(Nothing storage, Nothing value) => true;

    public static IEnumerable<Nothing> Enumerate(Nothing storage)
    {
        yield return default;
    }

    public static bool Equals(Nothing a, Nothing b) => true;
}
