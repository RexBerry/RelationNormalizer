namespace SetTrie;

/// <summary>
/// A node in a set trie.
/// </summary>
/// <remarks>
/// <para>
/// A set trie is a data structure similar to a trie, with the difference
/// being that a set trie stores multisets whose elements have a total order
/// by storing the elements in sorted order.
/// See the <see href="https://doi.org/10.1007/978-3-642-40511-2_10">
/// 2013 paper by Savnik, I.</see>
/// </para>
///
/// <para>
/// This implementation is not
/// designed to store multisets, and is instead made to be compatible with
/// <see cref="IReadOnlySet{T}"/> and <see cref="HashSet{T}"/>. Internally,
/// each SetTrieNode uses a <see cref="SortedDictionary{TKey, TValue}"/>
/// to store its children.
/// </para>
///
/// <para>
/// This implementation can also be used to implement a map or multimap
/// with sets as keys.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the elements in the set keys.
/// Must have meaningful equality, comparison, and hashing.</typeparam>
/// <typeparam name="TValue">The type of values associated with
/// the set keys.</typeparam>
/// <typeparam name="TStore">The type of values internally stored in
/// the nodes. This would differ from <c>TKey</c> in a multimap.</typeparam>
/// <typeparam name="TAccessor">A class used to access the values in
/// the nodes.</typeparam>
internal sealed class SetTrieNode<TKey, TValue, TStore, TAccessor>
    where TKey : IComparable<TKey>
    where TAccessor : ISetTrieAccessor<TValue, TStore>
{
    /// <summary>
    /// A record type used when constructing sets in breadth-first enumerators.
    /// </summary>
    /// <typeparam name="T">The type of information stored
    /// (e.g., the value of a SetTrieNode, an index of the input
    /// array, etc.).</typeparam>
    /// <param name="Parent">The parent BacktrackingNode of
    /// this BacktrackingNode, or <c>null</c> if there is none.</param>
    /// <param name="Value">The value stored in this BacktrackingNode,
    /// or <c>default</c> if there is no parent.</param>
    private record BacktrackingNode<T>(
        BacktrackingNode<T>? Parent = null,
        T? Value = default
    );

    /// <summary>
    /// The number of values stored in this SetTrieNode and its children.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Whether this SetTrieNode is storing a value.
    /// </summary>
    private bool _hasValue;

    /// <summary>
    /// The number of values stored in this SetTrieNode and its children.
    /// </summary>
    private int _count;

    /// <summary>
    /// The value storage of this SetTrieNode.
    /// </summary>
    private TStore? _storage;

    /// <summary>
    /// The child SetTrieNodes (and their corresponding element values)
    /// of this SetTrieNode.
    /// </summary>
    private readonly SortedDictionary<
        TKey,
        SetTrieNode<TKey, TValue, TStore, TAccessor>
    > _children;

    // TODO: Consider using a B(+)Tree for better performance.

    /// <summary>
    /// Creates a new SetTrieNode containing no sets and with no children.
    /// </summary>
    internal SetTrieNode()
    {
        _hasValue = false;
        _count = 0;
        _storage = default;
        _children = [];
    }

    /// <summary>
    /// Clones a SetTrieNode object.
    /// </summary>
    /// <param name="other">The SetTrieNode to clone.</param>
    internal SetTrieNode(SetTrieNode<TKey, TValue, TStore, TAccessor> other)
    {
        _hasValue = other._hasValue;
        _count = other._count;
        _storage = _hasValue ? TAccessor.Clone(other._storage!) : default;
        _children = [];

        foreach (var (element, otherChild) in other._children)
        {
            _children.Add(element, new(otherChild));
        }
    }

    /// <summary>
    /// Checks whether a set key exists.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>Whether the set key exists in this SetTrieNode
    /// or its children.</returns>
    internal bool ContainsKey(ReadOnlySpan<TKey> elements)
    {
        var node = this;
        var index = 0;

        while (true)
        {
            if (index == elements.Length)
            {
                return _hasValue;
            }

            var element = elements[index];

            if (!node._children.TryGetValue(element, out var child))
            {
                return false;
            }

            node = child;
            ++index;
        }
    }

    /// <summary>
    /// Gets the value storage associated with a set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>The value storage associated with the set represented by
    /// <c>elements</c>.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    internal TStore Get(ReadOnlySpan<TKey> elements)
    {
        var node = this;
        var index = 0;

        while (true)
        {
            if (index == elements.Length)
            {
                if (!_hasValue)
                {
                    throw new KeyNotFoundException(
                        "The given key was not present."
                    );
                }

                return _storage!;
            }

            var element = elements[index];

            if (!node._children.TryGetValue(element, out var child))
            {
                throw new KeyNotFoundException(
                    "The given key was not present."
                );
            }

            node = child;
            ++index;
        }
    }

    /// <summary>
    /// Whether this SetTrieNode is equal to another SetTrieNode.
    /// </summary>
    /// <param name="other">The other SetTrieNode.</param>
    /// <returns>Whether the two SetTrieNodes have the same set keys and the
    /// same values associated with those keys.</returns>
    internal bool SetTrieEquals(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (
            _hasValue != other._hasValue
            || _count != other._count
            || _children.Count != other._children.Count
        )
        {
            return false;
        }

        if (_hasValue && !TAccessor.Equals(_storage!, other._storage!))
        {
            return false;
        }

        foreach (var (element, child) in _children)
        {
            if (!other._children.TryGetValue(element, out var otherChild))
            {
                return false;
            }

            if (!child.SetTrieEquals(otherChild))
            {
                return false;
            }
        }

        return true;
    }

    // Used to implement the ISet<T> method of the same name.
    internal bool Overlaps(SetTrieNode<TKey, TValue, TStore, TAccessor> other)
    {
        if (_hasValue && other._hasValue)
        {
            return true;
        }

        if (other._children.Count == 0)
        {
            return false;
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            if (child.Overlaps(otherChild))
            {
                return true;
            }
        }

        return false;
    }

    // Used to implement the ISet<T> method of the same name.
    internal bool IsSubsetOf(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (
            (_hasValue && !other._hasValue)
            || _count > other._count
            || _children.Count > other._children.Count
        )
        {
            return false;
        }

        foreach (var (element, child) in _children)
        {
            if (!other._children.TryGetValue(element, out var otherChild))
            {
                return false;
            }

            if (!child.IsSubsetOf(otherChild))
            {
                return false;
            }
        }

        return true;
    }

    // Used to implement the ISet<T> method of the same name.
    internal bool IsSupersetOf(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (
            (!_hasValue && other._hasValue)
            || _count < other._count
            || _children.Count < other._children.Count
        )
        {
            return false;
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                return false;
            }

            if (!child.IsSupersetOf(otherChild))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the key-value pairs stored in this SetTrieNode and its children
    /// in depth-first order.
    /// </summary>
    /// <param name="resultElements">The element values of the chain
    /// of SetTrieNodes from the root of the set trie
    /// to this SetTrieNode, representing the current set key.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs stored in this SetTrieNode and its children
    /// in depth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateDepthFirst(Stack<TKey> resultElements)
    {
        if (_hasValue)
        {
            var set = resultElements.ToHashSet();

            foreach (var value in TAccessor.Enumerate(_storage!))
            {
                yield return new(set, value);
            }
        }

        foreach (var (element, child) in _children)
        {
            resultElements.Push(element);

            foreach (var entry in child.EnumerateDepthFirst(resultElements))
            {
                yield return entry;
            }

            resultElements.Pop();
        }
    }

    /// <summary>
    /// Gets the values stored in this SetTrieNode and its children
    /// in depth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields the
    /// values stored in this SetTrieNode and its children
    /// in depth-first order.</returns>
    internal IEnumerable<TValue> EnumerateValuesDepthFirst()
    {
        if (_hasValue)
        {
            foreach (var value in TAccessor.Enumerate(_storage!))
            {
                yield return value;
            }
        }

        foreach (var (_, child) in _children)
        {
            foreach (var value in child.EnumerateValuesDepthFirst())
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// Gets the key-value pairs stored in this SetTrieNode and its children
    /// in breadth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs stored in this SetTrieNode and its children
    /// in breadth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateBreadthFirst()
    {
        var nodes =
            new Queue<(
                SetTrieNode<TKey, TValue, TStore, TAccessor>,
                BacktrackingNode<TKey>
            )>();
        nodes.Enqueue((this, new()));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();

            if (node._hasValue)
            {
                var btNode = backtrackingNode;
                var set = new HashSet<TKey>();

                while (btNode.Parent is not null)
                {
                    set.Add(btNode.Value!);
                    btNode = btNode.Parent;
                }

                foreach (var value in TAccessor.Enumerate(_storage!))
                {
                    yield return new(set, value);
                }
            }

            foreach (var (element, child) in node._children)
            {
                nodes.Enqueue((child, new(backtrackingNode, element)));
            }
        }
    }

    /// <summary>
    /// Gets the values stored in this SetTrieNode and its children
    /// in breadth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that yields the
    /// values stored in this SetTrieNode and its children
    /// in breadth-first order.</returns>
    internal IEnumerable<TValue> EnumerateValuesBreadthFirst()
    {
        var nodes = new Queue<SetTrieNode<TKey, TValue, TStore, TAccessor>>();
        nodes.Enqueue(this);

        while (nodes.Count > 0)
        {
            var node = nodes.Dequeue();

            if (node._hasValue)
            {
                foreach (var value in TAccessor.Enumerate(_storage!))
                {
                    yield return value;
                }
            }

            foreach (var (_, child) in node._children)
            {
                nodes.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Removes all values stored in this SetTrieNode
    /// and removes all children.
    /// </summary>
    public void Clear()
    {
        _hasValue = false;
        _count = 0;
        _storage = default;
        _children.Clear();
    }

    /// <summary>
    /// Removes all values stored in this SetTrieNode.
    /// </summary>
    private void RemoveValues()
    {
        if (!_hasValue)
        {
            return;
        }

        _count -= TAccessor.Count(_storage!);
        _hasValue = false;
        _storage = default;
    }

    /// <summary>
    /// Removes all children.
    /// </summary>
    private void RemoveChildren()
    {
        _count = _hasValue ? TAccessor.Count(_storage!) : 0;
        _children.Clear();
    }

    /// <summary>
    /// Adds a value to this SetTrieNode or a child if not already present.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element in the set key.</param>
    /// <param name="value">The value to add.</param>
    internal void Add(ReadOnlySpan<TKey> elements, int index, TValue value)
    {
        if (index == elements.Length)
        {
            TAccessor.Add(value, ref _storage, ref _hasValue, ref _count);
            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            child = new();
            _children.Add(element, child);
        }

        var oldCount = child._count;
        child.Add(elements, index + 1, value);
        _count += child._count - oldCount;
    }

    /// <summary>
    /// Inserts a value storage into this set trie, replacing the existing
    /// value storage of a SetTrieNode if necessary.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element in the set key.</param>
    /// <param name="storage">The value storage to insert.</param>
    internal void Set(ReadOnlySpan<TKey> elements, int index, TStore storage)
    {
        if (index == elements.Length)
        {
            if (_hasValue)
            {
                RemoveValues();
            }

            TAccessor.AddFrom(
                storage,
                ref _storage,
                ref _hasValue,
                ref _count
            );

            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            child = new();
            _children.Add(element, child);
        }

        var oldCount = child._count;
        child.Set(elements, index + 1, storage);
        _count += child._count - oldCount;
    }

    /// <summary>
    /// Removes a value from this SetTrieNode or a child.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element in the set key.</param>
    /// <param name="value">The value to remove.</param>
    internal void Remove(ReadOnlySpan<TKey> elements, int index, TValue value)
    {
        if (index == elements.Length)
        {
            if (_hasValue)
            {
                TAccessor.Remove(
                    value,
                    ref _storage,
                    ref _hasValue,
                    ref _count
                );
            }

            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            return;
        }

        var oldCount = child._count;
        child.Remove(elements, index + 1, value);
        _count -= oldCount - child._count;

        if (child._count == 0)
        {
            _children.Remove(element);
        }
    }

    /// <summary>
    /// Removes all existing values associated with a set key
    /// from this SetTrieNode or a child.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element in the set key.</param>
    internal void Remove(ReadOnlySpan<TKey> elements, int index)
    {
        if (index == elements.Length)
        {
            RemoveValues();
            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            return;
        }

        var oldCount = child._count;
        child.Remove(elements, index + 1);
        _count -= oldCount - child._count;

        if (child._count == 0)
        {
            _children.Remove(element);
        }
    }

    /// <summary>
    /// Adds all key-value pairs from another SetTrieNode and its children.
    /// </summary>
    /// <param name="other">The other SetTrieNode.</param>
    internal void AddFrom(SetTrieNode<TKey, TValue, TStore, TAccessor> other)
    {
        if (other._hasValue)
        {
            TAccessor.AddFrom(
                other._storage!,
                ref _storage,
                ref _hasValue,
                ref _count
            );
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                child = new();
                _children.Add(element, child);
            }

            var oldCount = child._count;
            child.AddFrom(otherChild);
            _count += child._count - oldCount;
        }
    }

    // Used to implement the ISet<T> method of the same name.
    // Note: the value storages of surviving nodes are unaffected.
    internal void IntersectWith(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (_hasValue && !other._hasValue)
        {
            RemoveValues();
        }

        if (other._children.Count == 0)
        {
            RemoveChildren();
            return;
        }

        var elementsToRemove = new List<TKey>();

        foreach (var (element, child) in _children)
        {
            var oldCount = child._count;

            if (other._children.TryGetValue(element, out var otherChild))
            {
                child.IntersectWith(otherChild);
            }
            else
            {
                child._count = 0;
            }

            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }
    }

    // Used to implement the ISet<T> method of the same name.
    internal void ExceptWith(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (_hasValue && other._hasValue)
        {
            RemoveValues();
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            var oldCount = child._count;
            child.ExceptWith(otherChild);
            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    // Used to implement the ISet<T> method of the same name.
    // Note: key-value pairs from the other node are added when appropriate.
    internal bool SymmetricExceptWith(
        SetTrieNode<TKey, TValue, TStore, TAccessor> other
    )
    {
        if (_count == 0)
        {
            AddFrom(other);
            return _count != 0;
        }

        var changed = false;

        if (other._hasValue)
        {
            if (_hasValue)
            {
                RemoveValues();
            }
            else
            {
                AddFrom(other);
            }

            changed = true;
        }

        if (other._children.Count == 0)
        {
            return changed;
        }

        var elementsToRemove = new List<TKey>();

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                child = new();
                _children.Add(element, child);
            }

            var oldCount = child._count;
            changed = child.SymmetricExceptWith(otherChild) || changed;
            _count += child._count - oldCount;

            if (child._count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }

        return changed;
    }

    /// <summary>
    /// Checks whether a subset key of a given set key exists.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>Whether a subset key of the given set key is stored in
    /// this SetTrieNode or its children.</returns>
    internal bool ContainsSubsetOf(ReadOnlySpan<TKey> elements, int index)
    {
        if (_hasValue)
        {
            return true;
        }

        if (index == elements.Length)
        {
            return false;
        }

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            if (child.ContainsSubsetOf(elements, i + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the number of existing values associated with subset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>The number of values associated with subset keys of
    /// the given set key stored in this SetTrieNode and its children.</returns>
    internal int CountSubsetValuesOf(ReadOnlySpan<TKey> elements, int index)
    {
        var count = 0;

        if (_hasValue)
        {
            count += TAccessor.Count(_storage!);
        }

        if (index == elements.Length)
        {
            return count;
        }

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            count += child.CountSubsetValuesOf(elements, i + 1);
        }

        return count;
    }

    /// <summary>
    /// Removes all existing values associated with subset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    internal void RemoveSubsetsOf(ReadOnlySpan<TKey> elements, int index)
    {
        if (_hasValue)
        {
            RemoveValues();
        }

        if (index == elements.Length)
        {
            return;
        }

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            var oldCount = child._count;
            child.RemoveSubsetsOf(elements, i + 1);
            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    /// <summary>
    /// Gets the key-value pairs associated with subset keys
    /// of a given set key in depth-first order.
    /// </summary>
    /// <param name="resultElements">The element values of the chain
    /// of SetTrieNodes from the root of the set trie
    /// to this SetTrieNode, representing the current set key.</param>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs associated with subset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in depth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateSubsetsDepthFirst(
        Stack<TKey> resultElements,
        TKey[] elements,
        int index
    )
    {
        if (_hasValue)
        {
            var set = resultElements.ToHashSet();

            foreach (var value in TAccessor.Enumerate(_storage!))
            {
                yield return new(set, value);
            }
        }

        if (index == elements.Length)
        {
            yield break;
        }

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            resultElements.Push(element);

            foreach (
                var entry in child.EnumerateSubsetsDepthFirst(
                    resultElements,
                    elements,
                    i + 1
                )
            )
            {
                yield return entry;
            }

            resultElements.Pop();
        }
    }

    /// <summary>
    /// Gets the values associated with subset keys
    /// of a given set key in depth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// values associated with subset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in depth-first order.</returns>
    internal IEnumerable<TValue> EnumerateSubsetValuesDepthFirst(
        TKey[] elements,
        int index
    )
    {
        if (_hasValue)
        {
            foreach (var value in TAccessor.Enumerate(_storage!))
            {
                yield return value;
            }
        }

        if (index == elements.Length)
        {
            yield break;
        }

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            foreach (
                var value in child.EnumerateSubsetValuesDepthFirst(
                    elements,
                    i + 1
                )
            )
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// Gets the key-value pairs associated with subset keys
    /// of a given set key in breadth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs associated with subset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in breadth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateSubsetsBreadthFirst(TKey[] elements)
    {
        var nodes =
            new Queue<(
                SetTrieNode<TKey, TValue, TStore, TAccessor>,
                BacktrackingNode<(TKey? element, int index)>
            )>();
        nodes.Enqueue((this, new(null, (default, 0))));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();

            if (node._hasValue)
            {
                var btNode = backtrackingNode;
                var set = new HashSet<TKey>();

                while (btNode.Parent is not null)
                {
                    set.Add(btNode.Value.element!);
                    btNode = btNode.Parent;
                }

                foreach (var value in TAccessor.Enumerate(_storage!))
                {
                    yield return new(set, value);
                }
            }

            var (_, index) = backtrackingNode.Value;

            if (index == elements.Length)
            {
                continue;
            }

            for (var i = index; i < elements.Length; ++i)
            {
                var element = elements[i];

                if (!node._children.TryGetValue(element, out var child))
                {
                    continue;
                }

                nodes.Enqueue(
                    (child, new(backtrackingNode, (element, i + 1)))
                );
            }
        }
    }

    /// <summary>
    /// Gets the values associated with subset keys
    /// of a given set key in breadth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// values associated with subset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in breadth-first order.</returns>
    internal IEnumerable<TValue> EnumerateSubsetValuesBreadthFirst(
        TKey[] elements
    )
    {
        var nodes =
            new Queue<(SetTrieNode<TKey, TValue, TStore, TAccessor>, int)>();
        nodes.Enqueue((this, 0));

        while (nodes.Count > 0)
        {
            var (node, index) = nodes.Dequeue();

            if (node._hasValue)
            {
                foreach (var value in TAccessor.Enumerate(_storage!))
                {
                    yield return value;
                }
            }

            if (index == elements.Length)
            {
                continue;
            }

            for (var i = index; i < elements.Length; ++i)
            {
                var element = elements[i];

                if (!node._children.TryGetValue(element, out var child))
                {
                    continue;
                }

                nodes.Enqueue((child, i + 1));
            }
        }
    }

    /// <summary>
    /// Checks whether a subset key of a given set key exists.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>Whether a subset key of the given set key is stored in
    /// this SetTrieNode or its children.</returns>
    internal bool ContainsSupersetOf(ReadOnlySpan<TKey> elements, int index)
    {
        if (index == elements.Length)
        {
            return _count > 0;
        }

        var nextElement = elements[index];

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;

            if (child.ContainsSupersetOf(elements, nextIndex))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the number of existing values associated with superset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>The number of values associated with superset keys of
    /// the given set key stored in this SetTrieNode and its children.</returns>
    internal int CountSupersetValuesOf(ReadOnlySpan<TKey> elements, int index)
    {
        if (index == elements.Length)
        {
            return _count;
        }

        var count = 0;
        var nextElement = elements[index];

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            count += child.CountSupersetValuesOf(elements, nextIndex);
        }

        return count;
    }

    /// <summary>
    /// Removes all existing values associated with superset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    internal void RemoveSupersetsOf(ReadOnlySpan<TKey> elements, int index)
    {
        if (index == elements.Length)
        {
            Clear();
            return;
        }

        var nextElement = elements[index];
        var elementsToRemove = new List<TKey>();

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            var oldCount = child._count;
            child.RemoveSupersetsOf(elements, nextIndex);
            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }
    }

    /// <summary>
    /// Gets the key-value pairs associated with superset keys
    /// of a given set key in depth-first order.
    /// </summary>
    /// <param name="resultElements">The element values of the chain
    /// of SetTrieNodes from the root of the set trie
    /// to this SetTrieNode, representing the current set key.</param>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs associated with superset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in depth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateSupersetsDepthFirst(
        Stack<TKey> resultElements,
        TKey[] elements,
        int index
    )
    {
        if (index == elements.Length)
        {
            foreach (var entry in EnumerateDepthFirst(resultElements))
            {
                yield return entry;
            }

            yield break;
        }

        var nextElement = elements[index];

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            resultElements.Push(element);

            foreach (
                var entry in child.EnumerateSupersetsDepthFirst(
                    resultElements,
                    elements,
                    nextIndex
                )
            )
            {
                yield return entry;
            }

            resultElements.Pop();
        }
    }

    /// <summary>
    /// Gets the values associated with superset keys
    /// of a given set key in depth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// values associated with superset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in depth-first order.</returns>
    internal IEnumerable<TValue> EnumerateSupersetValuesDepthFirst(
        TKey[] elements,
        int index
    )
    {
        if (index == elements.Length)
        {
            foreach (var value in EnumerateValuesDepthFirst())
            {
                yield return value;
            }

            yield break;
        }

        var nextElement = elements[index];

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;

            foreach (
                var value in child.EnumerateSupersetValuesDepthFirst(
                    elements,
                    nextIndex
                )
            )
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// Gets the key-value pairs associated with superset keys
    /// of a given set key in breadth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// key-value pairs associated with superset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in breadth-first order.</returns>
    internal IEnumerable<
        KeyValuePair<HashSet<TKey>, TValue>
    > EnumerateSupersetsBreadthFirst(TKey[] elements)
    {
        var nodes =
            new Queue<(
                SetTrieNode<TKey, TValue, TStore, TAccessor>,
                BacktrackingNode<(TKey? element, int index)>
            )>();
        nodes.Enqueue((this, new(null, (default, 0))));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();
            var (_, index) = backtrackingNode.Value;

            if (index == elements.Length)
            {
                if (node._hasValue)
                {
                    var btNode = backtrackingNode;
                    var set = new HashSet<TKey>();

                    while (btNode.Parent is not null)
                    {
                        set.Add(btNode.Value.element!);
                        btNode = btNode.Parent;
                    }

                    foreach (var value in TAccessor.Enumerate(_storage!))
                    {
                        yield return new(set, value);
                    }
                }

                foreach (var (element, child) in node._children)
                {
                    nodes.Enqueue(
                        (child, new(backtrackingNode, (element, index)))
                    );
                }

                continue;
            }

            var nextElement = elements[index];

            foreach (var (element, child) in node._children)
            {
                var order = element.CompareTo(nextElement);

                if (order > 0)
                {
                    break;
                }

                var nextIndex = order == 0 ? index + 1 : index;
                nodes.Enqueue(
                    (child, new(backtrackingNode, (element, nextIndex)))
                );
            }
        }
    }

    /// <summary>
    /// Gets the values associated with superset keys
    /// of a given set key in breadth-first order.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <returns>An enumerable with an enumerator that yields the
    /// values associated with superset keys
    /// of the given set key stored in this SetTrieNode
    /// and its children in breadth-first order.</returns>
    internal IEnumerable<TValue> EnumerateSupersetValuesBreadthFirst(
        TKey[] elements
    )
    {
        var nodes =
            new Queue<(SetTrieNode<TKey, TValue, TStore, TAccessor>, int)>();
        nodes.Enqueue((this, 0));

        while (nodes.Count > 0)
        {
            var (node, index) = nodes.Dequeue();

            if (index == elements.Length)
            {
                if (node._hasValue)
                {
                    foreach (var value in TAccessor.Enumerate(_storage!))
                    {
                        yield return value;
                    }
                }

                foreach (var (_, child) in node._children)
                {
                    nodes.Enqueue((child, index));
                }

                continue;
            }

            var nextElement = elements[index];

            foreach (var (element, child) in node._children)
            {
                var order = element.CompareTo(nextElement);

                if (order > 0)
                {
                    break;
                }

                var nextIndex = order == 0 ? index + 1 : index;
                nodes.Enqueue((child, nextIndex));
            }
        }
    }

    /// <summary>
    /// Checks whether a proper subset key of a given set key exists.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <param name="depth">The depth of this SetTrieNode in the set trie.</param>
    /// <returns>Whether a proper subset key of the given set key is stored in
    /// this SetTrieNode or its children.</returns>
    internal bool ContainsProperSubsetOf(
        ReadOnlySpan<TKey> elements,
        int index,
        int depth
    )
    {
        if (_hasValue && depth != elements.Length)
        {
            return true;
        }

        if (index == elements.Length)
        {
            return false;
        }

        ++depth;

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            if (child.ContainsProperSubsetOf(elements, i + 1, depth))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes all existing values associated with proper subset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <param name="depth">The depth of this SetTrieNode in the set trie.</param>
    internal void RemoveProperSubsetsOf(
        ReadOnlySpan<TKey> elements,
        int index,
        int depth
    )
    {
        if (_hasValue && depth != elements.Length)
        {
            RemoveValues();
        }

        if (index == elements.Length)
        {
            return;
        }

        ++depth;

        for (var i = index; i < elements.Length; ++i)
        {
            var element = elements[i];

            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            var oldCount = child._count;
            child.RemoveProperSubsetsOf(elements, i + 1, depth);
            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    /// <summary>
    /// Checks whether a proper superset key of a given set key exists.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <param name="depth">The depth of this SetTrieNode in the set trie.</param>
    /// <returns>Whether a proper superset key of the given set key is stored in
    /// this SetTrieNode or its children.</returns>
    internal bool ContainsProperSupersetOf(
        ReadOnlySpan<TKey> elements,
        int index,
        int depth
    )
    {
        if (index == elements.Length)
        {
            return (depth == elements.Length ? _children.Count : _count) > 0;
        }

        ++depth;
        var nextElement = elements[index];

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;

            if (child.ContainsProperSupersetOf(elements, nextIndex, depth))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes all existing values associated with proper superset keys
    /// of a given set key.
    /// </summary>
    /// <param name="elements">The elements of the set key in sorted order.</param>
    /// <param name="index">The index of the current element.</param>
    /// <param name="depth">The depth of this SetTrieNode in the set trie.</param>
    internal void RemoveProperSupersetsOf(
        ReadOnlySpan<TKey> elements,
        int index,
        int depth
    )
    {
        if (index == elements.Length)
        {
            if (depth == elements.Length)
            {
                RemoveChildren();
            }
            else
            {
                Clear();
            }

            return;
        }

        ++depth;
        var nextElement = elements[index];
        var elementsToRemove = new List<TKey>();

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            var oldCount = child._count;
            child.RemoveProperSupersetsOf(elements, nextIndex, depth);
            _count -= oldCount - child._count;

            if (child._count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }
    }
}
