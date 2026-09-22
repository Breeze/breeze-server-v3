# Security

Breeze moves two jobs to the client that a hand-written API keeps on the server: composing
queries, and assembling a change-set. That is the point of it — but it means two things now
arrive from a browser that otherwise would not, and **both are untrusted input**.

This page is about what that costs you and what you write to pay it. None of it is exotic; it
is the same discipline as any API. What is different is *where* the checks go, because the
usual place — the shape of the endpoint — no longer constrains much.

> [!IMPORTANT]
> Breeze does nothing about authentication or authorization. `[Authorize]`, policies, claims
> and roles work exactly as they do in any ASP.NET Core application. What this page covers is
> the part that is specific to Breeze: a query and a change-set that the client composed.

---

## Queries

### The `IQueryable` you return is the boundary

This is the single most important thing on the page. The client's query is applied **on top of**
whatever your action returned, so it can only ever narrow the result. Whatever you hand back is
the most any client can see.

So this exposes every order in the database to anyone who can reach the endpoint:

[!code-csharp[](../snippets/SecuritySnippets.cs#UnboundedQuery)]

and this cannot, no matter what query arrives:

[!code-csharp[](../snippets/SecuritySnippets.cs#NamedQuery)]

An action that filters by something the *server* knows — the signed-in user, their tenant, their
role — is often called a **named query**. It is the primary tool here, and it is worth preferring
even where a bare set looks harmless today, because the set that is harmless now is the one
someone adds a sensitive column to later.

Note where `CurrentCustomerId` comes from: the user's claims. Not a route parameter, not a
header, not a property of the entity. A value the client supplies is a value the client chooses.

### Bound what a query may cost

Two properties on the filter attribute, both unlimited by default:

| | What it does | Without it |
|---|---|---|
| `MaxTake` | caps rows, applied after the client's `where` and `orderBy` | a client that omits `take` selects the whole table |
| `MaxDepth` | caps `expand` and `select` depth; returns 400 beyond it | `expand=orders.orderDetails.product` walks the graph in one request |

[!code-csharp[](../snippets/SecuritySnippets.cs#ControllerAttributes)]

These are availability controls, not access controls — they bound the damage a careless or
hostile query can do to your database, but they do not decide who may see what. That is the
previous section's job.

> [!WARNING]
> `MaxTake` applies only to Entity Framework queryables, and not inside `expand` subqueries. An
> action that returns an array or a `List<T>` is outside the cap entirely, and so is the
> collection on the far end of an expand. See [Querying](querying.md#maxtake).

### Filtering sees what projection hides

A subtle one. Leaving a column out of `select` does not stop a client *filtering* on it:

```
?{"where":{"salary":{"gt":100000}},"select":"lastName"}
```

No salary is returned, but the set that comes back is exactly the employees earning over
100,000 — and by moving the threshold a client reads the column a binary digit at a time.

So "the client cannot see it because it is not in the projection" is not true. If a property
must not be readable, keep it out of the `IQueryable` — project to a type that does not have it,
or do not expose that set — rather than relying on what the client asked for.

### Turning the filter off

Not every endpoint should be client-composable. An action that returns a computed report, or one
whose parameters fully determine the result, is better off not accepting a query at all: leave
`[BreezeQueryFilter]` off the controller, or call
<xref:Breeze.AspNetCore.QueryFns.SkipBreezeQueryFilter*> and return a materialized `List<T>`.

---

## Saves

### A save bundle is a claim, not a fact

The bundle a client posts to `SaveChanges` names, for every entity in it:

- the **entity type**, as `Name:#Namespace`,
- the **state** — added, modified or deleted,
- the **key**,
- the **original values**,
- and **every mapped property**.

All of it comes from the browser. `PersistenceManager.SaveChangesAsync(saveBundle)` with no hooks
attached believes all of it.

That is not a flaw to be worked around — it is what lets one request carry a whole screen's worth
of edits across several types. But it does mean the hooks are not a convenience. They are where
authorization lives.

### `BeforeSaveEntity` is the authorization point

[!code-csharp[](../snippets/SecuritySnippets.cs#AuthorizeSave)]

Three things in there are worth pulling out.

**Check the stored value, not the sent one.** For a modified entity the client supplied
`CustomerID` along with everything else, so testing what arrived only confirms that the client
*claims* to own the row. The check has to read what is in the database.

**Set it, do not trust it, on insert.** For a new order the client does not get to choose whose
it is; the server assigns it from the claim.

**Decide what is saveable.** Which brings us to:

### The type name is the client's too

<xref:Breeze.Persistence.PersistenceManager> resolves the type named in the bundle by reflection,
against every loaded assembly that is not a framework assembly. The reachable set is therefore
*your application's entity types* — not "the types this controller is about".

A controller that only ever intends to save orders will still accept a bundle naming any other
persistable type, and save it, unless something says otherwise. For a single type an `is` check
in `BeforeSaveEntity` covers it; for a batch, check the map:

[!code-csharp[](../snippets/SecuritySnippets.cs#AllowedTypes)]

### Every mapped property is written

On a modified entity, Breeze attaches the entity and marks the entry `Modified`, so the row is
written from the JSON that arrived. A property the user must not change is therefore not
protected by being absent from your UI, or read-only in it, or omitted from a form.

Put server-controlled values back, in a hook, every time:

[!code-csharp[](../snippets/SecuritySnippets.cs#ServerControlledFields)]

Prices, discounts, status flags, role and permission columns, audit fields, and any foreign key
that decides ownership all belong in that category.

### Original values are the client's word, so concurrency is not a control

`entityAspect.originalValuesMap` arrives from the client and becomes EF Core's `OriginalValue`
for those properties — which is what EF puts in the `WHERE` clause of the `UPDATE` for a
concurrency token.

A cooperating client sends the values it actually read, and optimistic concurrency does its job:
two users editing the same row do not silently clobber each other. A hostile client sends
whatever makes the update match.

So treat [concurrency conflicts](error-handling.md#concurrency-conflicts) as protection against
*accidents*, which is what they are for. If a row must not be changed by the wrong person, that
is an authorization check in a hook, not a concurrency token.

### Reject by throwing, not by returning `false`

Returning `false` from `BeforeSaveEntity` drops the entity from the save **silently**. The client
receives a successful save result and goes on believing the change was applied.

That is the right behaviour for "this entity does not need saving". It is the wrong behaviour for
"you may not do that", where the client needs to know. Throw an
<xref:Breeze.Persistence.EntityErrorsException> instead — see
[Error handling](error-handling.md#reporting-validation-errors).

---

## Metadata

`GET /breeze/Northwind/Metadata` describes your whole model: entity types, property names, data
types, keys, relationships, maximum lengths and which column is the concurrency token.

That is a design disclosure rather than a data one, and for most applications it is fine — it
describes the same schema the client needs in order to work at all. But it is worth knowing that
the endpoint is a map of your database, and it is an ordinary action: put `[Authorize]` on it, or
on the controller, if anonymous callers should not have it.

---

## The rest is ordinary ASP.NET Core

- **`[Authorize]`** on the controller, with whatever policies you use elsewhere. Breeze actions
  are ordinary actions.
- **CORS.** Configure it as narrowly as your clients allow. The policy in this repository's test
  server reflects any origin and allows credentials, which is why it carries a comment saying not
  to copy it into a real application.
- **Stack traces.** <xref:Breeze.Persistence.BreezeConfig.IncludeStackTraceInErrors> is `false` by
  default. Keep it that way outside development; it is how internals reach a client.
- **The usual database hygiene.** Breeze composes LINQ, so the provider parameterises the values —
  a `where` clause from a client is not a SQL injection vector. That says nothing about raw SQL
  you write yourself elsewhere.

---

## Checklist

| | |
|---|---|
| Every query action returns a set the current user may see, filtered server-side | [above](#the-iqueryable-you-return-is-the-boundary) |
| `MaxTake` and `MaxDepth` set on every `[BreezeQueryFilter]` | [above](#bound-what-a-query-may-cost) |
| Nothing sensitive reachable by a `where`, even if never selected | [above](#filtering-sees-what-projection-hides) |
| A save hook that decides which types may be saved | [above](#the-type-name-is-the-clients-too) |
| A save hook that checks ownership against stored values | [above](#beforesaveentity-is-the-authorization-point) |
| Server-controlled properties reset on every save | [above](#every-mapped-property-is-written) |
| Refusals throw rather than return `false` | [above](#reject-by-throwing-not-by-returning-false) |
| `IncludeStackTraceInErrors` off in production | [above](#the-rest-is-ordinary-aspnet-core) |
| `[Authorize]` where it belongs, including on `Metadata` if needed | [above](#metadata) |

## See also

- [Querying](querying.md) — the filter, and what its limits do
- [Saving](saving.md) — the hooks, in full
- [Error handling](error-handling.md) — telling a client what it may not do
