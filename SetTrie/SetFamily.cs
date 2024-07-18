using System.Collections;

namespace SetTrie;

/// <summary>
/// A family of sets. This collection is not thread-safe.
/// Storing sets with several thousand elements may cause a stack overflow.
/// </summary>
/// <typeparam name="T">The type of the elements in the stored sets.
/// Must have meaningful equality, comparison, and hashing.</typeparam>
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

    /// <summary>
    /// The node at the root of the set trie,
    /// representing the empty set (if present).
    /// </summary>
    private readonly SetTrieNode<T> _root;
    /// <summary>
    /// The version of this SetFamily, used to track modifications
    /// and invalidate enumerators.
    /// </summary>
    private int _version = 0;

    /// <summary>
    /// Creates a new, empty SetFamily.
    /// </summary>
    public SetFamily()
    {
        _root = new();
    }

    /// <summary>
    /// Clones a SetFamily object.
    /// </summary>
    /// <param name="other">The SetFamily to clone.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public SetFamily(SetFamily<T> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        _root = new(other._root);
    }

    public IEnumerator<IReadOnlySet<T>> GetEnumerator() =>
        WrapEnumerable(_root.EnumerateDepthFirst(new())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable<IReadOnlySet<T>>)this).GetEnumerator();

    /// <summary>
    /// Gets the sets in this SetFamily in depth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that
    /// yields sets in depth-first (lexicographic) order.</returns>
    public IEnumerable<IReadOnlySet<T>> GetSetsDepthFirst() =>
        WrapEnumerable(_root.EnumerateDepthFirst(new()));

    /// <summary>
    /// Gets the sets in this SetFamily in breadth-first order.
    /// </summary>
    /// <returns>An enumerable with an enumerator that
    /// yields sets in breadth-first
    /// (sorted by size, then lexicographically) order.</returns>
    public IEnumerable<IReadOnlySet<T>> GetSetsBreadthFirst() =>
        WrapEnumerable(_root.EnumerateBreadthFirst());

    public void CopyTo(IReadOnlySet<T>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array, nameof(array));
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex, nameof(arrayIndex));

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
        if (set is null)
        {
            return false;
        }

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
        if (set is null)
        {
            return false;
        }

        var oldCount = Count;
        var elements = SortedArrayFrom(set);
        _root.Add(elements, 0);
        return UpdateVersion(oldCount);
    }

    void ICollection<IReadOnlySet<T>>.Add(IReadOnlySet<T> set) => Add(set);

    public bool Remove(IReadOnlySet<T> set)
    {
        if (set is null)
        {
            return false;
        }

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

    /// <summary>
    /// Checks whether a subset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search subsets of.</param>
    /// <returns>Whether a subset of <c>set</c> exists
    /// in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSubsetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSubsetOf(SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Counts the number of existing subsets of a given set.
    /// </summary>
    /// <param name="set">The set to count subsets of.</param>
    /// <returns>The number of subsets of <c>set</c>
    /// in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSubsetsOf(SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Removes all existing subsets of a given set.
    /// </summary>
    /// <param name="set">The set to remove subsets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSubsetsOf(SortedArrayFrom(set), 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing subsets of a given set.
    /// </summary>
    /// <param name="set">The set to get subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all subsets of <c>set</c> in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSubsetsOf(IReadOnlySet<T> set) =>
        GetSubsetsDepthFirst(set);

    /// <summary>
    /// Gets all existing subsets of a given set
    /// in depth-first order.
    /// </summary>
    /// <param name="set">The set to get subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all subsets of <c>set</c> in this SetFamily
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSubsetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetsDepthFirst(new(), SortedArrayFrom(set), 0)
        );
    }

    /// <summary>
    /// Gets all existing subsets of a given set
    /// in breadth-first order.
    /// </summary>
    /// <param name="set">The set to get subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all subsets of <c>set</c> in this SetFamily
    /// in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSubsetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSubsetsBreadthFirst(SortedArrayFrom(set))
        );
    }

    /// <summary>
    /// Checks whether a superset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search supersets of.</param>
    /// <returns>Whether a superset of <c>set</c>
    /// exists in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsSupersetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsSupersetOf(SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Counts the number of existing supersets of a given set.
    /// </summary>
    /// <param name="set">The set to count supersets of.</param>
    /// <returns>The number of supersets of <c>set</c>
    /// in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CountSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.CountSupersetsOf(SortedArrayFrom(set), 0);
    }

    /// <summary>
    /// Removes all existing supersets of a given set.
    /// </summary>
    /// <param name="set">The set to remove supersets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveSupersetsOf(SortedArrayFrom(set), 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing supersets of a given set.
    /// </summary>
    /// <param name="set">The set to get supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all supersets of <c>set</c> in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSupersetsOf(IReadOnlySet<T> set) =>
        GetSupersetsDepthFirst(set);

    /// <summary>
    /// Gets all existing supersets of a given set
    /// in depth-first order.
    /// </summary>
    /// <param name="set">The set to get supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all supersets of <c>set</c> in this SetFamily
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSupersetsDepthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetsDepthFirst(new(), SortedArrayFrom(set), 0)
        );
    }

    /// <summary>
    /// Gets all existing supersets of a given set
    /// in breadth-first order.
    /// </summary>
    /// <param name="set">The set to get supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all supersets of <c>set</c> in this SetFamily
    /// in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetSupersetsBreadthFirst(
        IReadOnlySet<T> set
    )
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return WrapEnumerable(
            _root.EnumerateSupersetsBreadthFirst(SortedArrayFrom(set))
        );
    }

    /// <summary>
    /// Checks whether a proper subset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search proper subsets of.</param>
    /// <returns>Whether a proper subset of <c>set</c>
    /// exists in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSubsetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSubsetOf(SortedArrayFrom(set), 0, 0);
    }

    /// <summary>
    /// Counts the number of existing proper subsets of a given set.
    /// </summary>
    /// <param name="set">The set to count proper subsets of.</param>
    /// <returns>The number of proper subsets of <c>set</c>
    /// in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Removes all existing proper subsets of a given set.
    /// </summary>
    /// <param name="set">The set to remove proper subsets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveProperSubsetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSubsetsOf(SortedArrayFrom(set), 0, 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing proper subsets of a given set.
    /// </summary>
    /// <param name="set">The set to get proper subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper subsets of <c>set</c> in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetProperSubsetsOf(
        IReadOnlySet<T> set
    ) => GetProperSubsetsDepthFirst(set);

    /// <summary>
    /// Gets all existing proper subsets of a given set
    /// in depth-first order.
    /// </summary>
    /// <param name="set">The set to get proper subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper subsets of <c>set</c> in this SetFamily
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Gets all existing proper subsets of a given set
    /// in breadth-first order.
    /// </summary>
    /// <param name="set">The set to get proper subsets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper subsets of <c>set</c> in this SetFamily
    /// in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Checks whether a proper superset of a given set exists.
    /// </summary>
    /// <param name="set">The set to search proper supersets of.</param>
    /// <returns>Whether a proper superset of <c>set</c>
    /// exists in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool ContainsProperSupersetOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        return _root.ContainsProperSupersetOf(SortedArrayFrom(set), 0, 0);
    }

    /// <summary>
    /// Counts the number of existing proper supersets of a given set.
    /// </summary>
    /// <param name="set">The set to count proper supersets of.</param>
    /// <returns>The number of proper supersets of <c>set</c>
    /// in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Removes all existing proper supersets of a given set.
    /// </summary>
    /// <param name="set">The set to remove proper supersets of.</param>
    /// <returns>Whether any modifications occurred.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveProperSupersetsOf(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var oldCount = Count;
        _root.RemoveProperSupersetsOf(SortedArrayFrom(set), 0, 0);
        return UpdateVersion(oldCount);
    }

    /// <summary>
    /// Gets all existing proper supersets of a given set.
    /// </summary>
    /// <param name="set">The set to get proper supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper supersets of <c>set</c> in this SetFamily.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IReadOnlySet<T>> GetProperSupersetsOf(
        IReadOnlySet<T> set
    ) => GetProperSupersetsDepthFirst(set);

    /// <summary>
    /// Gets all existing proper supersets of a given set
    /// in depth-first order.
    /// </summary>
    /// <param name="set">The set to get proper supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper supersets of <c>set</c> in this SetFamily
    /// in depth-first (lexicographic) order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Gets all existing proper supersets of a given set
    /// in breadth-first order.
    /// </summary>
    /// <param name="set">The set to get proper supersets of.</param>
    /// <returns>An enumerable with an enumerator that
    /// yields all proper supersets of <c>set</c> in this SetFamily
    /// in breadth-first (sorted by size, then lexicographically)
    /// order.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Checks whether a given set is the union of existing sets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetFamily contains a collection of sets
    /// with a union of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Checks whether a given set is the intersection of existing sets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetFamily contains a collection of sets
    /// with an intersection of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Checks whether a given set is the union
    /// of existing proper subsets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetFamily contains a collection of
    /// proper subsets of <c>set</c> with
    /// a union of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Checks whether a given set is the intersection
    /// of existing proper supersets.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Whether this SetFamily contains a collection of
    /// proper supersets of <c>set</c> with
    /// an intersection of <c>set</c>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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

    /// <summary>
    /// Adds a set and maintains the invariant that all sets be
    /// minimal within this SetFamily (i.e., no existing set is a
    /// subset of another existing set).
    /// </summary>
    /// <param name="set">The set to add.</param>
    /// <returns>Whether any modifications occurred</returns>
    public bool AddWithMinimalSetsInvariant(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (_root.Contains(elements, 0) || _root.ContainsSubsetOf(elements, 0))
        {
            return false;
        }

        var oldCount = _root.Count;
        _root.RemoveSupersetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0);
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
    /// maximal within this SetFamily (i.e., no existing set is a
    /// superset of another existing set).
    /// </summary>
    /// <param name="set">The set to add.</param>
    /// <returns>Whether any modifications occurred</returns>
    public bool AddWithMaximalSetsInvariant(IReadOnlySet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set, nameof(set));

        var elements = SortedArrayFrom(set);

        if (
            _root.Contains(elements, 0)
            || _root.ContainsSupersetOf(elements, 0)
        )
        {
            return false;
        }

        var oldCount = _root.Count;
        _root.RemoveSubsetsOf(elements, 0);
        var changed = Count != oldCount;
        _root.Add(elements, 0);
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
    /// Gets the SetFamily of all minimal sets from the unions
    /// of all pairs of sets in the Cartesian product
    /// of this SetFamily and another family of sets.
    /// </summary>
    /// <param name="other">The other family of sets.</param>
    /// <returns>A SetFamily constructed by iterating every pair of sets
    /// with one set from this SetFamily and the other set from <c>other</c>
    /// and adding the union of the two sets to the resulting SetFamily
    /// while maintaining the minimal sets invariant (i.e., such that no set
    /// in the result is a subset of another set in the result).</returns>
    public SetFamily<T> UnionPairsOfSetsAndKeepMinimalSets(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var sets = SetFamilyFrom(other);
        var result = new SetFamily<T>();

        var largeFamily = sets.Count > Count ? sets : this;
        var smallFamily = sets.Count <= Count ? sets : this;
        var setsInSmallFamily = smallFamily._root.EnumerateBreadthFirst().ToArray();

        foreach (var set in largeFamily._root.EnumerateBreadthFirst())
        {
            if (result.Contains(set) || result.ContainsSubsetOf(set))
            {
                continue;
            }

            foreach (var otherSet in setsInSmallFamily)
            {
                var largeSet = set.Count > otherSet.Count ? set : otherSet;
                var smallSet = set.Count <= otherSet.Count ? set : otherSet;
                var union = new HashSet<T>(largeSet);
                union.UnionWith(smallSet);
                result.AddWithMinimalSetsInvariant(union);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the SetFamily of all maximal sets from the unions
    /// of all pairs of sets in the Cartesian product
    /// of this SetFamily and another family of sets.
    /// </summary>
    /// <param name="other">The other family of sets.</param>
    /// <returns>A SetFamily constructed by iterating every pair of sets
    /// with one set from this SetFamily and the other set from <c>other</c>
    /// and adding the union of the two sets to the resulting SetFamily
    /// while maintaining the maximal sets invariant (i.e., such that no set
    /// in the result is a superset of another set in the result).</returns>
    public SetFamily<T> UnionPairsOfSetsAndKeepMaximalSets(IEnumerable<IReadOnlySet<T>> other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var sets = SetFamilyFrom(other);
        var result = new SetFamily<T>();

        var largeFamily = sets.Count > Count ? sets : this;
        var smallFamily = sets.Count <= Count ? sets : this;
        var setsInSmallFamily = smallFamily._root.EnumerateBreadthFirst().ToArray();

        foreach (var set in largeFamily._root.EnumerateBreadthFirst())
        {
            foreach (var otherSet in setsInSmallFamily)
            {
                var largeSet = set.Count > otherSet.Count ? set : otherSet;
                var smallSet = set.Count <= otherSet.Count ? set : otherSet;
                var union = new HashSet<T>(largeSet);
                union.UnionWith(smallSet);
                result.AddWithMaximalSetsInvariant(union);
            }
        }

        return result;
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
    /// Wraps an enumerable of this SetFamily.
    /// </summary>
    /// <param name="enumerable">The enumerable to wrap.</param>
    /// <returns>An enumerable with an enumerator that is automatically
    /// invalidated when modifications are made.</returns>
    /// <exception cref="InvalidOperationException">When the enumerator is
    /// invalidated due to modifications.</exception>
    private IEnumerable<IReadOnlySet<T>> WrapEnumerable(
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

    /// <summary>
    /// Given a collection of sets,
    /// creates a new SetFamily object if necessary.
    /// </summary>
    /// <param name="source">The collection to copy elements from.</param>
    /// <returns>A new SetFamily object if <c>source</c> is not a SetFamily,
    /// otherwise <c>source</c>.</returns>
    private static SetFamily<T> SetFamilyFrom(
        IEnumerable<IReadOnlySet<T>> source
    ) => source is SetFamily<T> sets ? sets : source.ToSetFamily();

    /// <summary>
    /// Creates a sorted array from a set.
    /// </summary>
    /// <param name="set">The set to copy elements from.</param>
    /// <returns>A sorted array with the same elements as <c>set</c>.</returns>
    private static T[] SortedArrayFrom(IReadOnlySet<T> set)
    {
        var elements = set.ToArray();
        Array.Sort(elements);
        return elements;
    }
}
