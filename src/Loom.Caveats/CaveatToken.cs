namespace Loom.Caveats;

internal readonly record struct CaveatToken(CaveatTokenKind Kind, string Text, int Position);
