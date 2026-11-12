# Loom

A .NET-native reimplementation of Google's [Zanzibar](https://research.google/pubs/pub48190/)
relationship-based access control model (as implemented by [SpiceDB](https://authzed.com/docs/spicedb)),
with [Netflix-style ABAC caveats](https://netflixtechblog.com/abac-on-spicedb-enabling-netflixs-complex-identity-types-c118f374fa89)
layered on top for attribute-based conditions.

Loom is a Policy Decision Point library, not an authentication system — it answers
"can this subject perform this permission on this object?" given a schema and a set of
relation tuples. Authentication (OAuth, OIDC, etc.) is upstream; Loom consumes whatever
subject identity you hand it.

## Status

Early, in-progress. Phase 1 (current): an in-memory `Check` evaluator proving the core
relation-tuple graph algorithm — direct relations, computed-userset inheritance,
tuple-to-userset hierarchy traversal, nested usersets, and cycle safety. No caveats, no
SQL-backed storage, no multi-tenancy, no AuthZEN adapter yet — see `CHANGELOG.md` and the
package table below as those land.

## Packages

| Package | Description |
|---|---|
| `Loom.Core` | Core value types: `ObjectRef`, `SubjectRef`, `RelationTuple`, `CheckResult` |
| `Loom.Schema` | SpiceDB-style schema language: lexer, parser, AST (`definition`/`relation`/`permission`) |
| `Loom.Evaluator` | The `Check` algorithm — resolves a permission's rewrite tree against relation tuples |

Planned: `Loom.Caveats` (CEL-subset expression engine for ABAC conditions), `Loom.Relationships`
(mutable tuple-store abstraction), `Loom.Sql` (SQL Server-backed storage), `Loom.AspNetCore`,
`Loom.AuthZen`, `Loom.Testing`.

## Schema example

```
definition user {}

definition group {
    relation member: user | group#member
}

definition document {
    relation owner: user
    relation editor: user | group#member
    relation viewer: user | group#member

    permission edit = owner + editor
    permission view = edit + viewer
}
```

## Design principles

Borrowed from [Aegis](https://github.com/ehncraft/Aegis) (this project's Cedar-based
predecessor, still maintained independently): modular by concern (one package per
responsibility), eager schema validation, lazy-compiled expressions, pluggable storage
backends, multi-tenancy as a first-class concern, explainable decisions, and no vendor/FFI
dependency — built directly against the published Zanzibar model rather than wrapping an
existing server.

## License

MIT
