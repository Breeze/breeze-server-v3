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

## Query actions that take parameters

An action can take its own parameters and still be filtered — the client's query is applied to
whatever the action returns:

[!code-csharp[](../snippets/QueryingSnippets.cs#ParameterizedQuery)]

The client supplies them with `withParameters`, and its own `where` narrows the result further.

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
