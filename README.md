# Breeze Server for .NET

**Breeze** is a library from [IdeaBlade](https://www.ideablade.com/) that helps you manage data in rich client
applications. Breeze clients communicate with any remote service that speaks HTTP and JSON.

This repo contains the .NET server-side libraries. It is the successor to
[breeze.server.net](https://github.com/Breeze/breeze.server.net); see [Relationship to the old repo](#relationship-to-the-old-repo).

## Packages

| Package | Purpose |
|---|---|
| [Breeze.AspNetCore.NetCore](https://www.nuget.org/packages/Breeze.AspNetCore.NetCore/) | ASP.NET Core integration - `[BreezeQueryFilter]` and friends |
| [Breeze.Persistence.EFCore](https://www.nuget.org/packages/Breeze.Persistence.EFCore/) | Entity Framework Core support |
| [Breeze.Persistence.NH](https://www.nuget.org/packages/Breeze.Persistence.NH/) | NHibernate support |
| [Breeze.Persistence](https://www.nuget.org/packages/Breeze.Persistence/) | Shared persistence layer (pulled in automatically) |
| [Breeze.Core](https://www.nuget.org/packages/Breeze.Core/) | Query and serialization core (pulled in automatically) |

A typical EF Core application installs the first two.

**Version 8.0 targets .NET 8, 9 and 10.** For .NET 5, 6 or 7 - all out of support - use 7.5.2 from the old repo.

## Documentation

| | |
|---|---|
| [UPGRADE.md](./UPGRADE.md) | **Upgrading an app from 7.x** - every consumer-facing change |
| [CHANGES-DEV.md](./CHANGES-DEV.md) | Structural changes, for people working on Breeze itself |
| [STATUS.md](./STATUS.md) | What is done, what is in flight, what is next |

General Breeze documentation is at [breeze.github.io](http://breeze.github.io/doc-net/).

## Layout

```
src/     the five shipping packages
tests/   test server, EF/NHibernate models, and the test database script
tools/   DbScripter - regenerates tests/Databases/BreezeTestDb.sql
```

Shared package metadata, signing and target frameworks live in [src/Directory.Build.props](src/Directory.Build.props);
each `.csproj` carries only what differs.

## Building

```
dotnet build Breeze.sln
dotnet pack  src/Breeze.Core/Breeze.Core.csproj -c Release
```

## Running the tests

The test suite is driven from the client repo
([breeze-client-v3](https://github.com/Breeze/breeze-client-v3)); this repo provides the server it runs against.

1. Create the test database - a single database named `BreezeTestDb`:

   ```
   sqlcmd -S . -E -Q "CREATE DATABASE BreezeTestDb"
   sqlcmd -S . -E -d BreezeTestDb -f 65001 -i tests/Databases/BreezeTestDb.sql
   ```

2. Start the server:

   ```
   dotnet run --project tests/Test.AspNetCore.EFCore/Test.AspNetCore.EFCore.csproj --urls http://localhost:34377
   ```

3. Run the client test suite against it.

See [tests/Databases/README.md](tests/Databases/README.md) for details, including how to reset the database between
runs - the suite mutates data, so re-applying the script is the reliable reset.

## Relationship to the old repo

This repo starts fresh. The old repo carried four dead source trees (`AspNet`, `AspNetCore`, `AspNetCore-v3`, `Old`),
per-.NET-version duplicates of every project file (93 `.csproj` files, against 13 here), a gulp build, and 40MB of
SQL Server 2008-era `.mdf` binaries. None of that is here. The old repo remains available for history and for
7.x maintenance.

---

If you have discovered a bug or missing feature, please create an issue.

If you have questions about using Breeze, please ask on
[Stack Overflow](https://stackoverflow.com/questions/tagged/breeze).

If you need help developing your application, please contact us at [IdeaBlade](mailto:info@ideablade.com).
