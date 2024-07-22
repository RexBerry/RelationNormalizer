using System.Linq;
using System.Text;
using RelationNormalizer.Parsers;
using RelationNormalizer.SqlTypes;
using RelationNormalizer.Utils;
using SetTrie;

namespace RelationNormalizer;

internal class Program
{
    private static int Main(string[] args)
    {
        Logging.LogLevel = LogLevel.Info;

        // Parse arguments

        var positionalArgs = new List<string>();
        var namedArgs = new Dictionary<string, string>()
        {
            { "--nf", "BCNF" }
        };
        var flagArgs = new Dictionary<string, bool>()
        {
            { "-h", false },
            { "--help", false },
        };

        if (!ParseArgs(args, positionalArgs, namedArgs, flagArgs, 1, 1))
        {
            ShowHelp();
            return 1;
        }

        if (flagArgs["-h"] || flagArgs["--help"])
        {
            ShowHelp();
            return 0;
        }

        const NormalForm BadNormalForm = (NormalForm)(-1);

        var targetNormalForm = namedArgs["--nf"].ToUpper() switch
        {
            "1NF" => NormalForm.First,
            "2NF" => NormalForm.Second,
            "3NF" => NormalForm.Third,
            "BCNF" => NormalForm.BoyceCodd,
            "4NF" => NormalForm.Fourth,
            _ => BadNormalForm,
        };

        if (targetNormalForm is BadNormalForm)
        {
            Console.WriteLine("Error: Invalid normal form.");
            return 1;
        }

        // Parse CSV file

        var csvFilename = positionalArgs[0];
        var table = ReadCsvFile(csvFilename);
        var rowCount = table.Count;

        if (rowCount == 0)
        {
            Console.WriteLine("Error: The table is empty.");
            Environment.Exit(1);
        }

        if (rowCount <= 1)
        {
            Console.WriteLine(
                "Error: The table has no rows after the row of attribute names."
            );
            Environment.Exit(1);
        }

        var columnCount = table[0].Length;

        // Get column names

        var columnNames = table[0]
            .Select(value =>
                value.Normalize(System.Text.NormalizationForm.FormKC)
            )
            .ToArray();

        if (columnNames.ToHashSet().Count != columnNames.Length)
        {
            Console.WriteLine("Error: Duplicate column names.");
            Environment.Exit(1);
        }

        if (columnNames.Any(value => value.Contains(',')))
        {
            Console.WriteLine("Error: Column names cannot contain commas.");
            Environment.Exit(1);
        }

        // Infer column types

        var columnTypes = new SqlType[columnNames.Length];

        for (var column = 0; column < columnCount; ++column)
        {
            var inferrer = new SqlTypeInferrer();

            // Skip the first row, which contains the column names
            for (var row = 1; row < rowCount; ++row)
            {
                inferrer.AddValue(table[row][column]);
            }

            columnTypes[column] = inferrer.InferSqlType();
        }

        foreach (var row in table)
        {
            for (var column = 0; column < row.Length; ++column)
            {
                row[column] = columnTypes[column].ParseValue(row[column]);
            }
        }

        var columnTypeMap = columnTypes
            .WithIndex()
            .Select(value => (columnNames[value.Item1], value.Item2))
            .ToDictionary();

        // Get functional dependencies

        var normalizer = new SchemaNormalizer(columnNames);

        Console.WriteLine(
            "Enter functional dependencies (e.g., A, B -> C, D). Enter \"end\" without quotes to stop:"
        );

        while (true)
        {
            var row = Console.ReadLine();

            if (row is null || row.ToLower().Trim() == "end")
            {
                break;
            }

            if (row.Length == 0)
            {
                continue;
            }

            var (determinantSet, dependentSet) = ParseFunctionalDependency(
                row
            );

            normalizer.AddFunctionalDependency(determinantSet, dependentSet);
        }

        // Get multivalued dependencies

        Console.WriteLine(
            "Enter multivalued dependencies (e.g., A, B ->> C, D). Enter \"end\" without quotes to stop:"
        );

        var multivaluedDependencies =
            new List<(HashSet<string> lhs, HashSet<string> rhs)>();

        while (true)
        {
            var row = Console.ReadLine();

            if (row is null || row.ToLower().Trim() == "end")
            {
                break;
            }

            if (row.Length == 0)
            {
                continue;
            }

            var (mvdLhsSet, mvdRhsSet) = ParseMultivaluedDependency(row);

            multivaluedDependencies.Add((mvdLhsSet, mvdRhsSet));
        }

        // Validate multivalued dependencies

        static int RowCompare(string[] lhs, string[] rhs)
        {
            for (var i = 0; i < lhs.Length; ++i)
            {
                var order = lhs[i].CompareTo(rhs[i]);

                if (order != 0)
                {
                    return order;
                }
            }

            return 0;
        }

        // Distinct rows
        var sortedData = table[1..];
        sortedData.Sort(RowCompare);
        var tableData = new List<string[]>();

        for (var i = 0; i < sortedData.Count; ++i)
        {
            if (i != 0 && RowCompare(sortedData[i - 1], sortedData[i]) == 0)
            {
                continue;
            }

            tableData.Add(sortedData[i]);
        }

        var columnIndexes = columnNames
            .Select((value, index) => (value, index))
            .ToDictionary();

        foreach (var (lhs, rhs) in multivaluedDependencies)
        {
            if (
                !ValidateMultivaluedDependency(
                    lhs,
                    rhs,
                    tableData,
                    columnIndexes
                )
            )
            {
                Logging.Warn(
                    $"The multivalued dependency {{{string.Join(", ", lhs)}}} ->> {{{string.Join(", ", rhs)}}} is invalid."
                );
            }

            normalizer.AddMultivaluedDependency(lhs, rhs);
        }

        // Get unique keys

        Console.WriteLine(
            "Enter unique keys (e.g., A, B). Enter \"end\" without quotes to stop:"
        );

        while (true)
        {
            var row = Console.ReadLine();

            if (row is null || row.ToLower().Trim() == "end")
            {
                break;
            }

            if (row.Length == 0)
            {
                continue;
            }

            var uniqueKey = ParseAttributeNames(row);
            normalizer.AddUniqueKey(uniqueKey);
        }

        // Normalize and determine foreign keys

        var schemas = normalizer.Normalize(targetNormalForm).ToList();
        var foreignKeyTableIndexes = new SetKeyDictionary<string, int>();

        foreach (var (tableIndex, (schema, _)) in schemas.WithIndex())
        {
            // Foreign keys must reference unique keys
            // Smaller foreign key more likely to be more sensible
            // We are essentially guessing the best foreign keys
            foreach (
                var uniqueKey in Enumerable
                    .Repeat(schema.PrimaryKey, 1)
                    .Concat(schema.UniqueKeys)
            )
            {
                foreignKeyTableIndexes.AddWithMinimalSetsInvariant(
                    uniqueKey.ToHashSet(),
                    tableIndex
                );
            }
        }

        foreach (var (index, (schema, normalForm)) in schemas.WithIndex())
        {
            Console.WriteLine();
            Console.WriteLine(
                GenerateSqlCode(
                    schema,
                    normalForm,
                    columnTypeMap,
                    foreignKeyTableIndexes,
                    index
                )
            );
            Console.WriteLine();
        }

        return 0;
    }

