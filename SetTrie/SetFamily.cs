using System.Collections;

namespace SetTrie;

public sealed class SetFamily<T>
    : ICollection<IReadOnlySet<T>>,
        IEnumerable<IReadOnlySet<T>>,
        IReadOnlyCollection<IReadOnlySet<T>>,
        ISet<IReadOnlySet<T>>,
        IReadOnlySet<IReadOnlySet<T>>
    where T : IComparable<T>
{
    public int Count => _root.Count;
    public bool IsReadOnly => false;

    private readonly SetTrieNode<T> _root;
    private int _version = 0;

    public SetFamily()
    {
        _root = new();
    }

    public SetFamily(SetFamily<T> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        _root = new(other._root);
    }

    public IEnumerator<IReadOnlySet<T>> GetEnumerator() =>
        EnumerableWrapper(_root.EnumerateDepthFirst(new())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable<IReadOnlySet<T>>)this).GetEnumerator();

    public IEnumerable<IReadOnlySet<T>> GetSetsDepthFirst() =>
        EnumerableWrapper(_root.EnumerateDepthFirst(new()));

    public IEnumerable<IReadOnlySet<T>> GetSetsBreadthFirst() =>
        EnumerableWrapper(_root.EnumerateBreadthFirst());

    public void CopyTo(IReadOnlySet<T>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array, nameof(array));

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arrayIndex),
                "Array index must be nonnegative."
            );
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Not enough room in the array.");
        }

        var i = arrayIndex;

        foreach (var set in this)
        {
            array[i++] = set;
        }
    }

    public bool Contains(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);
        return _root.Contains(elements, 0);
    }

    public bool SetEquals(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        return _root.SetEquals(SetFamilyFrom(other)._root);
    }

    public bool Overlaps(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        if (other is SetFamily<T> sets)
        {
            return _root.Overlaps(sets._root);
        }

        foreach (var set in other)
        {
            if (Contains(set))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSubsetOf(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        return _root.IsSubsetOf(SetFamilyFrom(other)._root);
    }

    public bool IsSupersetOf(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        if (other is SetFamily<T> sets)
        {
            return _root.IsSupersetOf(sets._root);
        }

        foreach (var set in other)
        {
            if (!Contains(set))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsProperSubsetOf(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var sets = SetFamilyFrom(other);
        return Count < sets.Count && _root.IsSubsetOf(sets._root);
    }

    public bool IsProperSupersetOf(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var sets = SetFamilyFrom(other);
        return Count > sets.Count && _root.IsSupersetOf(sets._root);
    }

    public void Clear()
    {
        var oldCount = Count;
        _root.Clear();
        UpdateVersion(oldCount);
    }

    public bool Add(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        var elements = SortedArrayFrom(set);
        _root.Add(elements, 0);
        return UpdateVersion(oldCount);
    }

    void ICollection<IReadOnlySet<T>>.Add(IReadOnlySet<T> set) => Add(set);

    public bool Remove(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        var elements = SortedArrayFrom(set);
        _root.Remove(elements, 0);
        return UpdateVersion(oldCount);
    }

    public void UnionWith(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var oldCount = Count;

        if (other is SetFamily<T> sets)
        {
            _root.UnionWith(sets._root);
        }
        else
        {
            foreach (var set in other)
            {
                var elements = SortedArrayFrom(set);
                _root.Add(elements, 0);
            }
        }

        UpdateVersion(oldCount);
    }

    public void IntersectWith(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var oldCount = Count;
        _root.IntersectWith(SetFamilyFrom(other)._root);
        UpdateVersion(oldCount);
    }

    public void ExceptWith(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var oldCount = Count;

        if (other is SetFamily<T> sets)
        {
            _root.ExceptWith(sets._root);
        }
        else
        {
            foreach (var set in other)
            {
                Remove(set);
            }
        }

        UpdateVersion(oldCount);
    }

    public void SymmetricExceptWith(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var changed = false;

        if (other is SetFamily<T> sets)
        {
            changed = _root.SymmetricExceptWith(sets._root);
        }
        else
        {
            foreach (var set in other)
            {
                var elements = SortedArrayFrom(set);

                if (_root.Contains(elements, 0))
                {
                    _root.Remove(elements, 0);
                }
                else
                {
                    _root.Add(elements, 0);
                }

                changed = true;
            }
        }

        if (changed)
        {
            ++_version;
        }
    }

    public bool ContainsSubsetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSubsetOf(SortedArrayFrom(set), 0);
    }

    public int CountSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSubsetsOf(SortedArrayFrom(set), 0);
    }

    public void RemoveSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSubsetsOf(SortedArrayFrom(set), 0);
        UpdateVersion(oldCount);
    }

    public IEnumerable<IReadOnlySet<T>> GetSubsetsOf(IReadOnlySet<T> set) =>
        GetSubsetsDepthFirst(set);

    public IEnumerable<IReadOnlySet<T>> GetSubsetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return EnumerableWrapper(
            _root.EnumerateSubsetsDepthFirst(new(), SortedArrayFrom(set), 0)
        );
    }

    public IEnumerable<IReadOnlySet<T>> GetSubsetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return EnumerableWrapper(
            _root.EnumerateSubsetsBreadthFirst(SortedArrayFrom(set))
        );
    }

    public bool ContainsSupersetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSupersetOf(SortedArrayFrom(set), 0);
    }

    public int CountSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSupersetsOf(SortedArrayFrom(set), 0);
    }

    public void RemoveSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSupersetsOf(SortedArrayFrom(set), 0);
        UpdateVersion(oldCount);
    }

    public IEnumerable<IReadOnlySet<T>> GetSupersetsOf(IReadOnlySet<T> set) =>
        GetSupersetsDepthFirst(set);

    public IEnumerable<IReadOnlySet<T>> GetSupersetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return EnumerableWrapper(
            _root.EnumerateSupersetsDepthFirst(new(), SortedArrayFrom(set), 0)
        );
    }

    public IEnumerable<IReadOnlySet<T>> GetSupersetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return EnumerableWrapper(
            _root.EnumerateSupersetsBreadthFirst(SortedArrayFrom(set))
        );
    }

    public bool ContainsProperSubsetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSubsetOf(SortedArrayFrom(set), 0, 0);
    }

    public int CountProperSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);
        var count = _root.CountSubsetsOf(elements, 0);

        if (_root.Contains(elements, 0))
        {
            --count;
        }

        return count;
    }

    public void RemoveProperSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSubsetsOf(SortedArrayFrom(set), 0, 0);
        UpdateVersion(oldCount);
    }

    public IEnumerable<IReadOnlySet<T>> GetProperSubsetsOf(
        IReadOnlySet<T> set
    ) => GetProperSubsetsDepthFirst(set);

    public IEnumerable<IReadOnlySet<T>> GetProperSubsetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var subset in GetSubsetsDepthFirst(set))
        {
            if (subset.Count == set.Count)
            {
                continue;
            }

            yield return subset;
        }
    }

    public IEnumerable<IReadOnlySet<T>> GetProperSubsetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var subset in GetSubsetsBreadthFirst(set))
        {
            if (subset.Count == set.Count)
            {
                continue;
            }

            yield return subset;
        }
    }

    public bool ContainsProperSupersetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSupersetOf(SortedArrayFrom(set), 0, 0);
    }

    public int CountProperSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);
        var count = _root.CountSupersetsOf(elements, 0);

        if (_root.Contains(elements, 0))
        {
            --count;
        }

        return count;
    }

    public void RemoveProperSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSupersetsOf(SortedArrayFrom(set), 0, 0);
        UpdateVersion(oldCount);
    }

    public IEnumerable<IReadOnlySet<T>> GetProperSupersetsOf(
        IReadOnlySet<T> set
    ) => GetProperSupersetsDepthFirst(set);

    public IEnumerable<IReadOnlySet<T>> GetProperSupersetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var superset in GetSupersetsDepthFirst(set))
        {
            if (superset.Count == set.Count)
            {
                continue;
            }

            yield return superset;
        }
    }

    public IEnumerable<IReadOnlySet<T>> GetProperSupersetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        foreach (var superset in GetSupersetsBreadthFirst(set))
        {
            if (superset.Count == set.Count)
            {
                continue;
            }

            yield return superset;
        }
    }

    public bool ContainsSetsWithUnion(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (_root.Contains(elements, 0))
        {
            return true;
        }

        var union = new HashSet<T>();

        foreach (var subset in _root.EnumerateSubsetsBreadthFirst(elements))
        {
            union.UnionWith(subset);

            if (union.Count == elements.Length)
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsSetsWithIntersection(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (_root.Contains(elements, 0))
        {
            return true;
        }

        var intersection = (HashSet<T>?)null;

        foreach (
            var superset in _root.EnumerateSupersetsBreadthFirst(elements)
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

    public bool ContainsProperSubsetsWithUnion(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);
        var union = new HashSet<T>();

        foreach (var subset in _root.EnumerateSubsetsBreadthFirst(elements))
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

    public bool ContainsProperSupersetsWithIntersection(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);
        var intersection = (HashSet<T>?)null;

        foreach (
            var superset in _root.EnumerateSupersetsBreadthFirst(elements)
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

    public void AddWithMinimalSetsInvariant(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (_root.Contains(elements, 0) || _root.ContainsSubsetOf(elements, 0))
        {
            return;
        }

        var oldCount = _root.Count;
        _root.RemoveSupersetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0);
        changed = changed || Count != oldCount;

        if (changed)
        {
            ++_version;
        }
    }

    public void AddWithMaximalSetsInvariant(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (
            _root.Contains(elements, 0)
            || _root.ContainsSupersetOf(elements, 0)
        )
        {
            return;
        }

        var oldCount = _root.Count;
        _root.RemoveSubsetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0);
        changed = changed || Count != oldCount;

        if (changed)
        {
            ++_version;
        }
    }

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

    private IEnumerable<IReadOnlySet<T>> EnumerableWrapper(
        IEnumerable<IReadOnlySet<T>> enumerable
    )
    {
        var beginVersion = _version;

        foreach (var set in enumerable)
        {
            if (_version != beginVersion)
            {
                throw new InvalidOperationException(
                    "Collection was modified after the enumerator was instantiated."
                );
            }

            yield return set;
        }
    }

    private static SetFamily<T> SetFamilyFrom(
        IEnumerable<IReadOnlySet<T>> source
    ) => source is SetFamily<T> sets ? sets : source.ToSetFamily();

    private static T[] SortedArrayFrom(IReadOnlySet<T> set)
    {
        var elements = (T[])[.. set];
        Array.Sort(elements);
        return elements;
    }
}
