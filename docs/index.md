---
_layout: landing
---

# Breeze Server for .NET

**Breeze** is a library from [IdeaBlade](https://www.ideablade.com/) that helps you manage data in rich client
applications. This site documents the .NET server-side packages, version 8.0.0, which target
.NET 8, 9 and 10 on ASP.NET Core.

> [!TIP]
> **Looking for the client?** The Breeze client documentation - guides and the TypeScript API reference - is at
> **[breeze.github.io/breeze-client-v3](https://breeze.github.io/breeze-client-v3/)**. Start with
> [Using a Breeze .NET server](https://breeze.github.io/breeze-client-v3/server/dotnet) for how a client talks
> to the server documented here.

## What the Breeze server does

A Breeze client holds a cache of entities. It composes queries against them, tracks what the user changes, and
saves everything in one go. None of that works against an ordinary REST API, because an ordinary REST API
answers only the questions its author thought of in advance, and accepts only the shapes its author declared.

The server packages close that gap. They add three things to an ASP.NET Core application:

### 1. They describe your model to the client

`PersistenceManager.Metadata()` reads your Entity Framework Core or NHibernate mapping and returns it as JSON:
entity types, their properties and data types, keys, relationships, which properties are required, how long a
string may be, and which column is the concurrency token.

The client builds its own model from that document. It is what lets the client create a valid entity, follow
`customer.orders[0].orderDetails`, know that a change is a change, and reject a too-long `CompanyName` before
a request is ever sent. You write no schema twice: the mapping you already have is the description.

### 2. They run queries the client composed

The client builds a query — filter, sort, page, project, include related types — and sends it as JSON in the URL.
`[BreezeQueryFilter]` applies it to the `IQueryable` your action returned, before it is serialized. So it reaches
the database as SQL, with the filter in the `WHERE` clause rather than in memory afterwards.

The practical effect is on how many endpoints you write:

|  | without Breeze | with Breeze |
|---|---|---|
| query endpoints | one per question - `GetCustomers`, `GetCustomersByCity`, `GetCustomersByCityPaged` … | one per entity set, returning `IQueryable<T>` |
| a new filter the UI needs | a new endpoint, deployed | nothing; the client already can |

That is also why [Querying](guide/querying.md) spends most of its length on `MaxDepth` and `MaxTake`. A query
that arrives from a browser is user input, and the point of the feature is that you did not enumerate what it
may ask for.

### 3. They apply a whole change-set at once

`SaveChanges` receives one document containing every pending change — added, modified and deleted entities, of
as many types as the user touched, each carrying its state and its original values. The `PersistenceManager`
turns that back into real entities attached in the right states, saves them in one transaction, and reports
back the keys the database generated in place of the temporary ones the client invented.

So a screen that adds an order, edits two of its lines and deletes a third is one request that succeeds or
fails as a unit — not four, in an order you had to choose, with partial failure to unpick.

### And they translate errors

A failed save comes back as an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details document
carrying *which entity* failed and *why*, so the client can attach the message to the offending record rather
than showing one dialog for the batch. Optimistic concurrency conflicts arrive with a stable problem type,
identical whether Entity Framework Core or NHibernate raised them. See
[Error handling](guide/error-handling.md).

### What it does not do

It is a library, not a framework, and it is deliberately small:

- **It does not replace your `DbContext` or your mappings.** It reads them. Your entities, configuration and
  migrations are unchanged.
- **Your controllers are ordinary controllers.** Nothing is scaffolded or generated; see
  [Getting started](guide/getting-started.md) for the whole of one.
- **It does nothing about authentication or authorization.** That is ASP.NET Core's job as usual. What is
  yours to write is *what a given user may query and change* - the save hooks in
  [Saving](guide/saving.md) are where that belongs, because a save bundle names its own entity types and
  original values and is therefore untrusted input.
- **It is not OData.** The query format is Breeze's own, and the client speaks it out of the box.

---

This site has two halves: a hand-written **[Guide](guide/getting-started.md)**, and an
**[API reference](xref:Breeze.Persistence)** generated from the XML doc comments in
[breeze-server-v3](https://github.com/Breeze/breeze-server-v3). The *API reference* tab above
reaches the whole of it; the table below starts at each package.

## Start here

| | |
|---|---|
| [Getting started](guide/getting-started.md) | from an empty project to a working Breeze endpoint |
| [The PersistenceManager](guide/persistence-manager.md) | the server side of Breeze, and its lifetime |
| [Querying](guide/querying.md) | the query filter, and limiting what clients may ask for |
| [Saving](guide/saving.md) | interceptors, transactions and key mappings |
| [Security](guide/security.md) | the query and the change-set both come from the browser |
| [Metadata](guide/metadata.md) | what the client is told about your model |
| [Error handling](guide/error-handling.md) | returning errors a client can act on |

## Packages

| Package | Namespace | Purpose |
|---|---|---|
| Breeze.AspNetCore.NetCore | [Breeze.AspNetCore](xref:Breeze.AspNetCore) | ASP.NET Core integration - `[BreezeQueryFilter]` and friends |
| Breeze.Persistence.EFCore | [Breeze.Persistence.EFCore](xref:Breeze.Persistence.EFCore) | Entity Framework Core support - start at <xref:Breeze.Persistence.EFCore.EFPersistenceManager`1> |
| Breeze.Persistence.NH | [Breeze.Persistence.NH](xref:Breeze.Persistence.NH) | NHibernate support |
| Breeze.Persistence | [Breeze.Persistence](xref:Breeze.Persistence) | Shared persistence layer - <xref:Breeze.Persistence.PersistenceManager> |
| Breeze.Core | [Breeze.Core](xref:Breeze.Core) | Query parsing and JSON serialization |

A typical EF Core application installs the first two; the last two are pulled in automatically.

The reference is built from the `net10.0` target. The public API is the same on `net8.0` and `net9.0`, except
that each target references the matching major version of Entity Framework Core.

## More documentation

- **Breeze client documentation** - guides and the TypeScript API reference - is at
  [breeze.github.io/breeze-client-v3](https://breeze.github.io/breeze-client-v3/), built from
  [breeze-client-v3](https://github.com/Breeze/breeze-client-v3).
- **Upgrading from 7.x** - [UPGRADE.md](https://github.com/Breeze/breeze-server-v3/blob/master/UPGRADE.md) in
  breeze-server-v3.
- General Breeze documentation is at [breeze.github.io](http://breeze.github.io/doc-net/).