    private static void ShowHelp()
    {
        var programName = Path.GetFileName(Environment.ProcessPath);

        Console.WriteLine(
            $"""
            Usage: {programName} [-h|--help] [--nf <normal form>] <filename>

            -h, --help: Show this message and exit.

            normal form: The normal form to target.
                         Options: 1NF, 2NF, 3NF, BCNF, 4NF
                         Default: BCNF

            filename: The filename of a CSV file containing a table with attribute names
                      and sample data.
            """
        );
    }

    private static bool ParseArgs(
        string[] args,
        List<string> positionalArgs,
        Dictionary<string, string> namedArgs,
        Dictionary<string, bool> flagArgs,
        int minArgCount,
        int maxArgCount
    )
    {
        for (var i = 0; i < args.Length; )
        {
            var arg = args[i++];

            if (namedArgs.ContainsKey(arg))
            {
                if (i == args.Length)
                {
                    return false;
                }

                namedArgs[arg] = args[i++];
            }
            else if (flagArgs.ContainsKey(arg))
            {
                flagArgs[arg] = true;
            }
            else
            {
                positionalArgs.Add(arg);
            }
        }

        var argCount = positionalArgs.Count;
        return minArgCount <= argCount && argCount <= maxArgCount;
    }

    private static List<string[]> ReadCsvFile(string filename)
    {
        var parser = new CsvParser();
        var rows = new List<string[]>();
        var rowNumber = 0;

        try
        {
            foreach (var row in File.ReadLines(filename))
            {
                ++rowNumber;

                if (row.Length == 0)
                {
                    continue;
                }

                try
                {
                    rows.Add(parser.ParseRow(row));
                }
                catch (CsvParseException e)
                {
                    Console.WriteLine(
                        $"Error parsing CSV at row {rowNumber}, column {e.Column + 1}: {e.Message}"
                    );
                    Environment.Exit(1);
                }

                if (rows[^1].Length != rows[0].Length)
                {
                    Console.WriteLine(
                        $"Error parsing CSV at row {rowNumber}: The row has a different length."
                    );
                    Environment.Exit(1);
                }
            }
        }
        catch (IOException)
        {
            Console.WriteLine($"Error: Unable to read file {filename}.");
            Environment.Exit(1);
        }

        return rows;
    }

