namespace SetTrie;

internal sealed class SetTrieNode<T>
    where T : IComparable<T>
{
    private record BacktrackingNode<TValue>(
        BacktrackingNode<TValue>? Parent = null,
        TValue? Value = default
    );

    public bool Last { get; private set; }
    public int Count { get; private set; }

    private readonly SortedDictionary<T, SetTrieNode<T>> _children;

    internal SetTrieNode()
    {
        Last = false;
        Count = 0;
        _children = [];
    }

    internal SetTrieNode(SetTrieNode<T> other)
    {
        Last = other.Last;
        Count = other.Count;
        _children = [];

        foreach (var (element, otherChild) in other._children)
        {
            _children.Add(element, new(otherChild));
        }
    }

    internal bool Contains(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            return Last;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            return false;
        }

        return child.Contains(elements, index + 1);
    }

    internal bool SetEquals(SetTrieNode<T> other)
    {
        if (
            Last != other.Last
            || Count != other.Count
            || _children.Count != other._children.Count
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

            if (!child.SetEquals(otherChild))
            {
                return false;
            }
        }

        return true;
    }

    internal bool Overlaps(SetTrieNode<T> other)
    {
        if (Last && other.Last)
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

    internal bool IsSubsetOf(SetTrieNode<T> other)
    {
        if (
            (Last && !other.Last)
            || Count > other.Count
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

    internal bool IsSupersetOf(SetTrieNode<T> other)
    {
        if (
            (!Last && other.Last)
            || Count < other.Count
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

    internal IEnumerable<HashSet<T>> EnumerateDepthFirst(
        Stack<T> resultElements
    )
    {
        if (Last)
        {
            yield return resultElements.ToHashSet();
        }

        foreach (var (element, child) in _children)
        {
            resultElements.Push(element);

            foreach (var set in child.EnumerateDepthFirst(resultElements))
            {
                yield return set;
            }

            resultElements.Pop();
        }
    }

    internal IEnumerable<HashSet<T>> EnumerateBreadthFirst()
    {
        var nodes = new Queue<(SetTrieNode<T>, BacktrackingNode<T>)>();
        nodes.Enqueue((this, new()));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();

            if (node.Last)
            {
                var btNode = backtrackingNode;
                var set = new HashSet<T>();

                while (btNode.Parent is not null)
                {
                    set.Add(btNode.Value!);
                    btNode = btNode.Parent;
                }

                yield return set;
            }

            foreach (var (element, child) in node._children)
            {
                nodes.Enqueue((child, new(backtrackingNode, element)));
            }
        }
    }

    public void Clear()
    {
        _children.Clear();
        Count = 0;
    }

    internal void Add(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            if (!Last)
            {
                Last = true;
                ++Count;
            }

            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            child = new();
            _children.Add(element, child);
        }

        var oldCount = child.Count;
        child.Add(elements, index + 1);
        Count += child.Count - oldCount;
    }

    internal void Remove(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            if (Last)
            {
                Last = false;
                --Count;
            }

            return;
        }

        var element = elements[index];

        if (!_children.TryGetValue(element, out var child))
        {
            return;
        }

        var oldCount = child.Count;
        child.Remove(elements, index + 1);
        Count -= oldCount - child.Count;

        if (child.Count == 0)
        {
            _children.Remove(element);
        }
    }

    internal void UnionWith(SetTrieNode<T> other)
    {
        if (!Last && other.Last)
        {
            Last = true;
            ++Count;
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                child = new();
                _children.Add(element, child);
            }

            var oldCount = child.Count;
            child.UnionWith(otherChild);
            Count += child.Count - oldCount;
        }
    }

    internal void IntersectWith(SetTrieNode<T> other)
    {
        if (Last && !other.Last)
        {
            Last = false;
            --Count;
        }

        if (other._children.Count == 0)
        {
            _children.Clear();
            Count = Last ? 1 : 0;
            return;
        }

        var elementsToRemove = new List<T>();

        foreach (var (element, child) in _children)
        {
            var oldCount = child.Count;

            if (other._children.TryGetValue(element, out var otherChild))
            {
                child.IntersectWith(otherChild);
            }
            else
            {
                child.Count = 0;
            }

            Count -= oldCount - child.Count;

            if (child.Count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }
    }

    internal void ExceptWith(SetTrieNode<T> other)
    {
        if (Last && other.Last)
        {
            Last = false;
            --Count;
        }

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                continue;
            }

            var oldCount = child.Count;
            child.ExceptWith(otherChild);
            Count -= oldCount - child.Count;

            if (child.Count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    internal bool SymmetricExceptWith(SetTrieNode<T> other)
    {
        if (Count == 0)
        {
            UnionWith(other);
            return Count != 0;
        }

        var changed = false;

        if (other.Last)
        {
            if (Last)
            {
                Last = false;
                --Count;
            }
            else
            {
                Last = true;
                ++Count;
            }

            changed = true;
        }

        if (other._children.Count == 0)
        {
            return changed;
        }

        var elementsToRemove = new List<T>();

        foreach (var (element, otherChild) in other._children)
        {
            if (!_children.TryGetValue(element, out var child))
            {
                child = new();
                _children.Add(element, child);
            }

            var oldCount = child.Count;
            changed = child.SymmetricExceptWith(otherChild) || changed;
            Count += child.Count - oldCount;

            if (child.Count == 0)
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

    internal bool ContainsSubsetOf(ReadOnlySpan<T> elements, int index)
    {
        if (Last)
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

    internal int CountSubsetsOf(ReadOnlySpan<T> elements, int index)
    {
        var count = 0;

        if (Last)
        {
            ++count;
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

            count += child.CountSubsetsOf(elements, i + 1);
        }

        return count;
    }

    internal void RemoveSubsetsOf(ReadOnlySpan<T> elements, int index)
    {
        if (Last)
        {
            Last = false;
            --Count;
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

            var oldCount = child.Count;
            child.RemoveSubsetsOf(elements, i + 1);
            Count -= oldCount - child.Count;

            if (child.Count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    internal IEnumerable<HashSet<T>> EnumerateSubsetsDepthFirst(
        Stack<T> resultElements,
        T[] elements,
        int index
    )
    {
        if (Last)
        {
            yield return resultElements.ToHashSet();
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
                var set in child.EnumerateSubsetsDepthFirst(
                    resultElements,
                    elements,
                    i + 1
                )
            )
            {
                yield return set;
            }

            resultElements.Pop();
        }
    }

    internal IEnumerable<HashSet<T>> EnumerateSubsetsBreadthFirst(T[] elements)
    {
        var nodes =
            new Queue<(
                SetTrieNode<T>,
                BacktrackingNode<(T? element, int index)>
            )>();
        nodes.Enqueue((this, new(null, (default, 0))));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();

            if (node.Last)
            {
                var btNode = backtrackingNode;
                var set = new HashSet<T>();

                while (btNode.Parent is not null)
                {
                    set.Add(btNode.Value.element!);
                    btNode = btNode.Parent;
                }

                yield return set;
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
                    (child, new(backtrackingNode, (element, index)))
                );
            }
        }
    }

    internal bool ContainsSupersetOf(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            return Count > 0;
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

    internal int CountSupersetsOf(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            return Count;
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
            count += child.CountSupersetsOf(elements, nextIndex);
        }

        return count;
    }

    internal void RemoveSupersetsOf(ReadOnlySpan<T> elements, int index)
    {
        if (index == elements.Length)
        {
            Clear();
            return;
        }

        var nextElement = elements[index];
        var elementsToRemove = new List<T>();

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            var oldCount = child.Count;
            child.RemoveSupersetsOf(elements, nextIndex);
            Count -= oldCount - child.Count;

            if (child.Count == 0)
            {
                elementsToRemove.Add(element);
            }
        }

        foreach (var element in elementsToRemove)
        {
            _children.Remove(element);
        }
    }

    internal IEnumerable<HashSet<T>> EnumerateSupersetsDepthFirst(
        Stack<T> resultElements,
        T[] elements,
        int index
    )
    {
        if (index == elements.Length)
        {
            foreach (var set in EnumerateDepthFirst(resultElements))
            {
                yield return set;
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
                var set in child.EnumerateSupersetsDepthFirst(
                    resultElements,
                    elements,
                    nextIndex
                )
            )
            {
                yield return set;
            }

            resultElements.Pop();
        }
    }

    internal IEnumerable<HashSet<T>> EnumerateSupersetsBreadthFirst(
        T[] elements
    )
    {
        var nodes =
            new Queue<(
                SetTrieNode<T>,
                BacktrackingNode<(T? element, int index)>
            )>();
        nodes.Enqueue((this, new(null, (default, 0))));

        while (nodes.Count > 0)
        {
            var (node, backtrackingNode) = nodes.Dequeue();
            var (_, index) = backtrackingNode.Value;

            if (index == elements.Length)
            {
                if (node.Last)
                {
                    var btNode = backtrackingNode;
                    var set = new HashSet<T>();

                    while (btNode.Parent is not null)
                    {
                        set.Add(btNode.Value.element!);
                        btNode = btNode.Parent;
                    }

                    yield return set;
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

    internal bool ContainsProperSubsetOf(
        ReadOnlySpan<T> elements,
        int index,
        int depth
    )
    {
        if (Last && depth != elements.Length)
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

    internal void RemoveProperSubsetsOf(
        ReadOnlySpan<T> elements,
        int index,
        int depth
    )
    {
        if (Last && depth != elements.Length)
        {
            Last = false;
            --Count;
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

            var oldCount = child.Count;
            child.RemoveProperSubsetsOf(elements, i + 1, depth);
            Count -= oldCount - child.Count;

            if (child.Count == 0)
            {
                _children.Remove(element);
            }
        }
    }

    internal bool ContainsProperSupersetOf(
        ReadOnlySpan<T> elements,
        int index,
        int depth
    )
    {
        if (index == elements.Length)
        {
            return Count > (depth == elements.Length ? 1 : 0);
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

    internal void RemoveProperSupersetsOf(
        ReadOnlySpan<T> elements,
        int index,
        int depth
    )
    {
        if (index == elements.Length)
        {
            var restoreLast = Last && depth == elements.Length;
            Clear();

            if (restoreLast)
            {
                Last = true;
                ++Count;
            }

            return;
        }

        ++depth;
        var nextElement = elements[index];
        var elementsToRemove = new List<T>();

        foreach (var (element, child) in _children)
        {
            var order = element.CompareTo(nextElement);

            if (order > 0)
            {
                break;
            }

            var nextIndex = order == 0 ? index + 1 : index;
            var oldCount = child.Count;
            child.RemoveProperSupersetsOf(elements, nextIndex, depth);
            Count -= oldCount - child.Count;

            if (child.Count == 0)
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
