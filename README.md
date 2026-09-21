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
| [docs/guide/](./docs/guide/) | **Using Breeze on the server** - getting started, querying, saving, metadata, errors |
| [UPGRADE.md](./UPGRADE.md) | **Upgrading an app from 7.x** - every consumer-facing change |
| [CHANGES-DEV.md](./CHANGES-DEV.md) | Structural changes, for people working on Breeze itself |
| [STATUS.md](./STATUS.md) | What is done, what is in flight, what is next |
| [DOCS.md](./DOCS.md) | Building and viewing the docs - the guide and the .NET API reference (DocFX) |

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

The suite is driven from the client repo,
[breeze-client-v3](https://github.com/Breeze/breeze-client-v3); this repo provides the
server and the database it runs against.

**Full instructions, verified end to end, are in
[breeze-client-v3/TESTING.md](https://github.com/Breeze/breeze-client-v3/blob/master/TESTING.md).**
The short version:

```bash
# 1. create the single test database (note -f 65001; see below)
sqlcmd -S . -E -Q "CREATE DATABASE BreezeTestDb"
sqlcmd -S . -E -d BreezeTestDb -f 65001 -i tests/Databases/BreezeTestDb.sql

# 2. start the server and leave it running
dotnet run --project tests/Test.AspNetCore.EFCore/Test.AspNetCore.EFCore.csproj \
  --no-launch-profile --urls http://localhost:34377

# 3. from the breeze-client-v3 checkout
npm test
```

`-f 65001` is required. The script is UTF-8 and the Northwind data contains accented
characters; without it sqlcmd decodes the file as the system ANSI codepage and silently
corrupts every one of them. See [tests/Databases/README.md](tests/Databases/README.md).

`--no-launch-profile` is also required - the default profile is IIS Express.


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
