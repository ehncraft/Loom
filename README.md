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

Early, in-progress. Phase 1 + Phase 2 (current): an in-memory `Check`/`Expand`/
`LookupResources`/`LookupSubjects` evaluator over a mutable relation-tuple store, with
Netflix-style caveats (CEL-subset conditions attached to tuples) wired in as a third
`Caveated` outcome alongside `Permit`/`Deny`. No SQL-backed storage, no multi-tenancy, no
AuthZEN adapter yet.

## Packages

| Package | Description |
|---|---|
| `Loom.Core` | Core value types: `ObjectRef`, `SubjectRef`, `RelationTuple`, `CheckResult`, `ExpandNode` |
| `Loom.Schema` | SpiceDB-style schema language: lexer, parser, AST (`definition`/`relation`/`permission`) |
| `Loom.Relationships` | Mutable relation-tuple storage: read/write/delete, namespace enumeration |
| `Loom.Caveats` | CEL-subset expression engine for ABAC conditions attached to relation tuples |
| `Loom.Evaluator` | `Check`, `Expand`, `LookupResources`, `LookupSubjects` against a schema + tuple store |

`LookupResources`/`LookupSubjects` are brute-force today (scan every resource/subject of the
target namespace and `Check` each) — correct, not yet optimized; a reverse-index-based
traversal is a later-phase concern. `Expand` is purely structural and caveat-unaware, matching
real SpiceDB semantics (caveats only resolve at `Check` time, once runtime context is known).

Planned: `Loom.Sql` (SQL Server-backed storage), `Loom.AspNetCore`, `Loom.AuthZen`, `Loom.Testing`.

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

## Modeling org hierarchy: Department is first-class

For multi-tenant deployments where every tenant has departments (the common case this engine
targets), model `department` as its own schema definition with an explicit `tuple_to_userset`
edge back to its tenant — not as an attribute bolted onto a membership or flattened into a
permission-key suffix. This lets a tenant-level admin grant cascade into every department for
free, and keeps department-scoped approval authority (a real banking/SACCO requirement) a
first-class relationship rather than a side channel:

```
definition membership {}

definition tenant {
    relation admin: membership
}

definition department {
    relation tenant: tenant
    relation lead: membership
    relation staff: membership

    permission approve_requests = lead + tenant->admin
    permission view_requests = approve_requests + staff
}
```

See `DepartmentHierarchyTests.cs` for the worked example this schema is tested against.

## Design principles

Borrowed from [Aegis](https://github.com/ehncraft/Aegis) (this project's Cedar-based
predecessor, still maintained independently): modular by concern (one package per
responsibility), eager schema validation, lazy-compiled expressions, pluggable storage
backends, multi-tenancy as a first-class concern, explainable decisions, and no vendor/FFI
dependency — built directly against the published Zanzibar model rather than wrapping an
existing server.

## License

MIT
