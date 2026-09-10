# Upgrading to Breeze .NET Server 8.0

A running list of everything that affects **applications using the Breeze .NET packages**.
Kept current as v8 develops. Items marked *planned* are decided but not yet implemented.

For changes that only matter if you work on Breeze itself, see [CHANGES-DEV.md](./CHANGES-DEV.md).

> **Status: 8.0 is in development.** Nothing here is released yet.

---

## 1. Supported .NET versions

**Done.** The packages now target **net8.0, net9.0 and net10.0**.

7.5.x multi-targeted net5.0 through net10.0. .NET 5, 6 and 7 are all out of support, so
they are dropped. **If you are on one of those, stay on 7.5.2** — it remains on NuGet and
is unaffected.

Package IDs are unchanged:

- `Breeze.AspNetCore.NetCore`
- `Breeze.Persistence.EFCore`
- `Breeze.Persistence.NH`
- `Breeze.Persistence`
- `Breeze.Core`

The EF Core version is still pinned to the matching runtime — EF Core 8 on net8.0, 9 on
net9.0, 10 on net10.0.

## 2. API changes

**None so far.** 8.0 is a cleanup and modernization release, not a rewrite.
`PersistenceManager`, `[BreezeQueryFilter]`, `EFPersistenceManager`, `SaveResult`,
`SaveOptions`, the metadata format and the JSON wire format are all unchanged.

If you hit a behavioural difference, it is a bug — please file an issue.

## 3. JSON serialization

**Newtonsoft.Json is retained.** Moving to `System.Text.Json` would change the JSON shape
at the edges — `$type`/`$id` handling, `DateTimeOffset` formatting, dictionary key casing —
and Breeze clients depend on that shape. It is a wire-format change, not a cleanup, and is
out of scope for 8.0.

## 4. Nullable reference types

***Planned.*** `<Nullable>enable</Nullable>` will be turned on per project. This changes
only the annotations in the public API surface; it may produce new warnings in your code if
you compile against the packages with nullable enabled. No runtime behaviour changes.

## 5. If you run the Breeze test suite

This only affects contributors and anyone who runs the cross-tier tests.

**The three test databases are now one.** `NorthwindIB`, `InheritanceContext` and
`ProduceTPH` are consolidated into a single **`BreezeTestDb`**, behind a single connection
string.

**The database is now a SQL script, not binaries.** The SQL Server 2008-era `.mdf`/`.ldf`
files are replaced by `tests/Databases/BreezeTestDb.sql`. Create it with:

```bash
sqlcmd -S . -E -Q "CREATE DATABASE BreezeTestDb"
sqlcmd -S . -E -d BreezeTestDb -f 65001 -i tests/Databases/BreezeTestDb.sql
```

No attach step, no file-permission workaround, and no one-way file upgrade.

`appsettings.json` now has one connection string named `BreezeTestDb` in place of the three
named `InheritanceContext`, `NorthwindIB_CF` and `ProduceTPH`.
