using System.Globalization;

namespace Loom.Schema;

internal static class Lexer
{
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var position = 0;

        while (position < source.Length)
        {
            var ch = source[position];

            if (char.IsWhiteSpace(ch))
            {
                position++;
                continue;
            }

            if (ch is '-' && position + 1 < source.Length && source[position + 1] is '>')
            {
                tokens.Add(new Token(TokenKind.Arrow, "->", position));
                position += 2;
                continue;
            }

            var symbolKind = ch switch
            {
                '{' => TokenKind.LBrace,
                '}' => TokenKind.RBrace,
                '(' => TokenKind.LParen,
                ')' => TokenKind.RParen,
                ':' => TokenKind.Colon,
                '|' => TokenKind.Pipe,
                '#' => TokenKind.Hash,
                '=' => TokenKind.Equals,
                '+' => TokenKind.Plus,
                '-' => TokenKind.Minus,
                '&' => TokenKind.Amp,
                _ => (TokenKind?)null,
            };

            if (symbolKind is { } kind)
            {
                tokens.Add(new Token(kind, ch.ToString(CultureInfo.InvariantCulture), position));
                position++;
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
                var keywordKind = text switch
                {
                    "definition" => TokenKind.KeywordDefinition,
                    "relation" => TokenKind.KeywordRelation,
                    "permission" => TokenKind.KeywordPermission,
                    _ => TokenKind.Identifier,
                };
                tokens.Add(new Token(keywordKind, text, start));
                continue;
            }

            throw new SchemaParseException($"Unexpected character '{ch}' at position {position}.");
        }

        tokens.Add(new Token(TokenKind.Eof, string.Empty, position));
        return tokens;
    }
}
