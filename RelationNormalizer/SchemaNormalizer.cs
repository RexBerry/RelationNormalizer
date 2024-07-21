using RelationNormalizer.Utils;
using SetTrie;

namespace RelationNormalizer;

/// <summary>
/// Normalizes relation schemas.
/// </summary>
internal class SchemaNormalizer
{
    /// <summary>
    /// The number of attributes in the relation to normalize.
    /// </summary>
    public int AttributeCount => _attributeNames.Length;

    /// <summary>
    /// A mapping from attribute indexes to attribute names.
    /// </summary>
    private readonly string[] _attributeNames;

    /// <summary>
    /// A mapping from attribute names to attribute indexes.
    /// </summary>
    /// <remarks>
    /// Attribute indexes are used because ints use less memory
    /// than strings, making various data structures consume
    /// less memory.
    /// </remarks>
    private readonly Dictionary<string, int> _attributeIndexes;

    /// <summary>
    /// The provided determinant sets of each attribute.
    /// </summary>
    private readonly SetFamily<int>[] _attributeDeterminantSets;

    /// <summary>
    /// The calculated minimal determinant sets of each attribute.
    /// </summary>
    private readonly SetFamily<int>[] _attributeMinimalDeterminantSets;

    /// <summary>
    /// Creates a new schema normalizer from the given attribute names.
    /// </summary>
    /// <param name="attributeNames">The names of the attributes
    /// in the relation to normalize.</param>
    public SchemaNormalizer(IEnumerable<string> attributeNames)
    {
        _attributeNames = [.. attributeNames];
        _attributeIndexes = _attributeNames
            .Select((value, index) => (value, index))
            .ToDictionary();
        _attributeDeterminantSets =
        [
            .. _attributeNames.Select(_ => new SetFamily<int>())
        ];
        _attributeMinimalDeterminantSets =
        [
            .. _attributeDeterminantSets.Select(_ => new SetFamily<int>())
        ];
    }

    /// <summary>
    /// Adds a functional dependency.
    /// </summary>
    /// <param name="determinantSet">The attributes on the
    /// LHS of the functional dependency.</param>
    /// <param name="dependentSet">The attributes on the
    /// RHS of the functional dependency.</param>
    /// <exception cref="IndexOutOfRangeException">One of the attributes
    /// in <c>determinantSet</c> or <c>dependentSet</c> does not exist in
    /// the relation to normalize.</exception>
    public void AddFunctionalDependency(
        IReadOnlySet<string> determinantSet,
        IReadOnlySet<string> dependentSet
    )
    {
        HashSet<int> determinantAttributes =
        [
            .. determinantSet.Select(name => _attributeIndexes[name])
        ];

        foreach (var dependentAttributeName in dependentSet)
        {
            var dependentAttribute = _attributeIndexes[dependentAttributeName];

            if (determinantAttributes.Contains(dependentAttribute))
            {
                continue;
            }

            _attributeDeterminantSets[dependentAttribute]
                .Add(determinantAttributes);
        }
    }

    /// <summary>
    /// Adds a unique key.
    /// </summary>
    /// <param name="primeAttributes">The attributes in the unique key.</param>
    /// <exception cref="IndexOutOfRangeException">One of the attributes
    /// in <c>primeAttributes</c> does not exist in
    /// the relation to normalize.</exception>
    public void AddUniqueKey(IReadOnlySet<string> primeAttributes)
    {
        var keyAttributes = primeAttributes
            .Select(value => _attributeIndexes[value])
            .ToHashSet();

        for (var i = 0; i < _attributeDeterminantSets.Length; ++i)
        {
            if (keyAttributes.Contains(i))
            {
                continue;
            }

            _attributeDeterminantSets[i].Add(keyAttributes);
        }
    }

