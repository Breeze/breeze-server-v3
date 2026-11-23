# Breeze .NET Server release notes

What changed in each release of the Breeze .NET packages 8.x, for people using them. Upgrading an
application from 7.x is covered in [UPGRADE.md](./UPGRADE.md).

---

## 8.0.1 — 2026-11-23

### Fixed

**`expand` was ignored until the first persistence manager had been created.**

A query with `expand` answered 200 with every expanded navigation empty, as if the client had not
asked for it, until something created a persistence manager: a Metadata request or a save. A
Breeze client fetches metadata before its first query, so it never noticed. Any other first caller
after a restart did: a script, Swagger UI, an integration test host.

*Cause.* `EntityQuery.ApplyExpand` is a no-op until a persistence package plugs in its own, and
`EFPersistenceManager` (and `NHPersistenceManager`, for its query hooks) did that only in its
static constructor.

*Fix, in 8.0.1.* `[BreezeQueryFilter]` and `[BreezeAsyncQueryFilter]` install the provider's
hooks themselves, on the first query over that provider's queryable. The installers are also
public, as `EFQueryHooks.Install()` and `NHQueryHooks.Install()`.

*Workaround on 8.0.0.* Run the persistence manager's static constructor at startup:

```csharp
System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
    typeof(EFPersistenceManager<MyDbContext>).TypeHandle);
```

It is harmless on 8.0.1; remove it once you upgrade.

### Changed

**A query filter attribute keeps the limits of a wider one.**

The filter nearest the action runs the query, so a global
`BreezeQueryFilterAttribute { MaxTake = 1000, MaxDepth = 2 }` never reached an action whose
controller or method carried its own `[BreezeQueryFilter]`, and that attribute's unset limits
meant *unlimited*. A bare `[BreezeQueryFilter]` on a controller lifted the global limits for every
query with a query string.

*In 8.0.1,* a limit an attribute leaves unset comes from the nearest Breeze query filter at a
wider scope (the controller's, then a global one), for `[BreezeQueryFilter]` and
`[BreezeAsyncQueryFilter]` alike. With no wider filter nothing changes, and an explicit `-1` still
means unlimited. If you relied on a bare attribute to lift a global limit, set that limit to `-1`
on it. See [Make the query limits global](https://breeze.github.io/breeze-server-v3/guide/security#make-the-query-limits-global-not-per-controller).

*Workaround on 8.0.0.* Give every attribute both `MaxTake` and `MaxDepth` explicitly.

---

## 8.0.0 — 2026-09-30

The first release of the Breeze .NET Server 8: .NET 8, 9 and 10, with the same package IDs as 7.x.
Every consumer-facing change from 7.x is described in [UPGRADE.md](./UPGRADE.md).
