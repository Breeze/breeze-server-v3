# .NET API reference

Every public type in the five Breeze server packages, generated from the XML doc comments in
`src/`. Start at the namespace for the package you are using.

| Namespace | Package | Start at |
|---|---|---|
| [Breeze.AspNetCore](xref:Breeze.AspNetCore) | Breeze.AspNetCore.NetCore | <xref:Breeze.AspNetCore.BreezeQueryFilterAttribute>, <xref:Breeze.AspNetCore.GlobalExceptionFilter> |
| [Breeze.Persistence.EFCore](xref:Breeze.Persistence.EFCore) | Breeze.Persistence.EFCore | <xref:Breeze.Persistence.EFCore.EFPersistenceManager`1> |
| [Breeze.Persistence.NH](xref:Breeze.Persistence.NH) | Breeze.Persistence.NH | <xref:Breeze.Persistence.NH.NHPersistenceManager> |
| [Breeze.Persistence](xref:Breeze.Persistence) | Breeze.Persistence | <xref:Breeze.Persistence.PersistenceManager>, <xref:Breeze.Persistence.SaveResult> |
| [Breeze.Core](xref:Breeze.Core) | Breeze.Core | <xref:Breeze.Core.JsonSerializationFns>, <xref:Breeze.Core.EntityQuery> |

A typical EF Core application installs the first two; the last two come with them.

The reference is built from the `net10.0` target. The public API is the same on `net8.0` and
`net9.0`, except that each references the matching major version of Entity Framework Core.

New here? The [guide](../guide/getting-started.md) is the place to start — it covers the same
ground in the order you need it.
