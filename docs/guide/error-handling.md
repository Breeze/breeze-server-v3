# Error handling

When a save fails, the client needs more than a 500. It needs to know **which entity** failed and
**why**, so it can attach the message to the right record rather than showing one dialog for the
whole batch.

<xref:Breeze.AspNetCore.GlobalExceptionFilter> turns an exception into a response that carries that
detail.

## Install the filter

[!code-csharp[](../snippets/ErrorHandlingSnippets.cs#AddFilter)]

Without it, an <xref:Breeze.Persistence.EntityErrorsException> reaches the client as an
unremarkable 500 and the per-entity detail is lost.

## The response

An **RFC 9457 problem details** document, sent as `application/problem+json`:

```json
{
  "type": "https://breeze.github.io/problems/entity-errors",
  "title": "Forbidden",
  "status": 403,
  "detail": "Validation errors",
  "entityErrors": [
    {
      "ErrorName": "RequiredValidator",
      "EntityTypeName": "Customer:#Northwind.Models",
      "KeyValues": ["729de505-ea6d-4cdf-89f6-0360ad37bde7"],
      "PropertyName": "CompanyName",
      "ErrorMessage": "CompanyName is required"
    }
  ]
}
```

`type`, `title`, `status` and `detail` are the RFC's own members. `entityErrors` is an extension
member — RFC 9457 §3.2 permits them and requires consumers to ignore ones they do not recognize —
because nothing standard can say *which instance of which type* failed, which is exactly what the
client needs.

`KeyValues` is how the client finds the entity it already holds in its cache, and `PropertyName` is
how the error lands on the right field. Omit `PropertyName` and the error attaches to the entity as
a whole.

### Problem types

| `type` | Means |
|---|---|
| `.../problems/entity-errors` | per-entity errors are present |
| `.../problems/concurrency-conflict` | an optimistic concurrency conflict |
| `.../problems/server-error` | an unhandled 500 |
| `about:blank` | the status code is the whole story |

A client matches on `type`, not on message text, which is what makes the distinction stable.

## Reporting validation errors

Throw an `EntityErrorsException` with one `EntityError` per problem:

[!code-csharp[](../snippets/ErrorHandlingSnippets.cs#ThrowEntityErrors)]

It defaults to **403 Forbidden**; set `StatusCode` for something else. The
[DataAnnotationsValidator](saving.md#validation) throws this for you from your model's annotations.

> [!TIP]
> This is how you *reject* a change. Returning `false` from `BeforeSaveEntity` drops the entity
> from the save silently, and the client goes on believing it was saved.

## Concurrency conflicts

<xref:Breeze.Persistence.ConcurrencyErrorsException> is an `EntityErrorsException` that returns
**409 Conflict** with its own problem type. A persistence manager raises it in place of whatever the
ORM threw, so EF Core's `DbUpdateConcurrencyException` and NHibernate's `StaleObjectStateException`
reach the client identically.

It matters that this is distinguishable, because a duplicate key is *also* 409 and the recovery
differs — re-read and merge for a conflict, change the data for a duplicate.

It carries one error per conflicting row, so the client can mark exactly the records the user must
look at:

[!code-csharp[](../snippets/ErrorHandlingSnippets.cs#ThrowConcurrency)]

## Database errors

A duplicate key or a foreign-key violation should be **409 Conflict**, not 500 — it is the client's
data that is wrong, not the server. Recognizing one means reading a provider-specific error number,
which the filter deliberately does not know how to do. Supply the mapping:

[!code-csharp[](../snippets/ErrorHandlingSnippets.cs#DbExceptionMapper)]

<xref:Breeze.AspNetCore.DbExceptionMappers.SqlServer*> recognizes SQL Server's 2627, 2601 and 547.
For another database, write the equivalent — PostgreSQL uses SQLSTATE 23505 and 23503:

```csharp
StatusCodeForException = ex => ex.GetBaseException() is PostgresException { SqlState: "23505" or "23503" }
  ? HttpStatusCode.Conflict : null
```

Return `null` to accept the default of 500.

## Stack traces

Off by default. Turn them on for development only:

[!code-csharp[](../snippets/ErrorHandlingSnippets.cs#StackTraces)]

## Older clients

<xref:Breeze.Persistence.BreezeConfig.IncludeLegacyErrorMembers> is `true` by default, which adds
the pre-3.0 `Code`, `Message` and `EntityErrors` members alongside the RFC 9457 ones. A
`breeze-client` 2.x application therefore reads the new document unchanged — **no client upgrade
has to be scheduled** to adopt this server.

Set it to `false` once every client is 3.0 or later, and the document becomes RFC 9457 and nothing
else. Entity errors survive as the `entityErrors` extension member either way.

## See also

- [Saving](saving.md)
- [The PersistenceManager](persistence-manager.md)