    /// <summary>
    /// Calculates and stores the minimal determinant sets.
    /// </summary>
    private void CalculateMinimalDeterminantSets()
    {
        foreach (var determinantSets in _attributeMinimalDeterminantSets)
        {
            determinantSets.Clear();
        }

        var todo = new SetFamily<int>();

        foreach (
            var (
                attribute,
                determinantSets
            ) in _attributeMinimalDeterminantSets.WithIndex()
        )
        {
            todo.Clear();

            foreach (
                var determinantSet in _attributeDeterminantSets[attribute]
            )
            {
                if (determinantSet.Contains(attribute))
                {
                    // Trivial functional dependency
                    continue;
                }

                todo.AddWithMinimalSetsInvariant(determinantSet);
            }

            while (todo.Count > 0)
            {
                var determinantSet = todo.GetSetsBreadthFirst().First();
                todo.Remove(determinantSet);

                // `determinantSet` will always get added
                determinantSets.AddWithMinimalSetsInvariant(determinantSet);

                // Iterate functional dependencies by replacing a determinant
                // attribute with one of its determinant sets
                foreach (var otherAttribute in determinantSet)
                {
                    foreach (
                        var otherDeterminantSet in _attributeDeterminantSets[
                            otherAttribute
                        ]
                    )
                    {
                        if (otherDeterminantSet.Contains(attribute))
                        {
                            continue;
                        }

                        var derivedDeterminantSet = determinantSet.ToHashSet();
                        derivedDeterminantSet.Remove(otherAttribute);
                        derivedDeterminantSet.UnionWith(otherDeterminantSet);

                        if (
                            determinantSets.ContainsSubsetOf(
                                derivedDeterminantSet
                            )
                        )
                        {
                            continue;
                        }

                        todo.AddWithMinimalSetsInvariant(
                            derivedDeterminantSet
                        );
                    }
                }
            }

            determinantSets.Add((HashSet<int>)[attribute]);
        }
    }

    /// <summary>
    /// Calculates the candidate keys.
    /// </summary>
    /// <returns>The set of all candidate keys.</returns>
    /// <exception cref="InvalidOperationException">
    /// There are no attributes.</exception>
    private SetFamily<int> CalculateCandidateKeys() =>
        CalculateCandidateKeys(Enumerable.Range(0, AttributeCount));

    /// <summary>
    /// Calculates the candidate keys of a set of attributes.
    /// </summary>
    /// <param name="attributes">The attributes to calculate candidate
    /// keys of.</param>
    /// <returns>The set of all candidate keys of <c>attributes</c>.
    /// Candidate keys may include attributes that are not part
    /// of <c>attributes</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// There are no attributes in <c>attributes</c>.</exception>
    /// <exception cref="IndexOutOfRangeException">
    /// One of attributes in <c>attributes</c> does not exist in the
    /// relation to normalize.</exception>
    private SetFamily<int> CalculateCandidateKeys(IEnumerable<int> attributes)
    {
        var attributeEnumerator = attributes.GetEnumerator();

        if (!attributeEnumerator.MoveNext())
        {
            throw new InvalidOperationException(
                "Can't calculate candidate keys of a relation with no attributes."
            );
        }

        // Get all determinant sets of the first attribute
        var candidateKeys = new SetFamily<int>(
            _attributeMinimalDeterminantSets[attributeEnumerator.Current]
        );

        while (attributeEnumerator.MoveNext())
        {
            // Ensure the keys determine all previous attributes
            // and the current attribute
            candidateKeys = candidateKeys.UnionPairsOfSetsAndKeepMinimalSets(
                _attributeMinimalDeterminantSets[attributeEnumerator.Current]
            );
        }

        return candidateKeys;
    }

