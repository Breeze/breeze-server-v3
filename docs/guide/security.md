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

## Hardening

Everything above is a check you have to remember. That is the weakness: the breach is rarely a
*wrong* check, it is a *missing* one, in an endpoint somebody added last Tuesday. So the
measures worth doing first are the ones that change what happens when a check is forgotten.

Two of them do most of the work, and both are configured once rather than per endpoint.

### Put the read boundary in the ORM

A named query secures the set the action returns. It does **not** secure what a client reaches
*through* that set, because `expand` becomes Entity Framework's `Include`, and `Include` walks
navigation properties without regard to the `Where` you put on the root.

An EF Core **global query filter** does cover those, because EF applies it to every entity type
in the query, including the ones pulled in by `Include`:

[!code-csharp[](../snippets/MultiTenancySnippets.cs#TenantQueryFilters)]

This is the highest-leverage single measure available, for two reasons: it is the only one that
constrains graph traversal, and it applies to an action written next month whether or not its
author thought about it.

What it costs you: the filter needs request state — the current user or tenant — inside the
`DbContext`, which couples your model to the request; `IgnoreQueryFilters` bypasses it, so
administrative paths need deliberate handling; and a filter on a required navigation can make
data look mysteriously absent. Worth it, in my view, but not free.

> [!IMPORTANT]
> **Query filters apply to reads, not to saves.** Breeze attaches the entity the client sent and
> issues an `UPDATE ... WHERE key = @key`; no query runs, so no filter applies. The save side has
> to restate the boundary itself — see [below](#writes-restate-the-boundary).

### Make the query limits global, not per controller

As written, forgetting `[BreezeQueryFilter(MaxTake = …)]` on a new controller means *unlimited*.
Register the filter once instead and forgetting means *limited*:

[!code-csharp[](../snippets/MultiTenancySnippets.cs#GlobalQueryLimits)]

This is safe to apply globally: an action that does not return a queryable falls out of the
filter immediately. Controllers that genuinely need different limits still carry their own
attribute.

### Declare which navigations may be expanded

`MaxDepth` bounds how *far* a client may walk; it says nothing about *where*. `MaxDepth = 1` still
permits `expand=employee` from an order, and with it the salary on that employee.

<xref:Breeze.Core.AllowExpandAttribute> and <xref:Breeze.Core.DenyExpandAttribute> say which
navigations are reachable from a type:

[!code-csharp[](../snippets/ExpandPolicySnippets.cs#AttributeDeclaration)]

Each type declares only its **own** outbound navigations, and a path is judged one hop at a time
by the type each hop starts from. So `Orders.OrderDetails` needs `Orders` allowed on `Customer`
and `OrderDetails` allowed on `Order` — nobody writes whole paths, and adding a navigation to
`Order` does not mean revisiting `Customer`.

A refused query gets **400 Bad Request**, naming the first forbidden hop and no more, so the
message does not disclose what lies beyond it.

For a model whose classes are generated — or to override what they declare, without editing
them — <xref:Breeze.Core.ExpandPolicy> takes the same rules in code:

[!code-csharp[](../snippets/ExpandPolicySnippets.cs#RegistrationApi)]

Rules are resolved per navigation, highest first: a registered deny, then a registered allow-list,
then `DenyExpand`, then `AllowExpand`, then the default. Registration beats the attributes, which
is what lets a deployment tighten or relax what the model says.

> [!IMPORTANT]
> **An allow-list always means "these and no others"**, whichever source it came from. So
> `ExpandPolicy.Allow<Order>(o => o.Employee)` does not merely lift a `DenyExpand` on `Employee` —
> it makes `Employee` the only navigation expandable from `Order`. To relax one rule and leave
> the rest, list everything you want.

With nothing declared nothing is refused, so this changes no existing application until you use
it. Once you have been through the model, invert the default:

[!code-csharp[](../snippets/ExpandPolicySnippets.cs#DenyByDefault)]

> [!WARNING]
> This governs `expand` only. A client can still reach a related entity through `select` —
> `?{"select":"orders"}` returns the orders whether or not `expand=orders` was refused. `MaxDepth`
> does check select paths, so it remains the bound there; see
> [Filtering sees what projection hides](#filtering-sees-what-projection-hides) for the same
> shape of problem.

### Give saves one chokepoint

Authorization that lives in eight controllers is authorization that holds in seven. Put the
baseline in `BeforeSaveEntities` on a base `PersistenceManager` that all of yours derive from —
which types may be saved, and who may touch a row — and keep per-controller
`BeforeSaveEntityDelegate` for exceptions to it.

---

## Multi-tenancy

One database holding several customers' data is the case where all of this stops being
theoretical: the boundary is a column, every query crosses it, and a single missing filter
exposes one customer's data to another.

### The tenant id is an identity, not a parameter

Take it from the signed-in principal's claims. A subdomain, a header, a route segment or a field
in the payload can all be set by the caller, so none of them identifies anybody — at most they
decide which login to demand.

[!code-csharp[](../snippets/MultiTenancySnippets.cs#TenantContext)]

Note that it fails closed. A principal with no tenant claim gets an exception, not an unfiltered
view — the opposite of what `Guid.Empty` as a default would do.

[!code-csharp[](../snippets/MultiTenancySnippets.cs#TenantRegistration)]

### Reads: one filter per tenant-owned type

The `BillingContext` above is the whole read-side guard. Two things about it matter:

- **Compare against the field, not a literal.** `i.TenantId == _tenantId` closes over an instance
  field, which EF parameterises, so one cached model serves every tenant. Baking a constant into
  the filter would cache the first tenant's value and serve it to everyone.
- **A type with no filter has no boundary.** The list in `OnModelCreating` is the security
  control, so it is the thing to check whenever an entity is added to the model. A marker
  interface like `ITenantOwned` lets you assert the list is complete rather than trusting it:

[!code-csharp[](../snippets/MultiTenancySnippets.cs#TenantOwned)]

Genuinely shared reference data — currencies, country codes — is the legitimate exception. It
has no tenant column and needs no filter; just be sure that is a decision rather than an
oversight.

### Writes: restate the boundary

Query filters do nothing here, so the save guard carries the tenant rules itself:

[!code-csharp[](../snippets/MultiTenancySnippets.cs#TenantSaveGuard)]

Four decisions in that, each worth its line:

**Stamp, never read.** `TenantId` is assigned from the claim on insert *and* on update. Not
because the client is expected to change it, but because an update rewrites every mapped column
from what arrived, so leaving it alone means taking the client's value.

**Verify against the stored row.** Keys are guessable — sequential integers especially — so a
client can name another tenant's row in a save bundle without ever having read it. The check is
what stops that, and it has to consult the database.

**Query through the filtered `DbSet`.** Because the read side is already filtered,
`Context.Invoices.Any(...)` cannot see another tenant's invoice. "Not found" and "not yours" are
the same answer, with no comparison to get wrong.

**Return the same error either way.** A client that can tell "does not exist" from "belongs to
someone else" can enumerate which keys are real.

> [!WARNING]
> Verify with `Any()` or a projection — **not** with something that materialises the entity.
> `BeforeSaveEntities` runs before Breeze attaches the client's instances, so a verification read
> that tracks a row with the same key collides with the attach that follows, and the save fails
> with a duplicate-tracking error. `Context.Invoices.Any(i => i.InvoiceID == id)` and
> `.Select(i => i.TenantId).FirstOrDefault()` are both safe; `.FirstOrDefault(i => …)` is not.

### If the data is worth more than the code

Everything above is application code, and application code has bugs. Where a cross-tenant leak
would be severe, put the boundary somewhere a bug cannot reach past:

| | |
|---|---|
| **Row-level security** (SQL Server, PostgreSQL) | the predicate lives in the database and applies to every statement, including ones your app did not mean to issue |
| **Schema or database per tenant** | no shared table to leak across; costs you migrations and connection management |

Both are defence in depth, not replacements — you still want the query filters, because a
failure there should be a missing row rather than a caught exception.

### Test the boundary, do not inspect it

The one test worth writing is the negative one: sign in as tenant A, ask for a row belonging to
tenant B by key, and assert you get nothing — as a query, as an `expand` through a navigation,
and as a save. That test fails loudly when someone adds an entity and forgets its filter, which
is exactly the mistake that review misses.

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
| Global query filters on every tenant- or owner-scoped type | [above](#put-the-read-boundary-in-the-orm) |
| Sensitive navigations refused by `[DenyExpand]` or an allow-list | [above](#declare-which-navigations-may-be-expanded) |
| The query limits registered globally, not per controller | [above](#make-the-query-limits-global-not-per-controller) |
| Tenant stamped from the claim on every save, and verified against the stored row | [above](#writes-restate-the-boundary) |
| A test that asserts cross-tenant access fails | [above](#test-the-boundary-do-not-inspect-it) |
| `IncludeStackTraceInErrors` off in production | [above](#the-rest-is-ordinary-aspnet-core) |
| `[Authorize]` where it belongs, including on `Metadata` if needed | [above](#metadata) |

## See also

- [Querying](querying.md) — the filter, and what its limits do
- [Saving](saving.md) — the hooks, in full
- [Error handling](error-handling.md) — telling a client what it may not do
