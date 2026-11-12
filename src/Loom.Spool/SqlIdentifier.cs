using System.Text.RegularExpressions;

namespace Loom.Spool;

public static partial class SqlIdentifier
{
    public static string Quote(string identifier)
    {
        if (!SafeIdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException(
                $"'{identifier}' is not a safe SQL identifier -- only letters, digits, and underscores are " +
                "allowed, and it can't start with a digit. Table/column names are configuration, but they're " +
                "interpolated directly into SQL text (identifiers can't be parameterized), so this is checked " +
                "rather than trusted.",
                nameof(identifier));
        }

        return $"[{identifier}]";
    }

    public static string Quote(string? schema, string table) =>
        string.IsNullOrEmpty(schema) ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SafeIdentifierPattern();
}