    /// <summary>
    /// Gets a cover of the functional dependencies.
    /// </summary>
    /// <param name="removeTransitiveDependencyRedundancy">Whether to remove
    /// redundancy resulting from transitive functional dependencies.</param>
    /// <returns>A set of functional dependencies the logically implies
    /// all functional dependencies in the relation to normalize.
    /// All redundancy from partial functional dependencies is removed.
    /// Optionally, all redundancy from transitive functional dependneices
    /// is also removed to form the canonical cover.</returns>
    private SetKeyMultimap<int, int> CalculateCover(
        bool removeTransitiveDependencyRedundancy
    )
    {
        var functionalDependencies = new SetKeyMultimap<int, int>();

        foreach (
            var (
                attribute,
                determinantSets
            ) in _attributeMinimalDeterminantSets.WithIndex()
        )
        {
            foreach (var determinantSet in determinantSets)
            {
                if (
                    determinantSet.Count == 1
                    && determinantSet.First() == attribute
                )
                {
                    // Trivial functional dependency
                    continue;
                }

                functionalDependencies.Add(determinantSet, attribute);
            }
        }

        if (!removeTransitiveDependencyRedundancy)
        {
            return functionalDependencies;
        }

        // Get the canonical cover

        var attributeRemainingDeterminantSets =
            _attributeMinimalDeterminantSets
                .Select(value => new SetFamily<int>(value))
                .ToArray();
        var result = new SetKeyMultimap<int, int>();

        foreach (
            var determinantSet in functionalDependencies.GetKeysBreadthFirst()
        )
        {
            var dependentSet = functionalDependencies[determinantSet]
                .ToHashSet();
            var sortedDependentAttributes = dependentSet.ToList();
            sortedDependentAttributes.Sort();

            foreach (var dependentAttribute in sortedDependentAttributes)
            {
                var otherAttributes = dependentSet.ToHashSet();
                otherAttributes.Remove(dependentAttribute);
                otherAttributes.UnionWith(determinantSet);

                var determinantSetCandidates =
                    attributeRemainingDeterminantSets[dependentAttribute];

                // This will run for at most two iterations.
                // The only determinant set that could be a superset of `determinant`
                // is `determinant` itself, since all determinant sets are minimal.
                foreach (
                    var otherDeterminantSet in determinantSetCandidates.GetSubsetsBreadthFirst(
                        otherAttributes
                    )
                )
                {
                    if (otherDeterminantSet.IsSupersetOf(determinantSet))
                    {
                        continue;
                    }

                    // Transitive dependency
                    dependentSet.Remove(dependentAttribute);
                    determinantSetCandidates.Remove(determinantSet);
                    break;
                }
            }

            foreach (var dependentIndex in dependentSet)
            {
                result.Add(determinantSet, dependentIndex);
            }
        }

        return result;
    }

    // Temporary method for debugging
    public void PrintInfo()
    {
        CalculateMinimalDeterminantSets();
        var candidateKeys = CalculateCandidateKeys();

        foreach (
            var (
                index,
                determinants
            ) in _attributeMinimalDeterminantSets.WithIndex()
        )
        {
            var attributeName = _attributeNames[index];

            foreach (var determinant in determinants)
            {
                var determinantNames = determinant.Select(value =>
                    _attributeNames[value]
                );
                Console.WriteLine(
                    $"{string.Join(", ", determinantNames)} -> {attributeName}"
                );
            }
        }
        Console.WriteLine();

        foreach (var candidateKey in candidateKeys)
        {
            var attributeNames = candidateKey.Select(value =>
                _attributeNames[value]
            );
            Console.WriteLine(string.Join(", ", attributeNames));
        }
        Console.WriteLine();

        var cover = CalculateCover(true);

        foreach (var determinant in cover.Keys)
        {
            var dependent = cover[determinant];
            var determinantNames = determinant.Select(value =>
                _attributeNames[value]
            );
            var dependentNames = dependent.Select(value =>
                _attributeNames[value]
            );
            Console.WriteLine(
                $"{string.Join(", ", determinantNames)} -> {string.Join(", ", dependentNames)}"
            );
        }
        Console.WriteLine();

        cover = CalculateCover(false);

        foreach (var determinant in cover.Keys)
        {
            var dependent = cover[determinant];
            var determinantNames = determinant.Select(value =>
                _attributeNames[value]
            );
            var dependentNames = dependent.Select(value =>
                _attributeNames[value]
            );
            Console.WriteLine(
                $"{string.Join(", ", determinantNames)} -> {string.Join(", ", dependentNames)}"
            );
        }
        Console.WriteLine();
    }
}
