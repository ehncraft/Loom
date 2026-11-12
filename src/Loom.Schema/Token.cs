namespace Loom.Schema;

internal readonly record struct Token(TokenKind Kind, string Text, int Position);
