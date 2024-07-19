namespace SetTrie.Accessors;

internal class SetKeyMultimapAccessor<T> : ISetTrieAccessor<T, HashSet<T>>
{
    public static void Add(
        T value,
        ref HashSet<T>? storage,
        ref bool hasValue,
        ref int count
    )
    {
        if (hasValue)
        {
            if (storage!.Add(value))
            {
                ++count;
            }
        }
        else
        {
            storage = [value];
            hasValue = true;
            ++count;
        }
    }

    public static void AddFrom(
        HashSet<T> source,
        ref HashSet<T>? storage,
        ref bool hasValue,
        ref int count
    )
    {
        if (!hasValue)
        {
            storage = [];
            hasValue = true;
        }

        var oldCount = storage!.Count;
        storage.UnionWith(source);
        count += storage.Count - oldCount;
    }

    public static void Remove(
        T value,
        ref HashSet<T>? storage,
        ref bool hasValue,
        ref int count
    )
    {
        if (!hasValue)
        {
            return;
        }

        if (!storage!.Remove(value))
        {
            return;
        }

        --count;

        if (storage.Count == 0)
        {
            hasValue = false;
            storage = default;
        }
    }

    public static bool Contains(HashSet<T> storage, T value) =>
        storage.Contains(value);

    public static IEnumerable<T> Enumerate(HashSet<T> storage)
    {
        foreach (var value in storage)
        {
            yield return value;
        }
    }

    public static HashSet<T> Clone(HashSet<T> storage) => new(storage);

    public static bool Equals(HashSet<T> a, HashSet<T> b) => a.SetEquals(b);

    public static int Count(HashSet<T> storage) => storage.Count;
}
