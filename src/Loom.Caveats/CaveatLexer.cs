using System.Globalization;

namespace Loom.Caveats;

internal static class CaveatLexer
{
    private static readonly Dictionary<string, CaveatTokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["caveat"] = CaveatTokenKind.KeywordCaveat,
        ["list"] = CaveatTokenKind.KeywordList,
        ["string"] = CaveatTokenKind.KeywordString,
        ["int"] = CaveatTokenKind.KeywordInt,
        ["bool"] = CaveatTokenKind.KeywordBool,
        ["true"] = CaveatTokenKind.KeywordTrue,
        ["false"] = CaveatTokenKind.KeywordFalse,
        ["in"] = CaveatTokenKind.KeywordIn,
    };

    public static IReadOnlyList<CaveatToken> Tokenize(string source)
    {
        var tokens = new List<CaveatToken>();
        var position = 0;

        while (position < source.Length)
        {
            var ch = source[position];

            if (char.IsWhiteSpace(ch))
            {
                position++;
                continue;
            }

            if (TryReadTwoCharOperator(source, position, out var twoChar, out var twoCharKind))
            {
                tokens.Add(new CaveatToken(twoCharKind, twoChar, position));
                position += 2;
                continue;
            }

            var singleCharKind = ch switch
            {
                '(' => CaveatTokenKind.LParen,
                ')' => CaveatTokenKind.RParen,
                '{' => CaveatTokenKind.LBrace,
                '}' => CaveatTokenKind.RBrace,
                '[' => CaveatTokenKind.LBracket,
                ']' => CaveatTokenKind.RBracket,
                ',' => CaveatTokenKind.Comma,
                '<' => CaveatTokenKind.Less,
                '>' => CaveatTokenKind.Greater,
                '!' => CaveatTokenKind.Not,
                _ => (CaveatTokenKind?)null,
            };

            if (singleCharKind is { } kind)
            {
                tokens.Add(new CaveatToken(kind, ch.ToString(CultureInfo.InvariantCulture), position));
                position++;
                continue;
            }

            if (ch is '"')
            {
                var (text, length) = ReadStringLiteral(source, position);
                tokens.Add(new CaveatToken(CaveatTokenKind.StringLiteral, text, position));
                position += length;
                continue;
            }

            if (char.IsDigit(ch))
            {
                var start = position;
                while (position < source.Length && char.IsDigit(source[position]))
                {
                    position++;
                }

                tokens.Add(new CaveatToken(CaveatTokenKind.IntLiteral, source[start..position], start));
                continue;
            }

            if (char.IsLetter(ch) || ch is '_')
            {
                var start = position;
                while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] is '_'))
                {
                    position++;
                }

                var text = source[start..position];
                var keywordKind = Keywords.GetValueOrDefault(text, CaveatTokenKind.Identifier);
                tokens.Add(new CaveatToken(keywordKind, text, start));
                continue;
            }

            throw new CaveatParseException($"Unexpected character '{ch}' at position {position}.");
        }

        tokens.Add(new CaveatToken(CaveatTokenKind.Eof, string.Empty, position));
        return tokens;
    }

    private static bool TryReadTwoCharOperator(string source, int position, out string text, out CaveatTokenKind kind)
    {
        if (position + 1 < source.Length)
        {
            var pair = source.Substring(position, 2);
            var pairKind = pair switch
            {
                "==" => CaveatTokenKind.EqualEqual,
                "!=" => CaveatTokenKind.NotEqual,
                "<=" => CaveatTokenKind.LessOrEqual,
                ">=" => CaveatTokenKind.GreaterOrEqual,
                "&&" => CaveatTokenKind.AndAnd,
                "||" => CaveatTokenKind.OrOr,
                _ => (CaveatTokenKind?)null,
            };

            if (pairKind is { } resolved)
            {
                text = pair;
                kind = resolved;
                return true;
            }
        }

        text = string.Empty;
        kind = default;
        return false;
    }

    private static (string Text, int Length) ReadStringLiteral(string source, int start)
    {
        var position = start + 1;
        var content = new System.Text.StringBuilder();
        while (position < source.Length && source[position] is not '"')
        {
            content.Append(source[position]);
            position++;
        }

        if (position >= source.Length)
        {
            throw new CaveatParseException($"Unterminated string literal starting at position {start}.");
        }

        return (content.ToString(), position - start + 1);
    }
}
