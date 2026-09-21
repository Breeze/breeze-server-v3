# Saving

A Breeze client batches every pending change — added, modified and deleted entities, across as
many types as you like — into one **save bundle** and posts it to a single endpoint. The server
applies the lot in one unit and reports back.

[!code-csharp[](../snippets/SavingSnippets.cs#SaveChangesAction)]

The bundle arrives as a `JObject` rather than a typed model because it holds entities of mixed
types, each with its original values and its state. <xref:Breeze.Persistence.PersistenceManager>
turns it back into real entities attached in the right states, saves them, and returns a
<xref:Breeze.Persistence.SaveResult>.

## What comes back

| Member | Contents |
|---|---|
| <xref:Breeze.Persistence.SaveResult.Entities> | every entity that was saved, as the server now has it |
| <xref:Breeze.Persistence.SaveResult.KeyMappings> | temporary client keys paired with the real ones |
| <xref:Breeze.Persistence.SaveResult.DeletedKeys> | entities the server deleted as part of the save |
| <xref:Breeze.Persistence.SaveResult.Errors> | why the save failed |

Either `Errors` is set, or the other three are. The client merges `Entities` back into its cache,
so values the server changed — a computed column, an audit field, a database default — reach the
client without a re-query.

## Key mappings

A client that adds an entity invents a temporary key for it, because only the server knows the
real one. After the save, `KeyMappings` pairs them up:

```json
{ "EntityTypeName": "Order:#Northwind.Models", "TempValue": -1, "RealValue": 11078 }
```

The client swaps the temporary key for the real one everywhere it appears, including in the
foreign keys of entities saved in the same batch. For an identity column this is automatic. For a
key the database does not generate, set a <xref:Breeze.Persistence.IKeyGenerator> — see
[generated keys](persistence-manager.md#generated-keys).

## Intercepting the save

Four hooks, each a delegate property and a virtual method (see
[two ways to intercept](persistence-manager.md#two-ways-to-intercept-a-save)):

| Hook | Runs | Receives | Use it to |
|---|---|---|---|
| <xref:Breeze.Persistence.PersistenceManager.BeforeSaveEntityDelegate> | once per entity | an <xref:Breeze.Persistence.EntityInfo> | validate or adjust one entity; return `false` to drop it from the save |
| <xref:Breeze.Persistence.PersistenceManager.BeforeSaveEntitiesDelegate> | once for the batch | the whole save map | rules that span entities, or adding entities to the save |
| <xref:Breeze.Persistence.PersistenceManager.AfterSaveEntitiesDelegate> | once, after the save | the save map and the key mappings | anything that needs the real keys |

Each has an `...AsyncDelegate` counterpart for work that awaits.

### Per entity

`EntityInfo` carries the entity, its <xref:Breeze.Persistence.EntityInfo.EntityState>, and its
`OriginalValuesMap` — so a hook can see what actually changed, not just the new values:

[!code-csharp[](../snippets/SavingSnippets.cs#BeforeSaveEntity)]

> [!IMPORTANT]
> Returning `false` silently removes the entity from the save. The client is not told, and will
> still believe the change was applied. To *reject* a change, throw — see
> [Error handling](error-handling.md).

This hook is also where you enforce that a client may only change what it is allowed to change.
The bundle is user input: it names the entity type, the state and the original values, so trusting
it unchecked lets a client claim an entity it never fetched.

### Per batch

`BeforeSaveEntities` receives `Dictionary<Type, List<EntityInfo>>` — everything in the save, grouped
by type. Rules that need more than one entity go here:

[!code-csharp[](../snippets/SavingSnippets.cs#BeforeSaveEntities)]

You may add entries to the map to save entities the client never sent — an audit row, say. Return
the map.

### After the save

`AfterSaveEntities` gets the key mappings, so it is the place for work that needs the real keys.
It runs inside the transaction when there is one, so throwing here rolls the save back.

## Validation

<xref:Breeze.Persistence.DataAnnotationsValidator> applies the `System.ComponentModel.DataAnnotations`
attributes on your entities and collects the failures:

[!code-csharp[](../snippets/SavingSnippets.cs#ValidateOnSave)]

With `throwIfInvalid: true` it throws an <xref:Breeze.Persistence.EntityErrorsException>, which
reaches the client as a per-property error list the client attaches to the right entity. Pass
`false` to get the `List<EntityError>` back and decide yourself.

Where the entity class is generated and cannot carry the attributes,
<xref:Breeze.Persistence.DataAnnotationsValidator.AddDescriptor*> attaches a buddy class that does.
Call it **once** — a static constructor is the usual place — not on every save.

## Transactions

Pass <xref:Breeze.Persistence.TransactionSettings> to control how the save is wrapped:

[!code-csharp[](../snippets/SavingSnippets.cs#TransactionSettings)]

| `TransactionType` | What is wrapped |
|---|---|
| `None` (**default**) | nothing extra |
| `DbTransaction` | the hooks and the save, on the one connection |
| `TransactionScope` | the same, in an ambient `TransactionScope` — needed for distributed transactions |

> [!NOTE]
> The default of `None` does **not** mean the save is non-atomic. EF Core's own `SaveChanges` is
> already a transaction, so the database writes still succeed or fail together. What `None` leaves
> out is the *before* and *after* hooks: side effects in `BeforeSaveEntities` are not rolled back
> if the save then fails. Choose `DbTransaction` when your hooks write to the database.

`IsolationLevel` defaults to `ReadCommitted` and `Timeout` to `TransactionManager.DefaultTimeout`.
<xref:Breeze.Persistence.TransactionSettings.Default> changes the settings for every save that does
not pass its own.

## SaveOptions and the client's tag

A client can attach an arbitrary value to a save, which arrives as
<xref:Breeze.Persistence.SaveOptions.Tag>:

[!code-csharp[](../snippets/SavingSnippets.cs#SaveOptionsTag)]

It is how one endpoint serves several save intents without a separate route for each.

## See also

- [Error handling](error-handling.md) — what a failed save looks like to the client
- [The PersistenceManager](persistence-manager.md)
