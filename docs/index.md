---
_layout: landing
---

# Breeze Server for .NET - API reference

**Breeze** is a library from [IdeaBlade](https://www.ideablade.com/) that helps you manage data in rich client
applications. This site is the API reference for the .NET server-side packages, version 8.0.0, which target
.NET 8, 9 and 10 on ASP.NET Core. It is generated from the XML doc comments in
[breeze-server-v3](https://github.com/Breeze/breeze-server-v3).

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

- **Breeze client documentation** - guides and the TypeScript API reference - lives in
  [breeze-client-v3](https://github.com/Breeze/breeze-client-v3) (see its `DOCS.md` to build it).
- **Upgrading from 7.x** - [UPGRADE.md](https://github.com/Breeze/breeze-server-v3/blob/master/UPGRADE.md) in
  breeze-server-v3.
- General Breeze documentation is at [breeze.github.io](http://breeze.github.io/doc-net/).
