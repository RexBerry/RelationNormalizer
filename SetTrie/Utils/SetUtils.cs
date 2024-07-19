namespace SetTrie.Utils;

internal static class SetUtils
{
    /// <summary>
    /// Creates a sorted array from a set.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the set.</typeparam>
    /// <param name="set">The set to copy elements from.</param>
    /// <returns>A sorted array with the same elements as <c>set</c>.</returns>
    public static T[] SortedArrayFrom<T>(IReadOnlySet<T> set)
    {
        var elements = set.ToArray();
        Array.Sort(elements);
        return elements;
    }
}
