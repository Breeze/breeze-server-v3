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

### Error responses are RFC 9457 problem details

An error response is now a [problem details](https://www.rfc-editor.org/rfc/rfc9457) document,
sent as `Content-Type: application/problem+json`:

```json
{
  "type":   "https://breeze.github.io/problems/entity-errors",
  "title":  "Forbidden",
  "status": 403,
  "detail": "Order validation failed",

  "Code":    403,
  "Message": "Order validation failed",
  "EntityErrors": [ ... ]
}
```

**Existing clients keep working.** The capitalised members are what Breeze sent before, and they
are still there by default. RFC 9457 §3.2 permits extension members and requires consumers to
ignore ones they do not recognise, so the document is conformant with them present — there is no
flag to coordinate and no client upgrade to schedule.

Three things did change:

| | |
|---|---|
| **The stack trace is no longer sent.** | It named source files, line numbers and the build machine's directory layout, to every caller. `BreezeConfig.Instance.IncludeStackTraceInErrors = env.IsDevelopment();` gets it back where you want it. |
| **`Code` now holds the real status.** | It was left at `0` for anything that was not an `EntityErrorsException`, while the HTTP status said 500. |
| **The content type is `application/problem+json`.** | It was `application/json`. |

Set `BreezeConfig.Instance.IncludeLegacyErrorMembers = false` to drop `Code`, `Message` and
`EntityErrors` once every client reads the RFC 9457 members. Entity errors then move to a
lowercase `entityErrors` extension member, which breeze-client 3.0 also reads.

### Mapping exceptions to status codes

`GlobalExceptionFilter.StatusCodeForException` turns an exception into a status code; anything it
does not map stays 500, as before. It is off unless you set it. For SQL Server there is a
ready-made mapping that turns a duplicate-key or foreign-key violation into 409 Conflict:

```csharp
o.Filters.Add(new GlobalExceptionFilter {
  StatusCodeForException = DbExceptionMappers.SqlServer
});
```

409 says more than a blanket 500 - the request conflicts with the current state of the resource -
and unlike a 500 a client will not retry it.

### Mapping exceptions for other providers

`StatusCodeForException` is a `Func<Exception, HttpStatusCode?>` — return a status to override the
default, or `null` to leave it alone. Only SQL Server ships with a mapping, because these codes
belong to the provider and no Breeze package references a database client library. Writing one for
another provider is a single expression, and there are three things to get right whichever it is:

1. **Unwrap first.** EF Core wraps the provider's exception in a `DbUpdateException`, NHibernate in
   a `GenericADOException`. `GetBaseException()` reaches the innermost one either way.
2. **Match the exception type, not just the number.** Plenty of unrelated exceptions have a
   `Number` property.
3. **Return `null` for anything you do not recognise**, so unrelated failures keep their 500.

PostgreSQL, via Npgsql — the code is `SqlState`, a five-character SQLSTATE:

```csharp
using Npgsql;

static HttpStatusCode? Postgres(Exception ex) =>
  ex.GetBaseException() is PostgresException { SqlState: "23505" or "23503" }
    ? HttpStatusCode.Conflict : null;   // 23505 unique_violation, 23503 foreign_key_violation
```

MySQL and MariaDB, via MySqlConnector:

```csharp
using MySqlConnector;

static HttpStatusCode? MySql(Exception ex) =>
  ex.GetBaseException() is MySqlException { Number: 1062 or 1451 or 1452 }
    ? HttpStatusCode.Conflict : null;   // 1062 duplicate entry, 1451/1452 foreign key
```

Oracle, via Oracle.ManagedDataAccess:

```csharp
using Oracle.ManagedDataAccess.Client;

static HttpStatusCode? Oracle(Exception ex) =>
  ex.GetBaseException() is OracleException { Number: 1 or 2291 or 2292 }
    ? HttpStatusCode.Conflict : null;   // ORA-00001 unique, ORA-02291/02292 foreign key
```

SQLite, via Microsoft.Data.Sqlite, reports both through `SqliteErrorCode` 19:

```csharp
using Microsoft.Data.Sqlite;

static HttpStatusCode? Sqlite(Exception ex) =>
  ex.GetBaseException() is SqliteException { SqliteErrorCode: 19 }
    ? HttpStatusCode.Conflict : null;   // SQLITE_CONSTRAINT
```

`DbExceptionMappers.SqlServer` does the same thing by type name and reflection rather than by
referencing `Microsoft.Data.SqlClient`. That is a cost Breeze pays so that its packages stay free
of provider dependencies; your own application already references its provider, so match on the
concrete type as above.

Conflicts are not the only thing worth mapping. The delegate sees every exception the filter
catches, so your own domain exceptions can carry a status too, and mappers compose:

```csharp
StatusCodeForException = ex =>
  ex is NotFoundException       ? HttpStatusCode.NotFound
  : ex is UnauthorizedException ? HttpStatusCode.Forbidden
  : DbExceptionMappers.SqlServer(ex);
```

One caveat about the message. A raw provider message ("The INSERT statement conflicted with the
FOREIGN KEY constraint `FK_Order_Customer`…") names your tables and constraints to the caller. That
is what reaches the client as the problem document's `detail`, and it is the same trade as
`IncludeStackTraceInErrors` — fine internally, worth catching and rethrowing with a message of your
own on a public API.

### Concurrency conflicts are isolated

An optimistic concurrency conflict now reaches the client as **409 Conflict** with the RFC 9457
type `https://breeze.github.io/problems/concurrency-conflict`, whichever ORM is underneath. It was
a 500 carrying the ORM's own words, which a client could only recognize by matching on the text.

`ConcurrencyErrorsException` is what both persistence managers raise:

| ORM | was | now |
|---|---|---|
| EF Core | `DbUpdateConcurrencyException`, rethrown as 500 | caught in `SaveChangesCore`, converted using `e.Entries` |
| NHibernate | `StaleObjectStateException`, rethrown as 500 | caught in `SaveChangesCore`, converted using `EntityName` and `Identifier` |

It subclasses `EntityErrorsException`, so it also carries one `EntityError` per conflicting row -
entity type name and key values, `ErrorName` of `ConcurrencyError`, and no property name, since the
row is stale as a whole. A breeze client resolves each back to the entity it already holds and
attaches a validation error to it, which is what lets an application point at the records that went
stale instead of failing the save with one message.

The message is Breeze's, not the ORM's: *"The save failed because 1 record was changed or deleted
by another user after it was read."*

**This is automatic.** Nothing to register, no flag. It does need a concurrency column to detect
the conflict with - `[ConcurrencyCheck]`, or a `rowversion`/`timestamp` - which Breeze already
reports in metadata as `concurrencyMode: "Fixed"`.

NHibernate's plain `StaleStateException`, raised for a batched flush, says a row was stale but not
which one. That still produces the status and the problem type, just with no per-entity error.

### Naming your own problem types

`EntityErrorsException.ProblemType` is new and optional. Set it when the status code alone does not
say what went wrong, and the filter sends it as the problem document's `type` instead of choosing
one:

```csharp
throw new EntityErrorsException("That order is already shipped", errors) {
  StatusCode = HttpStatusCode.Conflict,
  ProblemType = "https://example.com/problems/order-already-shipped"
};
```

Left null - which is every existing throw site - the filter behaves exactly as before. This is what
separates a concurrency conflict from a duplicate key, since both are 409 and the client recovers
from them differently.

### Otherwise

**No other API changes.** 8.0 is a cleanup and modernization release, not a rewrite.
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
