using System.Collections;
using SetTrie.Accessors;
using SetTrie.Utils;

namespace SetTrie;

public class SetKeyDictionary<TKey, TValue>
    : ICollection<KeyValuePair<IReadOnlySet<TKey>, TValue>>,
        IDictionary<IReadOnlySet<TKey>, TValue>,
        IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>>,
        IReadOnlyCollection<KeyValuePair<IReadOnlySet<TKey>, TValue>>,
        IReadOnlyDictionary<IReadOnlySet<TKey>, TValue>
    where TKey : IComparable<TKey>
{
    public int Count => _root.Count;
    public bool IsReadOnly => false;

    public TValue this[IReadOnlySet<TKey> key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return _root.Get(SetUtils.SortedArrayFrom(key));
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            var elements = SetUtils.SortedArrayFrom(key);
            _root.Set(elements, 0, value);
            ++_version;
        }
    }

    IEnumerable<IReadOnlySet<TKey>> IReadOnlyDictionary<
        IReadOnlySet<TKey>,
        TValue
    >.Keys => this.Select(entry => entry.Key);

    ICollection<IReadOnlySet<TKey>> IDictionary<
        IReadOnlySet<TKey>,
        TValue
    >.Keys => this.Select(entry => entry.Key).ToArray();

    IEnumerable<TValue> IReadOnlyDictionary<
        IReadOnlySet<TKey>,
        TValue
    >.Values => this.Select(entry => entry.Value);

    ICollection<TValue> IDictionary<IReadOnlySet<TKey>, TValue>.Values =>
        this.Select(entry => entry.Value).ToArray();

    /// <summary>
    /// The node at the root of the set trie,
    /// representing the empty set key (if present).
    /// </summary>
    private readonly SetTrieNode<
        TKey,
        TValue,
        TValue,
        SetKeyDictionaryAccessor<TValue>
    > _root;

    /// <summary>
    /// The version of this SetKeyDictionary, used to track modifications
    /// and invalidate enumerators.
    /// </summary>
    private int _version = 0;

    public SetKeyDictionary()
    {
        _root = new();
    }

    public SetKeyDictionary(SetKeyDictionary<TKey, TValue> other)
    {
        _root = new(other._root);
    }

    public IEnumerator<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEnumerator() => GetEntriesDepthFirst().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        (
            (IEnumerable<KeyValuePair<IReadOnlySet<TKey>, TValue>>)this
        ).GetEnumerator();

    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEntriesDepthFirst() =>
        WrapEnumerable(_root.EnumerateDepthFirst(new()));

    public IEnumerable<
        KeyValuePair<IReadOnlySet<TKey>, TValue>
    > GetEntriesBreadthFirst() =>
        WrapEnumerable(_root.EnumerateBreadthFirst());

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

    public bool ContainsKey(IReadOnlySet<TKey> key) =>
        key is not null && _root.ContainsKey(SetUtils.SortedArrayFrom(key));

    public bool TryGetValue(IReadOnlySet<TKey> key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var elements = SetUtils.SortedArrayFrom(key);
        return _root.TryGetValue(elements, out value);
    }

    public void Clear()
    {
        var oldCount = Count;
        _root.Clear();
        UpdateVersion(oldCount);
    }

    public void Add(IReadOnlySet<TKey> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(key);
        _root.Add(elements, 0, value);

        if (!UpdateVersion(oldCount))
        {
            throw new ArgumentException(
                "An item with the same key has already been added."
            );
        }
    }

    public void Add(KeyValuePair<IReadOnlySet<TKey>, TValue> item) =>
        Add(item.Key, item.Value);

    public bool Remove(IReadOnlySet<TKey> key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(key);
        _root.Remove(elements, 0);
        return UpdateVersion(oldCount);
    }

    public bool Remove(KeyValuePair<IReadOnlySet<TKey>, TValue> item)
    {
        var (set, value) = item;

        if (set is null)
        {
            return false;
        }

        var oldCount = Count;
        var elements = SetUtils.SortedArrayFrom(set);
        _root.Remove(elements, 0, value);
        return UpdateVersion(oldCount);
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
