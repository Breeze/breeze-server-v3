# Getting started

This page takes an ASP.NET Core project from nothing to a working Breeze endpoint that a
`breeze-client` application can query and save against.

## Install

```bash
dotnet add package Breeze.AspNetCore.NetCore
dotnet add package Breeze.Persistence.EFCore
```

Two packages for an Entity Framework Core application. `Breeze.Persistence` and `Breeze.Core`
come with them and are not installed directly. For NHibernate, swap the second for
`Breeze.Persistence.NH`.

**Requirements:** .NET 8, 9 or 10, and ASP.NET Core. Version 8.0 targets all three; the EF Core
major version is pinned to the matching runtime, so `net10.0` gets EF Core 10. For .NET 5, 6 or 7 —
all out of support — use 7.5.2 from the
[old repo](https://github.com/Breeze/breeze.server.net).

## Configure JSON

Breeze serializes with **Newtonsoft.Json**, not `System.Text.Json`, and it needs particular
settings: reference handling for object graphs, type names so the client can tell what each JSON
object is, and a specific date format.
<xref:Breeze.Core.JsonSerializationFns.UpdateWithDefaults*>, in `Breeze.Core`, applies them.

[!code-csharp[](../snippets/GettingStarted.cs#ConfigureJson)]

> [!IMPORTANT]
> This is not optional. Without it the client receives JSON it cannot turn into entities —
> circular references throw, and results arrive with no `$type` for Breeze to match against
> its metadata.

The second parameter turns on camel casing of the JSON itself, and the third writes enums as
integers rather than strings. Both default to `false`. Leave `camelCasing` off: the client
camel-cases property names for you through its `NamingConvention`, so turning it on here as well
means the names are translated twice. See [Metadata](metadata.md#naming).

## Add a DbContext

An ordinary EF Core `DbContext` — Breeze adds nothing to it.

[!code-csharp[](../snippets/GettingStarted.cs#AddDbContext)]

## Write a PersistenceManager

<xref:Breeze.Persistence.EFCore.EFPersistenceManager`1> is the piece that produces metadata and
applies a save bundle. A subclass per `DbContext` is the usual arrangement, and it is where save
interceptors live later.

[!code-csharp[](../snippets/PersistenceManagerSnippet.cs#PersistenceManager)]

That is enough to query and save. See [The PersistenceManager](persistence-manager.md).

## Write a controller

[!code-csharp[](../snippets/ControllerSnippet.cs#Controller)]

Three kinds of member, and that is the whole shape of a Breeze controller:

| Member | Job |
|---|---|
| `Metadata()` | describes the entity types, so the client can build its own model |
| `SaveChanges()` | takes every pending change in one request and applies it |
| each `IQueryable<T>` action | exposes one entity set for querying |

`[BreezeQueryFilter]` on the class is what makes the `IQueryable` actions queryable: it reads the
filter, order, paging, `select` and `expand` instructions off the request and applies them to
whatever the action returned, before it is serialized. Without the attribute each action returns
its whole table. See [Querying](querying.md).

> [!NOTE]
> The actions return `IQueryable<T>`, not `List<T>` or `IActionResult`. Returning a materialized
> list means the database sees no `WHERE` clause — the filter can still be applied in memory, but
> every row has already been fetched.

## Point a client at it

With the route above, the service root is `/breeze/Northwind` and the endpoints fall out of
`[action]`:

| | |
|---|---|
| `GET /breeze/Northwind/Metadata` | metadata |
| `GET /breeze/Northwind/Customers?...` | a query |
| `POST /breeze/Northwind/SaveChanges` | a save |

```ts
import { EntityManager } from 'breeze-client';

const em = new EntityManager('/breeze/Northwind');
```

Those names are what the client's defaults expect, so a `breeze-client` application needs no
configuration to talk to this server. The `Metadata`/`SaveChanges` names are the convention; the
`IQueryable` action names become the resource names a query targets
(`EntityQuery.from('Customers')`).

## Check it works

```bash
curl http://localhost:5000/breeze/Northwind/Metadata
curl 'http://localhost:5000/breeze/Northwind/Customers?$top=1'
```

The first returns a JSON metadata document; the second, one customer.

## Where to go next

- [The PersistenceManager](persistence-manager.md) — what it does, and its lifetime
- [Querying](querying.md) — the query filter, and limiting what clients may ask for
- [Saving](saving.md) — interceptors, transactions and key mappings
- [Metadata](metadata.md) — what is in the document and how to change it
- [Error handling](error-handling.md) — returning validation and concurrency errors a client understands
