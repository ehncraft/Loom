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

Early, in-progress. Phases 1-4 (current): a fully async `CheckAsync`/`ExpandAsync`/
`LookupResourcesAsync`/`LookupSubjectsAsync` evaluator over a mutable relation-tuple store, with
Netflix-style caveats (CEL-subset conditions attached to tuples) wired in as a third
`Caveated` outcome alongside `Permit`/`Deny`, SQL Server-backed storage for both tuples and
schema, a per-tenant engine cache, ASP.NET Core DI/`IAuthorizationHandler` integration,
framework-agnostic test assertion helpers, and an AuthZEN Authorization API 1.0-compatible HTTP
adapter. No publish CI yet (nothing's been published to nuget.org).

All store and evaluator APIs are async (`Task`-returning, `CancellationToken`-accepting) even
though the in-memory implementation has no real I/O to await — `Loom.Spool`'s SQL-backed store
genuinely needs it, and a single interface shape avoids a sync/async split between backends.

## Packages

| Package | Description |
|---|---|
| `Loom.Core` | Core value types: `ObjectRef`/`SubjectRef`/`RelationTuple`/`CheckResult`/`ExpandNode`, claims-to-`SubjectRef` mapping, JSON-to-CLR value conversion |
| `Loom.Schema` | SpiceDB-style schema language: lexer, parser, AST (`definition`/`relation`/`permission`) |
| `Loom.Relationships` | Mutable relation-tuple storage abstraction + in-memory implementation |
| `Loom.Caveats` | CEL-subset expression engine for ABAC conditions attached to relation tuples |
| `Loom.Spool` | SQL Server-backed relation-tuple and schema storage, configurable table/column mapping |
| `Loom.Evaluator` | `CheckAsync`, `ExpandAsync`, `LookupResourcesAsync`, `LookupSubjectsAsync`, `MultiTenantLoomEngine` |
| `Loom.AspNetCore` | `services.AddLoom(...)` DI registration + `LoomAuthorizationHandler` (native `IAuthorizationHandler`) |
| `Loom.Testing` | `ShouldPermitAsync`/`ShouldDenyAsync`/`ShouldRequireCaveatContextAsync` assertion helpers |
| `Loom.AuthZen` | AuthZEN Authorization API 1.0-compatible `/access/v1/evaluation`(s) endpoints + health check |

`LookupResourcesAsync`/`LookupSubjectsAsync` are brute-force today (scan every resource/subject
of the target namespace and `CheckAsync` each) — correct, not yet optimized; a reverse-index-based
traversal is a later-phase concern. `ExpandAsync` is purely structural and caveat-unaware,
matching real SpiceDB semantics (caveats only resolve at `Check` time, once runtime context is
known). `Loom.Spool` isolates tenants structurally, one schema per tenant (e.g.
`tenant_123.RelationTuples`), not a shared table filtered by a tenant-id column —
`MultiTenantLoomEngine` mirrors that same structural isolation at the evaluator layer, one
lazily-built `LoomEvaluator` cached per tenant until `InvalidateAsync` evicts it.

`LoomAuthorizationHandler` resolves the resource to check against `AuthorizationHandlerContext`'s
own resource object: a supplied `ObjectRef` is used directly, a `string` becomes
`new ObjectRef(requirement.ResourceNamespace, thatString)`, and anything else (declarative
`[Authorize(Policy = "...")]` with no instance in hand) falls back to
`ObjectRef.KindLevel(requirement.ResourceNamespace)` — a well-known `"_"` id representing "the
resource kind as a whole," the closest Zanzibar equivalent to Cedar's type-only
`resource is SomeKind` policies (Zanzibar always checks a specific object id; there's no native
type-level-only check).

`Loom.AuthZen` maps AuthZEN's three-field `subject`/`resource`/`action` (plus an optional
free-form `context`) onto `CheckAsync`, mirroring Aegis.AuthZen's wire shape closely: wire
`context`/`properties` values are `JsonElement` (not deserialized straight into
`Dictionary<string, object?>`, which System.Text.Json would fill with boxed `JsonElement`s
rather than plain CLR values — a real bug caught by the test suite, see `JsonValueConverter`)
and explicitly converted via `Loom.Core`'s `JsonValueConverter` before evaluation. Both the
single (`/access/v1/evaluation`) and batch (`/access/v1/evaluations`, with
`execute_all`/`deny_on_first_deny`/`permit_on_first_permit` semantics) endpoints are mapped,
matching the full `Aegis.AuthZen` surface. A three-valued `Caveated` result maps to AuthZEN's
boolean `decision: false` plus a `missing_context` array in the response's free-form `context`
slot, since AuthZEN itself has no native three-valued concept.

Planned: publish CI (release-please + NuGet OIDC Trusted Publishing).

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
