namespace RelationNormalizer;

/// <summary>
/// A relation schema that can be easily converted to SQL.
/// </summary>
internal class RelationSchema
{
    /// <summary>
    /// The number of attributes in the relation schema.
    /// </summary>
    public int AttributeCount => Attributes.Count;

    /// <summary>
    /// The attributes in the relation schema.
    /// </summary>
    public IReadOnlyList<string> Attributes { get; private init; }

    /// <summary>
    /// The primary key of the relation schema.
    /// </summary>
    public IReadOnlySet<string> PrimaryKey { get; private init; }

    /// <summary>
    /// The unique keys (not including the primary key) of the relation schema.
    /// </summary>
    public IEnumerable<IReadOnlySet<string>> UniqueKeys { get; private init; }

    /// <summary>
    /// Constructs a complete relation schema.
    /// </summary>
    /// <param name="attributeNames">The names of the attributes.</param>
    /// <param name="primaryKey">The primary key.</param>
    /// <param name="uniqueKeys">The unique keys.</param>
    /// <exception cref="ArgumentException">
    /// One of the attributes in <c>primaryKey</c> or <c>uniqueKeys</c>
    /// does not exist in <c>attributeNames</c>.
    /// </exception>
    public RelationSchema(
        IEnumerable<string> attributeNames,
        IReadOnlySet<string> primaryKey,
        IEnumerable<IReadOnlySet<string>> uniqueKeys
    )
    {
        var validNames = attributeNames.ToHashSet();

        foreach (var attributeName in primaryKey)
        {
            if (!validNames.Contains(attributeName))
            {
                throw new ArgumentException(
                    "The primary key contains an invalid attribute.",
                    nameof(primaryKey)
                );
            }
        }

        foreach (var uniqueKey in uniqueKeys)
        {
            if (uniqueKey.SetEquals(primaryKey))
            {
                continue;
            }

            foreach (var attributeName in uniqueKey)
            {
                if (!validNames.Contains(attributeName))
                {
                    throw new ArgumentException(
                        "A unique key contains an invalid attribute.",
                        nameof(uniqueKeys)
                    );
                }
            }
        }

        Attributes = attributeNames.ToArray();
        PrimaryKey = primaryKey.ToHashSet();
        UniqueKeys = uniqueKeys.Select(value => value.ToHashSet()).ToArray();
    }
}