    private static (
        HashSet<string> determinantSet,
        HashSet<string> dependentSet
    ) ParseFunctionalDependency(string value)
    {
        var sides = value.Split("->");

        if (sides.Length != 2)
        {
            Console.WriteLine("Error: Unable to parse functional dependency.");
            Environment.Exit(1);
        }

        var determinantSet = ParseAttributeNames(sides[0]);
        var dependentSet = ParseAttributeNames(sides[1]);

        if (determinantSet.Count == 0)
        {
            Console.WriteLine("Error: The determinant set is empty.");
            Environment.Exit(1);
        }

        if (dependentSet.Count == 0)
        {
            Console.WriteLine("Error: The dependent set is empty.");
            Environment.Exit(1);
        }

        return (determinantSet, dependentSet);
    }

    private static (
        HashSet<string> mvdLhsSet,
        HashSet<string> mvdRhsSet
    ) ParseMultivaluedDependency(string value)
    {
        var sides = value.Split("->>");

        if (sides.Length != 2)
        {
            Console.WriteLine("Error: Unable to parse functional dependency.");
            Environment.Exit(1);
        }

        var mvdLhsSet = ParseAttributeNames(sides[0]);
        var mvdRhsSet = ParseAttributeNames(sides[1]);

        if (mvdLhsSet.Count == 0)
        {
            Console.WriteLine("Error: The LHS set is empty.");
            Environment.Exit(1);
        }

        if (mvdRhsSet.Count == 0)
        {
            Console.WriteLine("Error: The RHS set is empty.");
            Environment.Exit(1);
        }

        return (mvdLhsSet, mvdRhsSet);
    }

    private static HashSet<string> ParseAttributeNames(string value)
    {
        return
        [
            .. value
                .Split(',')
                .Select(value =>
                    value.Normalize(NormalizationForm.FormKC).Trim()
                )
        ];
    }

