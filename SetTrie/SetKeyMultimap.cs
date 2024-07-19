using System.Collections;
using SetTrie.Accessors;
using SetTrie.Utils;

namespace SetTrie;

/// <summary>
/// A multimap whose keys are sets. This collection is not thread-safe.
/// Storing set keys with several thousand elements may cause a stack overflow.
/// </summary>
/// <typeparam name="TKey">The type of the elements in the set keys.
/// Must have meaningful equality, comparison, and hashing.</typeparam>
/// <typeparam name="TValue">The type of the values associated with the
/// set keys. Must have meaningful equality and hashing.</typeparam>
public class SetKeyMultimap<TKey, TValue>
    : ICollection<KeyValuePair<IReadOnlySet<TKey>, TValue>>,
        IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>>,
        IEnumerable,
        IReadOnlyCollection<KeyValuePair<IReadOnlySet<TKey>, TValue>>
    where TKey : IComparable<TKey>
{
    public int Count => _root.Count;
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets all values associated with a given set key.
    /// </summary>
    /// <param name="key">The set key.</param>
    /// <returns>The set of all values associated with <c>key</c>.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public IReadOnlySet<TValue> this[IReadOnlySet<TKey> key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            var elements = SetUtils.SortedArrayFrom(key);
            return _root.Get(elements);
        }
    }

    /// <summary>
    /// Gets all keys in this SetKeyMultimap.
    /// </summary>
    public IEnumerable<IReadOnlySet<TKey>> Keys =>
        GetEntriesDepthFirst().Select(entry => entry.Key);

    /// <summary>
    /// Gets all values in this SetKeyMultimap.
    /// </summary>
    public IEnumerable<TValue> Values => GetValuesDepthFirst();

    /// <summary>
    /// The node at the root of the set trie,
    /// representing the empty set key (if present).
    /// </summary>
    private readonly SetTrieNode<
        TKey,
        TValue,
        HashSet<TValue>,
        SetKeyMultimapAccessor<TValue>
    > _root;

    /// <summary>
    /// The version of this SetKeyMultimap, used to track modifications
    /// and invalidate enumerators.
    /// </summary>
    private int _version = 0;

    /// <summary>
    /// Constructs a new, empty SetKeyMultimap.
    /// </summary>
    public SetKeyMultimap()
    {
        _root = new();
    }

    /// <summary>
    /// Clones a SetKeyMultimap object.
    /// </summary>
    /// <param name="other">The SetKeyMultimap to clone.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public SetKeyMultimap(SetKeyMultimap<TKey, TValue> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        _root = new(other._root);
    }

    public IEnumerator<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEnumerator() => GetEntriesDepthFirst().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        (
            (IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>>)this
        ).GetEnumerator();

    /// <summary>
    /// Gets all key-value pairs in depth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields all key-value
    /// pairs in depth-first (lexicographic) order.</returns>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEntriesDepthFirst() =>
        WrapEnumerable(_root.EnumerateDepthFirst(new()));

    /// <summary>
    /// Gets all key-value pairs in breadth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields all key-value
    /// pairs in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEntriesBreadthFirst() =>
        WrapEnumerable(_root.EnumerateBreadthFirst());

    /// <summary>
    /// Gets all values in depth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields all
    /// values in depth-first (lexicographic) order.</returns>
    public IEnumerable<TValue> GetValuesDepthFirst() =>
        WrapEnumerable(_root.EnumerateValuesDepthFirst());

    /// <summary>
    /// Gets all values in breadth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields all
    /// values in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    public IEnumerable<TValue> GetValuesBreadthFirst() =>
        WrapEnumerable(_root.EnumerateValuesBreadthFirst());

    public void CopyTo(
        KeyValuePair<IReadOnlySet<TKey>, TValue>[] array,
        int arrayIndex
    )
    {
        ArgumentNullException.ThrowIfNull(array, nameof(array));
        ArgumentOutOfRangeException.ThrowIfNegative(
            arrayIndex,
            nameof(arrayIndex)
        );

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Not enough room in the array.");
        }

        var i = arrayIndex;

        foreach (var entry in this)
        {
            array[i++] = entry;
        }
    }

    public bool Contains(KeyValuePair<IReadOnlySet<TKey>, TValue> item) =>
        item.Key is not null
        && _root.Contains(SetUtils.SortedArrayFrom(item.Key), item.Value);

    /// <summary>
    /// Checks whether a key exists.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <returns>Whether <c>key</c> exists as a key in this
    /// SetKeyMultimap.</returns>
    public bool ContainsKey(IReadOnlySet<TKey> key) =>
        key is not null && _root.ContainsKey(SetUtils.SortedArrayFrom(key));

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, contains the value
    /// associated with the specified key, if the key is found; otherwise,
    /// the default value for the type of the <c>value</c> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the <see cref="SetKeyMultimap{TKey, TValue}"/>
    /// contains an element with the specified key;
    /// otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <c>key</c> is <c>null</c>.</exception>
    public bool TryGetValue(
        IReadOnlySet<TKey> key,
        out IReadOnlySet<TValue> value
    )
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        var elements = SetUtils.SortedArrayFrom(key);
        var exists = _root.TryGetValue(elements, out var valueSet);
        value = valueSet;
        return exists;
    }

    public void Clear()
    {
        var oldCount = Count;
        _root.Clear();
        UpdateVersion(oldCount);
    }

    /// <summary>
    /// Adds a key-value pair.
    /// </summary>
    /// <param name="key">The key of the key-value pair to add.</param>
    /// <param name="value">The value of the key-value pair to add.</param>
    /// <returns>Whether any modifications occurred.</returns>
    public bool Add(IReadOnlySet<TKey> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(key);
        _root.Add(elements, 0, value);
        return UpdateVersion(oldCount);
    }

    public void Add(KeyValuePair<IReadOnlySet<TKey>, TValue> item) =>
        Add(item.Key, item.Value);

    /// <summary>
    /// Removes a key and all of its associated values.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>Whether any modifications occurred.</returns>
    public bool RemoveKey(IReadOnlySet<TKey> key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(key);
        _root.Remove(elements, 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Removes a key-value pair.
    /// </summary>
    /// <param name="key">The key of the key-value pair to remove.</param>
    /// <param name="value">The value of the key-value pair to remove.</param>
    /// <returns>Whether any modifications occurred.</returns>
    public bool Remove(IReadOnlySet<TKey> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(key);
        _root.Remove(elements, 0, value);
        return UpdateVersion(oldCount);
    }

    public bool Remove(KeyValuePair<IReadOnlySet<TKey>, TValue> item) =>
        item.Key is not null && Remove(item.Key, item.Value);

    /// <summary>
    /// Checks whether a subset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search subsets of.</param>
    /// <returns>Whether a subset of <c>set</c> exists
    /// in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSubsetOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSubsetOf(SetUtils.SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Counts the number of existing subsets of a given set.
    /// </summary>
    /// <param name="set">The set to count subsets of.</param>
    /// <returns>The number of subsets of <c>set</c>
    /// in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountSubsetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSubsetValuesOf(SetUtils.SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Removes all existing subsets of a given set.
    /// </summary>
    /// <param name="set">The set to remove subsets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveSubsetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSubsetsOf(SetUtils.SortedArrayFrom(set), 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// subset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a subset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSubsetEntriesOf(IReadOnlySet<TKey> set) =>
        GetSubsetEntriesDepthFirst(set);

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// subset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a subset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSubsetEntriesDepthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetEntriesDepthFirst(
                new(),
                SetUtils.SortedArrayFrom(set),
                0
            )
        );
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// subset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a subset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSubsetEntriesBreadthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetEntriesBreadthFirst(
                SetUtils.SortedArrayFrom(set)
            )
        );
    }

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// subset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a subset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSubsetValuesOf(IReadOnlySet<TKey> set) =>
        GetSubsetValuesDepthFirst(set);

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// subset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a subset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSubsetValuesDepthFirst(
        IReadOnlySet<TKey> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetValuesDepthFirst(
                SetUtils.SortedArrayFrom(set),
                0
            )
        );
    }

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// subset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a subset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSubsetValuesBreadthFirst(
        IReadOnlySet<TKey> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetValuesBreadthFirst(
                SetUtils.SortedArrayFrom(set)
            )
        );
    }

    /// <summary>
    /// Checks whether a superset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search supersets of.</param>
    /// <returns>Whether a superset of <c>set</c>
    /// exists in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSupersetOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSupersetOf(SetUtils.SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Counts the number of existing supersets of a given set.
    /// </summary>
    /// <param name="set">The set to count supersets of.</param>
    /// <returns>The number of supersets of <c>set</c>
    /// in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountSupersetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSupersetValuesOf(SetUtils.SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Removes all existing supersets of a given set.
    /// </summary>
    /// <param name="set">The set to remove supersets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveSupersetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSupersetsOf(SetUtils.SortedArrayFrom(set), 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// superset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a superset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSupersetEntriesOf(IReadOnlySet<TKey> set) =>
        GetSupersetEntriesDepthFirst(set);

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// superset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a superset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSupersetEntriesDepthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetEntriesDepthFirst(
                new(),
                SetUtils.SortedArrayFrom(set),
                0
            )
        );
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// superset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a superset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetSupersetEntriesBreadthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetEntriesBreadthFirst(
                SetUtils.SortedArrayFrom(set)
            )
        );
    }

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// superset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a superset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSupersetValuesOf(IReadOnlySet<TKey> set) =>
        GetSupersetValuesDepthFirst(set);

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// superset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a superset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSupersetValuesDepthFirst(
        IReadOnlySet<TKey> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetValuesDepthFirst(
                SetUtils.SortedArrayFrom(set),
                0
            )
        );
    }

    /// <summary>
    /// Gets all existing values associated with a key that is a
    /// superset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all values associated with a superset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TValue> GetSupersetValuesBreadthFirst(
        IReadOnlySet<TKey> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetValuesBreadthFirst(
                SetUtils.SortedArrayFrom(set)
            )
        );
    }

    /// <summary>
    /// Checks whether a proper subset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search proper subsets of.</param>
    /// <returns>Whether a proper subset of <c>set</c>
    /// exists in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSubsetOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSubsetOf(
            SetUtils.SortedArrayFrom(set),
            0,
            0
        );
    }

    /// <summary>
    /// Counts the number of existing proper subsets of a given set.
    /// </summary>
    /// <param name="set">The set to count proper subsets of.</param>
    /// <returns>The number of proper subsets of <c>set</c>
    /// in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountProperSubsetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        var count = _root.CountSubsetValuesOf(elements, 0);

        if (_root.ContainsKey(elements))
        {
            --count;
        }

        return count;
    }

    /// <summary>
    /// Removes all existing proper subsets of a given set.
    /// </summary>
    /// <param name="set">The set to remove proper subsets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveProperSubsetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSubsetsOf(SetUtils.SortedArrayFrom(set), 0, 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper subset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper subset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSubsetEntriesOf(IReadOnlySet<TKey> set) =>
        GetProperSubsetEntriesDepthFirst(set);

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper subset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper subset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSubsetEntriesDepthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var entry in GetSubsetEntriesDepthFirst(set))
        {
            var subset = entry.Key;

            if (subset.Count == set.Count)
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper subset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper subset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSubsetEntriesBreadthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var entry in GetSubsetEntriesBreadthFirst(set))
        {
            var subset = entry.Key;

            if (subset.Count == set.Count)
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Checks whether a proper superset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search proper supersets of.</param>
    /// <returns>Whether a proper superset of <c>set</c>
    /// exists in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSupersetOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSupersetOf(
            SetUtils.SortedArrayFrom(set),
            0,
            0
        );
    }

    /// <summary>
    /// Counts the number of existing proper supersets of a given set.
    /// </summary>
    /// <param name="set">The set to count proper supersets of.</param>
    /// <returns>The number of proper supersets of <c>set</c>
    /// in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountProperSupersetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        var count = _root.CountSupersetValuesOf(elements, 0);

        if (_root.ContainsKey(elements))
        {
            --count;
        }

        return count;
    }

    /// <summary>
    /// Removes all existing proper supersets of a given set.
    /// </summary>
    /// <param name="set">The set to remove proper supersets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveProperSupersetsOf(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSupersetsOf(SetUtils.SortedArrayFrom(set), 0, 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper superset of a given set.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper superset of
    /// <c>set</c> in this SetKeyMultimap.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSupersetEntriesOf(IReadOnlySet<TKey> set) =>
        GetProperSupersetEntriesDepthFirst(set);

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper superset of a given set in depth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper superset of
    /// <c>set</c> in this SetKeyMultimap
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSupersetEntriesDepthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var entry in GetSupersetEntriesDepthFirst(set))
        {
            var superset = entry.Key;

            if (superset.Count == set.Count)
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Gets all existing key-value pairs with a key that is a
    /// proper superset of a given set in breadth-first order.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all key-value pairs whose key is a proper superset of
    /// <c>set</c> in this SetKeyMultimap in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetProperSupersetEntriesBreadthFirst(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var entry in GetSupersetEntriesBreadthFirst(set))
        {
            var superset = entry.Key;

            if (superset.Count == set.Count)
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Checks whether a given set is the union of existing sets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetKeyMultimap contains a collection of sets
    /// with a union of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSetsWithUnion(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);

        if (_root.ContainsKey(elements))
        {
            return true;
        }

        var union = new HashSet<TKey>();

        foreach (
            var (subset, _) in _root.EnumerateSubsetEntriesBreadthFirst(
                elements
            )
        )
        {
            union.UnionWith(subset);

            if (union.Count == elements.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a given set is the intersection of existing sets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetKeyMultimap contains a collection of sets
    /// with an intersection of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSetsWithIntersection(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);

        if (_root.ContainsKey(elements))
        {
            return true;
        }

        var intersection = (HashSet<TKey>?)null;

        foreach (
            var (superset, _) in _root.EnumerateSupersetEntriesBreadthFirst(
                elements
            )
        )
        {
            if (intersection is null)
            {
                intersection = superset;
            }
            else
            {
                intersection.IntersectWith(superset);
            }

            if (intersection.Count == elements.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a given set is the union
    /// of existing proper subsets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetKeyMultimap contains a collection of
    /// proper subsets of <c>set</c> with
    /// a union of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSubsetsWithUnion(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        var union = new HashSet<TKey>();

        foreach (
            var (subset, _) in _root.EnumerateSubsetEntriesBreadthFirst(
                elements
            )
        )
        {
            if (subset.Count == elements.Length)
            {
                continue;
            }

            union.UnionWith(subset);

            if (union.Count == elements.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a given set is the intersection
    /// of existing proper supersets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetKeyMultimap contains a collection of
    /// proper supersets of <c>set</c> with
    /// an intersection of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSupersetsWithIntersection(IReadOnlySet<TKey> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        var intersection = (HashSet<TKey>?)null;

        foreach (
            var (superset, _) in _root.EnumerateSupersetEntriesBreadthFirst(
                elements
            )
        )
        {
            if (superset.Count == elements.Length)
            {
                continue;
            }

            if (intersection is null)
            {
                intersection = superset;
            }
            else
            {
                intersection.IntersectWith(superset);
            }

            if (intersection.Count == elements.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a set and maintains the invariant that all sets be
    /// minimal within this SetKeyMultimap (i.e., no existing set is a
    /// subset of another existing set).
    /// </summary>
    /// <param name="set">The set to add.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>Whether any modifications occurred.</returns>
    public bool AddWithMinimalSetsInvariant(
        IReadOnlySet<TKey> set,
        TValue value
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        int oldCount;

        if (_root.ContainsKey(elements))
        {
            oldCount = Count;
            _root.Add(elements, 0, value);
            return UpdateVersion(oldCount);
        }

        if (_root.ContainsSubsetOf(elements, 0))
        {
            return false;
        }

        oldCount = _root.Count;
        _root.RemoveSupersetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0, value);
        changed = changed || Count != oldCount;

        if (changed)
        {
            ++_version;
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Adds a set and maintains the invariant that all sets be
    /// maximal within this SetKeyMultimap (i.e., no existing set is a
    /// superset of another existing set).
    /// </summary>
    /// <param name="set">The set to add.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>Whether any modifications occurred.</returns>
    public bool AddWithMaximalSetsInvariant(
        IReadOnlySet<TKey> set,
        TValue value
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SetUtils.SortedArrayFrom(set);
        int oldCount;

        if (_root.ContainsKey(elements))
        {
            oldCount = Count;
            _root.Add(elements, 0, value);
            return UpdateVersion(oldCount);
        }

        if (_root.ContainsSupersetOf(elements, 0))
        {
            return false;
        }

        oldCount = _root.Count;
        _root.RemoveSubsetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0, value);
        changed = changed || Count != oldCount;

        if (changed)
        {
            ++_version;
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Increments the version if modifications were made.
    /// </summary>
    /// <param name="oldCount">The count prior to any mutating operation.</param>
    /// <returns>Whether modifications were made, based on <c>oldCount</c>
    /// and the current count.</returns>
    private bool UpdateVersion(int oldCount)
    {
        if (Count != oldCount)
        {
            ++_version;
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Wraps an enumerable of the key-value pairs in this SetKeyMultimap.
    /// </summary>
    /// <param name="enumerable">The enumerable to wrap.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs in thie SetKeyMultimap and is automatically
    /// invalidated when modifications are made.</returns>
    /// <exception cref="InvalidOperationException">When the enumerator is
    /// invalidated due to modifications.</exception>
    private IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > WrapEnumerable(
        IEnumerable<KeyValuePair<HashSet<TKey>, TValue>> enumerable
    )
    {
        var beginVersion = _version;

        foreach (var (set, value) in enumerable)
        {
            if (_version != beginVersion)
            {
                throw new InvalidOperationException(
                    "Collection was modified after the enumerator was instantiated."
                );
            }

            yield return new(set, value);
        }
    }

    /// <summary>
    /// Wraps an enumerable of the values in this SetKeyMultimap.
    /// </summary>
    /// <param name="enumerable">The enumerable to wrap.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// values in this SetKeyMultimap and is automatically
    /// invalidated when modifications are made.</returns>
    /// <exception cref="InvalidOperationException">When the enumerator is
    /// invalidated due to modifications.</exception>
    private IEnumerable<TValue> WrapEnumerable(IEnumerable<TValue> enumerable)
    {
        var beginVersion = _version;

        foreach (var value in enumerable)
        {
            if (_version != beginVersion)
            {
                throw new InvalidOperationException(
                    "Collection was modified after the enumerator was instantiated."
                );
            }

            yield return value;
        }
    }
}
