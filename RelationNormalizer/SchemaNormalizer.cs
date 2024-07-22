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

        Logging.Info("Found the following transitive dependencies:");

        foreach (
            // Breadth first order is nicer for logging
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
                    // Breadth first likely to find a match quicker
                    var otherDeterminantSet in determinantSetCandidates.GetSubsetsBreadthFirst(
                        otherAttributes
                    )
                )
                {
                    if (otherDeterminantSet.IsSupersetOf(determinantSet))
                    {
                        continue;
                    }

                    var attributeName = _attributeNames[dependentAttribute];
                    Logging.Info(
                        $"    {AttributesToString(determinantSet)} -> {AttributesToString(otherDeterminantSet)} -> {{{attributeName}}}"
                    );

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

    /// <summary>
    /// Normalize the relation schema.
    /// </summary>
    /// <param name="targetNormalForm">The normal form to target.</param>
    /// <returns>The relation schemas in the
    /// normalized database schema and their normal forms.</returns>
    /// <exception cref="ArgumentException"><c>targetNormalForm</c>
    /// is not a valid normal form.</exception>
    public IEnumerable<(RelationSchema, NormalForm)> Normalize(
        NormalForm targetNormalForm
    )
    {
        CalculateMinimalDeterminantSets();

        var (_, initialNormalForm) = CreateRelationSchema(
            Enumerable.Range(0, AttributeCount).ToHashSet()
        );
        Logging.Info(
            $"The input relation schema is in {initialNormalForm.Name()}."
        );

        if (targetNormalForm is NormalForm.First)
        {
            yield return CreateRelationSchema(
                Enumerable.Range(0, AttributeCount).ToHashSet()
            );
            yield break;
        }

        var removeTransitive = targetNormalForm switch
        {
            NormalForm.First => false,
            NormalForm.Second => false,
            NormalForm.Third => true,
            NormalForm.BoyceCodd => true,
            _
                => throw new ArgumentException(
                    "Invalid normal form",
                    nameof(targetNormalForm)
                ),
        };

        var candidateKeys = CalculateCandidateKeys();

        Logging.Info("Found the following candidate keys:");

        // Breadth-first order is nicer for logging
        foreach (var candidateKey in candidateKeys.GetSetsBreadthFirst())
        {
            Logging.Info($"    {AttributesToString(candidateKey)}");
        }

        Logging.Info(
            "Found the following partial dependencies on a candidate key:"
        );

        foreach (var attribute in Enumerable.Range(0, AttributeCount))
        {
            var attributeName = _attributeNames[attribute];

            foreach (var candidateKey in candidateKeys)
            {
                if (candidateKey.Contains(attribute))
                {
                    continue;
                }

                var keyString = AttributesToString(candidateKey);

                foreach (
                    var determinantSet in _attributeMinimalDeterminantSets[
                        attribute
                    ]
                        .GetProperSubsetsBreadthFirst(candidateKey)
                )
                {
                    Logging.Info(
                        $"    {AttributesToString(determinantSet)} -> {{{attributeName}}} ; {keyString} -> {{{attributeName}}}"
                    );
                }
            }
        }

        var cover = CalculateCover(removeTransitive);

        var tables = new SetFamily<int>();

        Logging.Info("Reduced set of functional dependencies:");

        // Breadth-first order is nicer for logging
        foreach (var determinantSet in cover.GetKeysBreadthFirst())
        {
            var dependentSet = cover[determinantSet];

            Logging.Info(
                $"    {AttributesToString(determinantSet)} -> {AttributesToString(dependentSet)}"
            );

            var tableAttributes = determinantSet.ToHashSet();
            tableAttributes.UnionWith(dependentSet);
            tables.AddWithMaximalSetsInvariant(tableAttributes);
        }

        // Prevent attributes from disappearing.
        // This could occur otherwise if an attribute is not in any dependent
        // set in a non-trivial functional dependency and is not
        // is any minimal determinant set.

        var hasTableWithCandidateKey = false;

        foreach (var candidateKey in candidateKeys)
        {
            if (tables.ContainsSupersetOf(candidateKey))
            {
                hasTableWithCandidateKey = true;
                break;
            }
        }

        if (!hasTableWithCandidateKey)
        {
            tables.AddWithMaximalSetsInvariant(
                candidateKeys.GetSetsBreadthFirst().First()
            );
        }

        // Order results by descending table size
        // Looks nicer to whoever views the results
        foreach (var table in tables.GetSetsBreadthFirst().Reverse())
        {
            yield return CreateRelationSchema(table);
        }
    }

    /// <summary>
    /// Creates a relation schema and determines its normal form.
    /// </summary>
    /// <param name="relationAttributes">The attributes to include
    /// in the relation schema.</param>
    /// <returns>A relation schema and its normal form.</returns>
    /// <exception cref="IndexOutOfRangeException">
    /// An attribute in <c>relationAttributes</c> does not exist
    /// in the relation to normalize.</exception>
    internal (RelationSchema, NormalForm) CreateRelationSchema(
        IReadOnlySet<int> relationAttributes
    )
    {
        var keys = CalculateCandidateKeys(relationAttributes)
            .Where(value => value.IsSubsetOf(relationAttributes))
            .ToSetFamily();

        var primaryKey = keys.GetSetsBreadthFirst().First();
        var uniqueKeys = keys.GetSetsBreadthFirst().Skip(1);

        var is2NF = true;
        var is3NF = true;
        var isBCNF = true;

        foreach (var attribute in relationAttributes)
        {
            var attributeAsSet = (HashSet<int>)[attribute];

            foreach (
                var determinantSet in _attributeMinimalDeterminantSets[
                    attribute
                ]
            )
            {
                if (determinantSet.Contains(attribute))
                {
                    // Trivial functional dependency
                    continue;
                }

                if (!determinantSet.IsSubsetOf(relationAttributes))
                {
                    continue;
                }

                var isPrimeAttribute = keys.ContainsSupersetOf(attributeAsSet);

                // `keys` cannot contain a proper subset of `determinantSet`

                if (keys.ContainsProperSupersetOf(determinantSet))
                {
                    // Partial functional dependency on a candidate key
                    isBCNF = false;

                    if (!isPrimeAttribute)
                    {
                        is2NF = false;
                        is3NF = false;
                        break;
                    }
                }

                if (!keys.Contains(determinantSet))
                {
                    // Determinant set is not a superkey
                    isBCNF = false;

                    if (!isPrimeAttribute)
                    {
                        is3NF = false;
                    }
                }
            }
        }

        var relationSchema = new RelationSchema(
            relationAttributes
                .OrderBy(value => value)
                .Select(value => _attributeNames[value]),
            primaryKey
                .OrderBy(value => value)
                .Select(value => _attributeNames[value]),
            uniqueKeys.Select(value =>
                value
                    .OrderBy(value => value)
                    .Select(value => _attributeNames[value])
            )
        );

        var normalForm = isBCNF
            ? NormalForm.BoyceCodd
            : is3NF
                ? NormalForm.Third
                : is2NF
                    ? NormalForm.Second
                    : NormalForm.First;

        return (relationSchema, normalForm);
    }

    /// <summary>
    /// Converts a set of attributes to a string.
    /// </summary>
    /// <param name="attributes">The attributes.</param>
    /// <returns>A human-readable string of the set of attributes.</returns>
    private string AttributesToString(IReadOnlySet<int> attributes) =>
        $"{{{string.Join(", ", attributes.OrderBy(value => value).Select(value => _attributeNames[value]))}}}";
}
