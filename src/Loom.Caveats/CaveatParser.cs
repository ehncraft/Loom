using System.Globalization;

namespace Loom.Caveats;

public static class CaveatParser
{
    public static IReadOnlyDictionary<string, CaveatDefinition> Parse(string source)
    {
        var tokens = CaveatLexer.Tokenize(source);
        var state = new ParserState(tokens);

        var definitions = new Dictionary<string, CaveatDefinition>(StringComparer.Ordinal);
        while (state.Current.Kind is not CaveatTokenKind.Eof)
        {
            var definition = ParseCaveat(state);
            definitions[definition.Name] = definition;
        }

        return definitions;
    }

    private static CaveatDefinition ParseCaveat(ParserState state)
    {
        state.Expect(CaveatTokenKind.KeywordCaveat);
        var name = state.Expect(CaveatTokenKind.Identifier).Text;
        state.Expect(CaveatTokenKind.LParen);

        var parameters = new List<CaveatParameter>();
        if (state.Current.Kind is not CaveatTokenKind.RParen)
        {
            parameters.Add(ParseParameter(state));
            while (state.Current.Kind is CaveatTokenKind.Comma)
            {
                state.Advance();
                parameters.Add(ParseParameter(state));
            }
        }

        state.Expect(CaveatTokenKind.RParen);
        state.Expect(CaveatTokenKind.LBrace);
        var body = ParseOr(state);
        state.Expect(CaveatTokenKind.RBrace);

        return new CaveatDefinition(name, parameters, body);
    }

    private static CaveatParameter ParseParameter(ParserState state)
    {
        var name = state.Expect(CaveatTokenKind.Identifier).Text;
        var type = ParseType(state);
        return new CaveatParameter(name, type);
    }

    private static CaveatParameterType ParseType(ParserState state)
    {
        if (state.Current.Kind is CaveatTokenKind.KeywordList)
        {
            state.Advance();
            state.Expect(CaveatTokenKind.Less);
            state.Expect(CaveatTokenKind.KeywordString);
            state.Expect(CaveatTokenKind.Greater);
            return CaveatParameterType.StringList;
        }

        return state.Current.Kind switch
        {
            CaveatTokenKind.KeywordString => Consume(state, CaveatParameterType.Text),
            CaveatTokenKind.KeywordInt => Consume(state, CaveatParameterType.Number),
            CaveatTokenKind.KeywordBool => Consume(state, CaveatParameterType.Bool),
            _ => throw new CaveatParseException(
                $"Expected a parameter type but found {state.Current.Kind} at position {state.Current.Position}."),
        };

        static CaveatParameterType Consume(ParserState state, CaveatParameterType type)
        {
            state.Advance();
            return type;
        }
    }

    private static CelExpr ParseOr(ParserState state)
    {
        var left = ParseAnd(state);
        while (state.Current.Kind is CaveatTokenKind.OrOr)
        {
            state.Advance();
            var right = ParseAnd(state);
            left = new CelExpr.LogicalOr(left, right);
        }

        return left;
    }

    private static CelExpr ParseAnd(ParserState state)
    {
        var left = ParseNot(state);
        while (state.Current.Kind is CaveatTokenKind.AndAnd)
        {
            state.Advance();
            var right = ParseNot(state);
            left = new CelExpr.LogicalAnd(left, right);
        }

        return left;
    }

    private static CelExpr ParseNot(ParserState state)
    {
        if (state.Current.Kind is not CaveatTokenKind.Not)
        {
            return ParseComparison(state);
        }

        state.Advance();
        return new CelExpr.LogicalNot(ParseNot(state));
    }

    private static CelExpr ParseComparison(ParserState state)
    {
        var left = ParseIn(state);
        var op = state.Current.Kind switch
        {
            CaveatTokenKind.EqualEqual => ComparisonOperator.Equal,
            CaveatTokenKind.NotEqual => ComparisonOperator.NotEqual,
            CaveatTokenKind.Less => ComparisonOperator.LessThan,
            CaveatTokenKind.LessOrEqual => ComparisonOperator.LessOrEqual,
            CaveatTokenKind.Greater => ComparisonOperator.GreaterThan,
            CaveatTokenKind.GreaterOrEqual => ComparisonOperator.GreaterOrEqual,
            _ => (ComparisonOperator?)null,
        };

        if (op is not { } resolved)
        {
            return left;
        }

        state.Advance();
        var right = ParseIn(state);
        return new CelExpr.Comparison(left, resolved, right);
    }

    private static CelExpr ParseIn(ParserState state)
    {
        var left = ParsePrimary(state);
        if (state.Current.Kind is not CaveatTokenKind.KeywordIn)
        {
            return left;
        }

        state.Advance();
        var right = ParsePrimary(state);
        return new CelExpr.InList(left, right);
    }

    private static CelExpr ParsePrimary(ParserState state)
    {
        switch (state.Current.Kind)
        {
            case CaveatTokenKind.LParen:
                state.Advance();
                var inner = ParseOr(state);
                state.Expect(CaveatTokenKind.RParen);
                return inner;
            case CaveatTokenKind.IntLiteral:
                var intToken = state.Advance();
                return new CelExpr.Literal(int.Parse(intToken.Text, CultureInfo.InvariantCulture));
            case CaveatTokenKind.StringLiteral:
                return new CelExpr.Literal(state.Advance().Text);
            case CaveatTokenKind.KeywordTrue:
                state.Advance();
                return new CelExpr.Literal(true);
            case CaveatTokenKind.KeywordFalse:
                state.Advance();
                return new CelExpr.Literal(false);
            case CaveatTokenKind.LBracket:
                return ParseListLiteral(state);
            case CaveatTokenKind.Identifier:
                return new CelExpr.Variable(state.Advance().Text);
            default:
                throw new CaveatParseException(
                    $"Unexpected token {state.Current.Kind} ('{state.Current.Text}') at position {state.Current.Position}.");
        }
    }

    private static CelExpr.Literal ParseListLiteral(ParserState state)
    {
        state.Expect(CaveatTokenKind.LBracket);
        var items = new List<object?>();
        if (state.Current.Kind is not CaveatTokenKind.RBracket)
        {
            items.Add(ParseListItem(state));
            while (state.Current.Kind is CaveatTokenKind.Comma)
            {
                state.Advance();
                items.Add(ParseListItem(state));
            }
        }

        state.Expect(CaveatTokenKind.RBracket);
        return new CelExpr.Literal(items);
    }

    private static object? ParseListItem(ParserState state) => state.Current.Kind switch
    {
        CaveatTokenKind.IntLiteral => int.Parse(state.Advance().Text, CultureInfo.InvariantCulture),
        CaveatTokenKind.StringLiteral => state.Advance().Text,
        CaveatTokenKind.KeywordTrue => Consume(state, true),
        CaveatTokenKind.KeywordFalse => Consume(state, false),
        _ => throw new CaveatParseException(
            $"Unexpected list item {state.Current.Kind} at position {state.Current.Position}."),
    };

    private static bool Consume(ParserState state, bool value)
    {
        state.Advance();
        return value;
    }

    private sealed class ParserState(IReadOnlyList<CaveatToken> tokens)
    {
        private int _index;

        public CaveatToken Current => tokens[_index];

        public CaveatToken Advance()
        {
            var token = Current;
            _index++;
            return token;
        }

        public CaveatToken Expect(CaveatTokenKind kind)
        {
            if (Current.Kind != kind)
            {
                throw new CaveatParseException(
                    $"Expected {kind} but found {Current.Kind} ('{Current.Text}') at position {Current.Position}.");
            }

            return Advance();
        }
    }
}