    private static bool ValidateMultivaluedDependency(
        HashSet<string> mvdLhsSet,
        HashSet<string> mvdRhsSet,
        List<string[]> tableData,
        Dictionary<string, int> columnIndexes
    )
    {
        if (tableData.Count == 0)
        {
            return true;
        }

        mvdRhsSet = [.. mvdRhsSet];
        mvdRhsSet.ExceptWith(mvdLhsSet);

        if (mvdRhsSet.Count == 0)
        {
            // Trivial MVD
            return true;
        }

        var lhsColumns = mvdLhsSet
            .Select(value => columnIndexes[value])
            .OrderBy(value => value)
            .ToArray();
        var rhsColumns = mvdRhsSet
            .Select(value => columnIndexes[value])
            .OrderBy(value => value)
            .ToArray();
        var otherColumns = columnIndexes
            .Values.Except(lhsColumns)
            .Except(rhsColumns)
            .OrderBy(value => value)
            .ToArray();

        if (otherColumns.Length == 0)
        {
            return true;
        }

        var columnCompareOrder = lhsColumns
            .Concat(rhsColumns)
            .Concat(otherColumns);

        int RowCompare(string[] lhs, string[] rhs)
        {
            foreach (var columnIndex in columnCompareOrder)
            {
                var order = lhs[columnIndex].CompareTo(rhs[columnIndex]);

                if (order != 0)
                {
                    return order;
                }
            }

            return 0;
        }

        tableData.Sort(RowCompare);

        static bool RowEquals(string[] row1, string[] row2, int[] indexes)
        {
            foreach (var index in indexes)
            {
                if (row1[index] != row2[index])
                {
                    return false;
                }
            }

            return true;
        }

        for (var beginLhs = 0; beginLhs < tableData.Count; )
        {
            var beginLhsRow = tableData[beginLhs];
            var endLhs = beginLhs;

            // Find block where column values from the MVD LHS are equal
            for (
                ;
                endLhs < tableData.Count
                    && RowEquals(beginLhsRow, tableData[endLhs], lhsColumns);
                ++endLhs
            ) { }

            var beginRhs = beginLhs;
            var endRhs = beginRhs;

            // Find block where column values from the MVD RHS are equal
            for (
                ;
                endRhs < endLhs
                    && RowEquals(beginLhsRow, tableData[endRhs], rhsColumns);
                ++endRhs
            ) { }

            var lhsBlockSize = endLhs - beginLhs;
            var rhsBlockSize = endRhs - beginRhs;

            if (lhsBlockSize % rhsBlockSize != 0)
            {
                return false;
            }

            // Check that RHS blocks are equal in size

            for (; beginRhs < endLhs; beginRhs += rhsBlockSize)
            {
                var beginRhsRow = tableData[beginRhs];

                if (
                    beginRhs > beginLhs
                    && RowEquals(
                        beginRhsRow,
                        tableData[beginRhs - 1],
                        rhsColumns
                    )
                )
                {
                    return false;
                }

                for (var i = beginRhs + 1; i < beginRhs + rhsBlockSize; ++i)
                {
                    if (!RowEquals(beginRhsRow, tableData[i], rhsColumns))
                    {
                        return false;
                    }
                }
            }

            // Check that other column values repeat

            for (var i = beginLhs + rhsBlockSize; i < endLhs; ++i)
            {
                if (
                    !RowEquals(
                        tableData[i],
                        tableData[i - rhsBlockSize],
                        otherColumns
                    )
                )
                {
                    return false;
                }
            }

            beginLhs = endLhs;
        }

        return true;
    }

    private static string GenerateSqlCode(
        RelationSchema schema,
        NormalForm normalForm,
        IReadOnlyDictionary<string, SqlType> columnTypes,
        SetKeyDictionary<string, int> foreignKeyTableIndexes,
        int tableIndex
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- The following table is in {normalForm.Name()}");
        sb.AppendLine($"CREATE TABLE Table{tableIndex + 1}(");

        // Trailing comma is allowed in SQL

        foreach (var columnName in schema.Attributes)
        {
            var columnTypeCode = columnTypes[columnName]
                .GenerateSqlCode()
                .ToUpper();
            sb.AppendLine($"    {columnName} {columnTypeCode},");
        }

        sb.AppendLine(
            $"    PRIMARY KEY ({string.Join(", ", schema.PrimaryKey)}),"
        );

        foreach (var uniqueKey in schema.UniqueKeys)
        {
            sb.AppendLine($"    UNIQUE ({string.Join(", ", uniqueKey)}),");
        }

        foreach (
            var (
                foreignKey,
                referencesTableIndex
            ) in foreignKeyTableIndexes.GetSubsetEntriesBreadthFirst(
                schema.Attributes.ToHashSet()
            )
        )
        {
            if (referencesTableIndex == tableIndex)
            {
                continue;
            }

            var referencesTableName = $"Table{referencesTableIndex + 1}";

            sb.AppendLine(
                $"    FOREIGN KEY ({string.Join(", ", foreignKey)}) REFERENCES {referencesTableName},"
            );
        }

        sb.Append(");");
        return sb.ToString();
    }
}
