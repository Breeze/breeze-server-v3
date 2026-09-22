# Querying

A Breeze client builds a query on the client and sends it as JSON in the URL. The server's job is
to turn that into a `WHERE`, `ORDER BY`, `SKIP`/`TAKE`, `SELECT` and `INCLUDE` against your
`IQueryable`, so the database does the work rather than the web server.

<xref:Breeze.AspNetCore.BreezeQueryFilterAttribute> is what does it.

## The filter

Put it on the controller class. Every action that returns `IQueryable` or `IEnumerable` is then
queryable:

[!code-csharp[](../snippets/QueryingSnippets.cs#FilteredController)]

The action returns the *unfiltered* set. The filter runs after the action, reads the query off the
request, applies it to what was returned, and executes it.

```
GET /breeze/Northwind/Customers?{"where":{"city":"London"},"take":10}
```

becomes `SELECT TOP 10 ... WHERE City = 'London'`.

> [!NOTE]
> Return `IQueryable<T>`, not `List<T>`. A materialized list can still be filtered — LINQ to
> Objects — but only after every row has come back from the database.

## Sync or async

<xref:Breeze.AspNetCore.BreezeAsyncQueryFilterAttribute> is the same filter with async execution
and cancellation.

| | `[BreezeQueryFilter]` | `[BreezeAsyncQueryFilter]` |
|---|---|---|
| execution | synchronous | `await`ed |
| cancellation | no | yes, via `CatchCancellations` |
| `MaxDepth`, `MaxTake`, `UsePost` | yes | yes |

The async one frees the request thread while the database works, which matters under load. The
sync one exists because of [efcore#18221](https://github.com/dotnet/efcore/issues/18221); the
attribute's own remarks say so.

[!code-csharp[](../snippets/QueryingSnippets.cs#AsyncFilterAttribute)]

With `CatchCancellations`, a client that gives up mid-query gets an empty result with status
`499` rather than an exception — `CancellationStatusCode` changes the code.

## Limiting what a client may ask for

A query arrives from a browser, so it is user input. Two properties bound it.

### MaxTake

[!code-csharp[](../snippets/QueryingSnippets.cs#MaxTakeAttribute)]

Adds `Take(1000)` when the client asked for more, or for nothing at all. Default `-1`, unlimited —
which means a client that forgets `.take()` selects the whole table.

This is not the same as a `Take()` in the action: `MaxTake` is applied **after** the client's
`where` and `orderBy`, so it caps the page rather than the candidate set.

> [!WARNING]
> `MaxTake` only applies to Entity Framework queryables, and it does not reach inside `expand`
> subqueries. Returning an array or a `List` from an action puts it outside this cap.

### MaxDepth

[!code-csharp[](../snippets/QueryingSnippets.cs#MaxDepthAttribute)]

Caps how far `select` and `expand` may reach, and returns **400 Bad Request** when a query goes
deeper. Default `-1`, unlimited.

On `IQueryable<Customer>`:

| `MaxDepth` | allows | rejects |
|---|---|---|
| `0` | `select=companyName` | `expand=orders`, `select=orders` |
| `1` | `expand=orders` | `expand=orders.orderDetails` |
| `2` | `expand=orders.orderDetails` | a third level |

Depth is what keeps one request from pulling a large part of the database through a chain of
navigation properties.

## Named queries with parameters

Not every endpoint is a bare entity set. An action that takes its own parameters and decides for
itself what to return is usually called a **named query**: the client asks for it by action name
rather than by resource.

[!code-csharp[](../snippets/QueryingSnippets.cs#ParameterizedQuery)]

The parameters are bound by ordinary ASP.NET Core model binding — there is nothing Breeze-specific
about them. The client supplies them with `withParameters`:

```ts
EntityQuery.from('CustomersStartingWith').withParameters({ companyName: 'C' });
```

### It is still composable

This is the part worth understanding, because it is what makes a named query the right default
rather than a compromise.

The action returned an `IQueryable`, which is an *unexecuted expression tree*. The filter appends
the client's query to it, and only then does anything reach the database. So the client can go on
building:

```ts
EntityQuery.from('CustomersStartingWith')
  .withParameters({ companyName: 'C' })
  .where('fax', '!=', null)
  .orderBy('companyName')
  .skip(20).take(10)
  .inlineCount(true);
```

Your `StartsWith`, the client's `fax != null`, the ordering, the paging and the count all end up
in **one SQL statement**. The parameter narrows first because it is already in the tree; everything
the client sent is layered on top and can only narrow further.

Return a `List<T>` instead and the parameter still works, but the composition does not:

[!code-csharp[](../snippets/QueryingSnippets.cs#NotComposable)]

| | `IQueryable<T>` | `List<T>` |
|---|---|---|
| where does the client's `where` run? | in the database | in memory, after the fetch |
| rows fetched for `.take(10)` | 10 | every row matching the parameter |
| `MaxTake` applies | yes | no — it only acts on EF queryables |
| `inlineCount` | a `COUNT` in the database | a `.Count()` on the list |

The difference is invisible until the table is large, and then it is the whole story.

> [!TIP]
> This is also why a named query is the primary security tool. The action fixes the most a client
> can ever see, and composition means the client loses nothing by being confined to it — it can
> still filter, sort and page exactly as it would against a bare entity set. See
> [Security](security.md#the-iqueryable-you-return-is-the-boundary).

### Parameter shapes

Anything model binding understands from a query string:

| Client | Server |
|---|---|
| `withParameters({ companyName: 'C' })` | `string companyName` |
| `withParameters({ cities: ['London', 'Paris'] })` | `[FromQuery] string[] cities` |
| `withParameters({ CompanyName: 'C', City: 'London' })` | `[FromQuery] CustomerQuery qbe` |

An array arrives as `cities[0]=London&cities[1]=Paris`:

[!code-csharp[](../snippets/QueryingSnippets.cs#ArrayParameter)]

A query-by-example object binds from **flat, top-level** parameters named after its properties —
`CompanyName=C&City=London`, not `qbe.CompanyName=C`. The parameter name on the server is not part
of what the client sends:

[!code-csharp[](../snippets/QueryingSnippets.cs#ObjectParameter)]

> [!NOTE]
> Zero, `null` and empty-string parameters are worth a test of your own. An absent parameter and
> an empty one are not the same thing to model binding, and which one a client sends depends on
> how it built the object.

### Where the parameters sit in the URL

The client puts the Breeze query first and appends the parameters after it:

```
GET /breeze/Northwind/CustomersStartingWith?{"where":{"fax":{"ne":null}}}&companyName=C
```

That order is not decorative. With the default configuration the server looks for the JSON
**immediately after the `?`** and ignores it otherwise, so a URL built by hand — in a test, or with
curl — must put the JSON first or the query is silently dropped while the parameters still bind.
An `&` inside a quoted JSON string is handled and does not end the query.

Setting <xref:Breeze.Persistence.BreezeConfig.QueryParamName> makes the JSON a named parameter
instead, and ordering stops mattering. See
[Where the query lives in the URL](#where-the-query-lives-in-the-url).

> [!NOTE]
> On the client, a named query often cannot be matched to an entity type by its resource name, and
> `where` needs the type to validate property names against. `.toType('Customer')` supplies it.

## Long queries: UsePost

A complex query can outgrow the URL length a server or proxy will accept. `UsePost` reads it from
the request body instead:

[!code-csharp[](../snippets/QueryingSnippets.cs#UsePostAttribute)]

There is a cost — the body has to be read and buffered — so put it on endpoints that need it
rather than on every controller. If model binding has already consumed the body, it must be
rewound before the filter can read it.

## Opting out for one action

<xref:Breeze.AspNetCore.QueryFns.SkipBreezeQueryFilter*> turns the filter off for the current
request, for an action on a filtered controller that returns something the filter should not touch:

[!code-csharp[](../snippets/QueryingSnippets.cs#SkipFilter)]

To apply a query by hand instead — to inspect or post-process the result —
<xref:Breeze.AspNetCore.QueryFns.ApplyBreezeQuery*> and
<xref:Breeze.AspNetCore.QueryFns.ApplyBreezeWhere*> are extension methods on `ControllerBase`.

## Loading several lookup tables in one request

Most applications open onto a screen that needs a dozen small reference lists — regions,
categories, statuses, roles, currencies — before it can render a single dropdown. Fetching them
one resource at a time costs a round trip each, and on a slow connection that is the whole of
the startup delay.

One action can return them all. Return an object whose properties are the sets:

[!code-csharp[](../snippets/QueryingSnippets.cs#Lookups)]

The client asks for it like any other resource, and every entity in the bag lands in its cache:

```ts
await EntityQuery.from('Lookups').using(em).execute();
// Region, Territory and Category entities are all cached now
```

Breeze does not treat the wrapper as an entity. Each nested object carries its `$type`, which is
how the client recognises the entities inside and merges them. The client side of this is in
[A bag of lookups](https://breeze.github.io/breeze-client-v3/query/examples#a-bag-of-lookups).

### The return type decides whether the filter applies

`object` is the right return type, and deliberately so. The filter looks for an `IQueryable` or an
`IEnumerable` in the result; an anonymous object is neither, so the bag passes through untouched.

Return `IEnumerable<object>` — a list holding one bag — and the filter *does* engage, applying the
client's query to the outer one-element list rather than to anything inside it. It works, but the
query does nothing useful. Prefer `object`.

> [!WARNING]
> Because the filter does not engage, **`MaxTake` and `MaxDepth` do not apply here**. This
> endpoint returns each set in full, however large it has become. That is exactly what you want
> for a handful of small static tables and exactly what you do not want when somebody adds
> `Orders` to the bag a year from now. See [Security](security.md#bound-what-a-query-may-cost).

### Run the queries before serialising

In the version above the properties are still `IQueryable`, so nothing touches the database until
the serializer enumerates them — one query per set, part-way through writing the response. If one
fails there, the response has already begun and the client gets a truncated body rather than an
error. The filter takes the same precaution for `select` and `expand`, for the same reason.

Materialise them yourself and that problem goes away, along with any doubt about when the work
happens:

[!code-csharp[](../snippets/QueryingSnippets.cs#LookupsMaterialized)]

It also gives you somewhere to assert that these tables really are small.

> [!NOTE]
> "One pass" means one round trip from the browser, which is the expensive part. It is still one
> SQL query per set, run sequentially on the one connection — Breeze does not combine them.

### Trim the anonymous type out of the payload

`UpdateWithDefaults` sets `TypeNameHandling.Objects`, so Newtonsoft writes a `$type` for the
anonymous wrapper as well as for the entities — an assembly-qualified name the client has no use
for. <xref:Breeze.Core.NoAnonSerializationBinder> drops it:

[!code-csharp[](../snippets/QueryingSnippets.cs#AnonBinder)]

Optional — the bag works either way — but it shortens every projection response, and it keeps your
assembly name out of them.

### Cache them

Lookup data is the same for every request and changes rarely, which makes it the easiest caching
win available: `[ResponseCache]`, an `IMemoryCache` around the materialised lists, or an ETag.

> [!WARNING]
> Cache per tenant, or not at all, if the lists differ by tenant or by user. With a
> [global query filter](security.md#put-the-read-boundary-in-the-orm) the sets are already scoped
> to the caller, so a cache that ignores that will serve one tenant's data to another.

## Inline count

When the client asks for a total alongside a page, the response becomes a
<xref:Breeze.Core.QueryResult> — `{ "Results": [...], "InlineCount": 235 }` — instead of a bare
array. The filter does this for you; nothing in the action changes. The client unwraps it.

## Where the query lives in the URL

By default the JSON follows the question mark directly:

```
?{"where":{"city":"London"}}
```

Some proxies dislike that. <xref:Breeze.Persistence.BreezeConfig.QueryParamName> moves it into a
named parameter — set it to `"bq"` and the client sends `?bq={"where":...}`. It must match what
the client sends, so change both sides together.

## See also

- [The PersistenceManager](persistence-manager.md)
- [Saving](saving.md)
