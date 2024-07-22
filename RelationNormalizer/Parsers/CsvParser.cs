using System.Text;

namespace RelationNormalizer.Parsers;

/// <summary>
/// A parser for CSV.
/// </summary>
internal class CsvParser
{
    /// <summary>
    /// The delimiters to use for parsing CSV.
    /// </summary>
    public char[] Delimiters { get; set; }

    /// <summary>
    /// A list that stores items in the row currently being parsed.
    /// </summary>
    private readonly List<string> _items = [];

    /// <summary>
    /// A string builder that is used whenever a string needs to be built.
    /// </summary>
    private readonly StringBuilder _sb = new();

    /// <summary>
    /// Constructs a new CsvParser with comma, semicolon, and tab delimiters.
    /// </summary>
    public CsvParser()
    {
        Delimiters = [',', ';', '\t'];
    }

    /// <summary>
    /// Constructors a new CsvParser with the provided delimiters.
    /// </summary>
    /// <param name="delimiters">An array of delimiters to use.</param>
    public CsvParser(char[] delimiters)
    {
        Delimiters = [.. delimiters];
    }

    /// <summary>
    /// Parses a row of CSV.
    /// </summary>
    /// <param name="row">The row of CSV to parse.</param>
    /// <returns>The values of each column in the row.</returns>
    public string[] ParseRow(string row)
    {
        for (var i = 0; i < row.Length; )
        {
            if (row[i] == '"')
            {
                var (itemString, index) = ParseString(row, i);
                i = index;
                _items.Add(itemString);
            }
            else
            {
                while (i < row.Length && !IsDelimiter(row[i]))
                {
                    _sb.Append(row[i++]);
                }

                if (i < row.Length)
                {
                    ++i; // Go past item delimiter
                }

                _items.Add(_sb.ToString());
                _sb.Clear();
            }
        }

        var result = _items.ToArray();
        _items.Clear();
        return result;
    }

    /// <summary>
    /// Parses a single string delimited by double quotes.
    /// </summary>
    /// <param name="row">The row of CSV.</param>
    /// <param name="index">The index at the start of the string.
    /// Must include the double quote delimiter.</param>
    /// <returns>The string parsed
    /// and the index at the start of the next item in the row.</returns>
    /// <exception cref="CsvParseException"></exception>
    private (string result, int index) ParseString(string row, int index)
    {
        var i = index + 1;
        var end = false;

        while (!end)
        {
            if (i == row.Length)
            {
                throw new CsvParseException("Unterminated string literal.")
                {
                    Column = i
                };
            }

            var ch = row[i++];

            switch (ch)
            {
                default:
                    _sb.Append(ch);
                    break;

                case '"':
                    if (i < row.Length && row[i] == '"')
                    {
                        _sb.Append('"');
                        ++i;
                    }
                    else if (i < row.Length && !IsDelimiter(row[i]))
                    {
                        _sb.Append('"');
                        _sb.Append(row[i]);
                        ++i;
                    }
                    else
                    {
                        end = true;
                    }
                    break;

                case '\\':
                    if (i == row.Length)
                    {
                        // Next iteration will throw exception
                        continue;
                    }

                    ch = row[i++];

                    switch (ch)
                    {
                        default:
                            _sb.Append('\\');
                            _sb.Append(ch);
                            break;
                        case 'a':
                            _sb.Append('\a');
                            break;
                        case 'b':
                            _sb.Append('\b');
                            break;
                        case 'f':
                            _sb.Append('\f');
                            break;
                        case 'n':
                            _sb.Append('\n');
                            break;
                        case 'r':
                            _sb.Append('\r');
                            break;
                        case 't':
                            _sb.Append('\t');
                            break;
                        case 'v':
                            _sb.Append('\v');
                            break;
                        case '\'':
                            _sb.Append('\'');
                            break;
                        case '"':
                            _sb.Append('\"');
                            break;
                        case '\\':
                            _sb.Append('\\');
                            break;
                    }

                    break;
            }
        }

        if (i < row.Length)
        {
            ++i; // Go past item delimiter
        }

        var result = _sb.ToString();
        index = i;
        _sb.Clear();
        return (result, index);
    }

    /// <summary>
    /// Checks whether a char is a delimiter.
    /// </summary>
    /// <param name="ch">The char to check.</param>
    /// <returns>Whether <c>ch</c> is a delimiter.</returns>
    private bool IsDelimiter(char ch) => Delimiters.Contains(ch);
}
