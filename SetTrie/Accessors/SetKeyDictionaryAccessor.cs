using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SetTrie.Utils;

namespace SetTrie.Accessors;

internal class SetKeyDictionaryAccessor<T> : ISetTrieAccessor<T, T>
{
    public static void Add(
        T value,
        ref T? storage,
        ref bool hasValue,
        ref int count
    )
    {
        if (hasValue)
        {
            return;
        }

        storage = value;
        hasValue = true;
        count = 1;
    }

    public static void AddFrom(
        T source,
        ref T? storage,
        ref bool hasValue,
        ref int count
    )
    {
        if (hasValue)
        {
            return;
        }

        storage = source;
        hasValue = true;
        count = 1;
    }

    public static void Remove(
        T value,
        ref T? storage,
        ref bool hasValue,
        ref int count
    )
    {
        _ = value;
        storage = default;
        hasValue = false;
        count = 0;
    }

    public static bool Contains(T storage, T value) =>
        storage?.Equals(value) ?? value is null;

    public static IEnumerable<T> Enumerate(T storage)
    {
        yield return storage;
    }
}
