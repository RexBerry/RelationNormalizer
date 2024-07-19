namespace SetTrie;

/// <summary>
/// An interface to be implemented by classes used to access
/// values in a <see cref="SetTrieNode{TKey, TValue, TStore, TAccessor}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the values conceptually stored in the
/// set trie.</typeparam>
/// <typeparam name="TStore">The type of the values actually stored in the
/// SetTrieNodes.</typeparam>
internal interface ISetTrieAccessor<TValue, TStore>
{
    /// <summary>
    /// Adds a value to a SetTrieNode.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <param name="storage">The value storage of the SetTrieNode.</param>
    /// <param name="hasValue">Whether the SetTrieNode is currently
    /// storing a value.</param>
    /// <param name="count">The number of values stored in the SetTrieNode
    /// and its children.</param>
    static abstract void Add(
        TValue value,
        ref TStore? storage,
        ref bool hasValue,
        ref int count
    );

    /// <summary>
    /// Adds values from the value storage of one SetTrieNode
    /// to the value storage of another SetTrieNode.
    /// </summary>
    /// <param name="source">The value storage to add values from.</param>
    /// <param name="storage">The value storage to add values to.</param>
    /// <param name="hasValue">Whether the SetTrieNode is currently
    /// storing a value.</param>
    /// <param name="count">The number of values stored in the SetTrieNode
    /// and its children.</param>
    static abstract void AddFrom(
        TStore source,
        ref TStore? storage,
        ref bool hasValue,
        ref int count
    );

    /// <summary>
    /// Removes a value from a SetTrieNode.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <param name="storage">The value storage of the SetTrieNode.</param>
    /// <param name="hasValue">Whether the SetTrieNode is currently
    /// storing a value. This will always initially be <c>true</c> when
    /// this method is called.</param>
    /// <param name="count">The number of values stored in the SetTrieNode
    /// and its children.</param>
    static abstract void Remove(
        TValue value,
        ref TStore? storage,
        ref bool hasValue,
        ref int count
    );

    /// <summary>
    /// Checks whether a SetTrieNode is storing a value.
    /// </summary>
    /// <param name="storage">The value storage of the SetTrieNode.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>Whether <c>storage</c> contains <c>value</c>.</returns>
    static abstract bool Contains(TStore storage, TValue value);

    /// <summary>
    /// Enumerates the values stored in a SetTrieNode.
    /// </summary>
    /// <param name="storage">The value storage of the SetTrieNode.</param>
    /// <returns>An enumerable with an enumerator that yields all
    /// values stored in <c>storage</c>.</returns>
    static abstract IEnumerable<TValue> Enumerate(TStore storage);

    /// <summary>
    /// Clones the value storage of a SetTrieNode.
    /// </summary>
    /// <param name="storage">The value storage to clone.</param>
    /// <returns>A clone of <c>storage</c>.</returns>
    static virtual TStore Clone(TStore storage) => storage;

    /// <summary>
    /// Checks if two SetTrieNode value storages are equal.
    /// </summary>
    /// <param name="a">The first value storage.</param>
    /// <param name="b">The second value storage.</param>
    /// <returns>Whether <c>a</c> and <c>b</c> store the same values.</returns>
    static virtual bool Equals(TStore a, TStore b) =>
        a?.Equals(b) ?? b is null;

    /// <summary>
    /// Counts the number of values stored in the value storage
    /// of a SetTrieNode.
    /// </summary>
    /// <param name="storage">The value storage.</param>
    /// <returns>The number of values stored in <c>storage</c>.</returns>
    static virtual int Count(TStore storage) => 1;
}
