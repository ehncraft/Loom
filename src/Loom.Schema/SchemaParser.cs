namespace Loom.Schema;

public static class SchemaParser
{
    public static LoomSchema Parse(string source)
    {
        var tokens = Lexer.Tokenize(source);
        var state = new ParserState(tokens);

        var types = new Dictionary<string, ObjectTypeDefinition>(StringComparer.Ordinal);
        while (state.Current.Kind is not TokenKind.Eof)
        {
            var definition = ParseDefinition(state);
            types[definition.Name] = definition;
        }

        return new LoomSchema(types);
    }

    private static ObjectTypeDefinition ParseDefinition(ParserState state)
    {
        state.Expect(TokenKind.KeywordDefinition);
        var name = state.Expect(TokenKind.Identifier).Text;
        state.Expect(TokenKind.LBrace);

        var relations = new Dictionary<string, RelationDefinition>(StringComparer.Ordinal);
        var permissions = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);

        while (state.Current.Kind is not TokenKind.RBrace)
        {
            if (state.Current.Kind is TokenKind.KeywordRelation)
            {
                var relation = ParseRelation(state);
                relations[relation.Name] = relation;
            }
            else
            {
                var permission = ParsePermission(state);
                permissions[permission.Name] = permission;
            }
        }

        state.Expect(TokenKind.RBrace);
        return new ObjectTypeDefinition(name, relations, permissions);
    }

    private static RelationDefinition ParseRelation(ParserState state)
    {
        state.Expect(TokenKind.KeywordRelation);
        var name = state.Expect(TokenKind.Identifier).Text;
        state.Expect(TokenKind.Colon);

        var allowedTypes = new List<RelationTypeRef> { ParseTypeRef(state) };
        while (state.Current.Kind is TokenKind.Pipe)
        {
            state.Advance();
            allowedTypes.Add(ParseTypeRef(state));
        }

        return new RelationDefinition(name, allowedTypes);
    }

    private static RelationTypeRef ParseTypeRef(ParserState state)
    {
        var ns = state.Expect(TokenKind.Identifier).Text;
        if (state.Current.Kind is not TokenKind.Hash)
        {
            return new RelationTypeRef(ns);
        }

        state.Advance();
        var relation = state.Expect(TokenKind.Identifier).Text;
        return new RelationTypeRef(ns, relation);
    }

    private static PermissionDefinition ParsePermission(ParserState state)
    {
        state.Expect(TokenKind.KeywordPermission);
        var name = state.Expect(TokenKind.Identifier).Text;
        state.Expect(TokenKind.Equals);
        var expression = ParseExpr(state);
        return new PermissionDefinition(name, expression);
    }

    private static PermissionExpr ParseExpr(ParserState state)
    {
        var left = ParseTerm(state);
        while (state.Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var isUnion = state.Current.Kind is TokenKind.Plus;
            state.Advance();
            var right = ParseTerm(state);
            left = isUnion ? new PermissionExpr.Union(left, right) : new PermissionExpr.Exclusion(left, right);
        }

        return left;
    }

    private static PermissionExpr ParseTerm(ParserState state)
    {
        var left = ParseFactor(state);
        while (state.Current.Kind is TokenKind.Amp)
        {
            state.Advance();
            var right = ParseFactor(state);
            left = new PermissionExpr.Intersection(left, right);
        }

        return left;
    }

    private static PermissionExpr ParseFactor(ParserState state)
    {
        if (state.Current.Kind is TokenKind.LParen)
        {
            state.Advance();
            var inner = ParseExpr(state);
            state.Expect(TokenKind.RParen);
            return inner;
        }

        var name = state.Expect(TokenKind.Identifier).Text;
        if (state.Current.Kind is not TokenKind.Arrow)
        {
            return new PermissionExpr.Leaf(name);
        }

        state.Advance();
        var target = state.Expect(TokenKind.Identifier).Text;
        return new PermissionExpr.Arrow(name, target);
    }

    private sealed class ParserState(IReadOnlyList<Token> tokens)
    {
        private int _index;

        public Token Current => tokens[_index];

        public void Advance() => _index++;

        public Token Expect(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                throw new SchemaParseException(
                    $"Expected {kind} but found {Current.Kind} ('{Current.Text}') at position {Current.Position}.");
            }

            var token = Current;
            Advance();
            return token;
        }
    }
}
