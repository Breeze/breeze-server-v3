# The PersistenceManager

<xref:Breeze.Persistence.PersistenceManager> is the server side of Breeze. It does two things a
plain `DbContext` or NHibernate `ISession` does not:

1. **Describes the model** — turns your mapping into the JSON metadata document the client builds
   its entity types from.
2. **Applies a save bundle** — takes the JSON a client sends, turns it back into entities in the
   right states, saves them in one transaction, and reports back the server-assigned keys.

It is abstract. You use the subclass for your ORM.

| ORM | Class | Constructed from |
|---|---|---|
| Entity Framework Core | <xref:Breeze.Persistence.EFCore.EFPersistenceManager`1> | your `DbContext` |
| NHibernate | <xref:Breeze.Persistence.NH.NHPersistenceManager> | an `ISession` |

## Subclassing it

Write one per context. Even with no members it is worth having, because interceptors and metadata
overrides go here rather than in the controller:

```csharp
public class NorthwindPersistenceManager : EFPersistenceManager<NorthwindContext> {
  public NorthwindPersistenceManager(NorthwindContext context) : base(context) { }
}
```

<xref:Breeze.Persistence.EFCore.EFPersistenceManager`1.Context> gives the typed `DbContext` back,
which is what query actions return sets from:

```csharp
[HttpGet]
public IQueryable<Customer> Customers() => _pm.Context.Customers;
```

## Lifetime

**Create one per request**, in the controller's constructor, from a `DbContext` that DI has already
scoped to the request:

```csharp
public NorthwindController(NorthwindContext context) {
  _pm = new NorthwindPersistenceManager(context);
}
```

It wraps a `DbContext`, so it inherits the `DbContext`'s rules: not thread-safe, and not meant to
outlive the request. Registering one as a singleton shares one change-tracker between every
concurrent request.

> [!WARNING]
> A `PersistenceManager` is built for **one save**. It keeps the state of the save in progress, and
> a second `SaveChanges` on the same instance rebuilds that state rather than adding to it. This is
> the ordinary case — one instance, one request, one save — but it is worth knowing before you
> reach for a longer-lived one.

NHibernate is the same shape with an `ISession` in place of the `DbContext`; the test server builds
an `ISessionFactory` once as a singleton and opens a session per request from it.

## What you get without writing anything

| Member | What it does |
|---|---|
| <xref:Breeze.Persistence.PersistenceManager.Metadata> | the JSON metadata document, built from your mapping |
| <xref:Breeze.Persistence.PersistenceManager.SaveChangesAsync*> | applies a save bundle in a transaction, returns a <xref:Breeze.Persistence.SaveResult> |
| <xref:Breeze.Persistence.PersistenceManager.SaveChanges*> | the synchronous form |
| <xref:Breeze.Persistence.PersistenceManager.SaveOptions> | what the client sent alongside the changes, including its `Tag` |
| <xref:Breeze.Persistence.PersistenceManager.KeyGenerator> | how temporary client keys become real ones |

## Two ways to intercept a save

Every hook exists both as a **delegate property**, set per request, and as a **virtual method**,
overridden once in your subclass. They do the same work; pick by how widely the rule applies.

```csharp
// Per request - a rule for this endpoint only.
[HttpPost]
public Task<SaveResult> SaveWithAudit([FromBody] JObject saveBundle) {
  _pm.BeforeSaveEntityDelegate = SetAuditFields;
  return _pm.SaveChangesAsync(saveBundle);
}

// Once - a rule for every save through this manager.
public class NorthwindPersistenceManager : EFPersistenceManager<NorthwindContext> {
  protected override bool BeforeSaveEntity(EntityInfo entityInfo) {
    // return false to drop this entity from the save
    return true;
  }
}
```

[Saving](saving.md) covers what each hook receives and when it runs.

## Generated keys

When a client adds an entity it invents a temporary key, and the server has to tell it what the
real one turned out to be. For an identity column the ORM already knows, and Breeze reports the
mapping back with no help from you — see [key mappings](saving.md#key-mappings).

For a key the database does *not* generate, set a
<xref:Breeze.Persistence.IKeyGenerator>. <xref:Breeze.Persistence.NumericKeyGenerator> is the one
that ships, which draws from a `NextId` table:

```csharp
_pm.KeyGenerator = new NumericKeyGenerator(_pm.GetDbConnection() as DbConnection);
```

## Metadata from somewhere else

`EFPersistenceManager<T>` builds metadata from the EF model. To supply your own instead — a
hand-written document, or one for types EF does not map — override
`BuildAltJsonMetadata()`; returning `null`, the default, keeps the generated document. See
[Metadata](metadata.md#supplying-your-own).
