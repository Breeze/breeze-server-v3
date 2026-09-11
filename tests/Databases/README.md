# Breeze test database

All Breeze server tests run against a **single** database: **`BreezeTestDb`**.

Previously the tests used three separate databases — `NorthwindIB`, `InheritanceContext`
and `ProduceTPH` — each with its own connection string. They are now consolidated into
one, so there is a single database to create, back up, reset and point a connection
string at. The three EF `DbContext`s still exist and are unchanged; they simply share
one catalog. Their tables do not overlap:

| Source model | Tables |
|---|---|
| NorthwindIB   | `Customer`, `Order`, `OrderDetail`, `Employee`, `Product`, `Supplier`, `Category`, `Region`, `Territory`, `EmployeeTerritory`, `EmployeeTerritoryNoPayload`, `InternationalOrder`, `PreviousEmployee`, `Role`, `User`, `UserRole`, `TimeGroup`, `TimeLimit`, `Comment`, `Geospatial`, `UnusualDate`, `NextId` |
| ProduceTPH    | `ItemOfProduce` |
| Inheritance   | `AccountTypes`, `BillingDetailTPHs`, `BillingDetailTPTs`, `BankAccountTPTs`, `BankAccountTPCs`, `CreditCardTPTs`, `CreditCardsTPCs`, `DepositTPHs`, `DepositTPTs`, `DepositTPCs` |

## Creating the database

[`BreezeTestDb.sql`](./BreezeTestDb.sql) creates all 33 tables — Northwind, Produce and the
inheritance tables — and loads the Northwind and Produce data. On a local default instance,
from the root of this repo:

```bash
sqlcmd -S . -E -Q "CREATE DATABASE BreezeTestDb"
sqlcmd -S . -E -d BreezeTestDb -f 65001 -i tests/Databases/BreezeTestDb.sql
```

`-f 65001` matters; see *Encoding* below. Alternatively, from a `breeze-client-v3` checkout
next to this repo, `scripts\test-with-server.cmd` creates the database if it is missing,
starts the test server and runs the client suite against it.

The inheritance tables are created empty; `InheritanceDbInitializer.Seed` fills them on
every server startup.

[`inheritance-schema.sql`](./inheritance-schema.sql) is the EF-generated schema for the
inheritance tables, kept for reference and regenerated with
`InheritanceContext.Database.GenerateCreateScript()` when that model changes. It is already
part of `BreezeTestDb.sql`; you do not need to run it.

To regenerate `BreezeTestDb.sql` itself after a schema change, script a **pristine**
database — one no tests have run against — with:

```bash
dotnet run --project tools/DbScripter -- tests/Databases/BreezeTestDb.sql
```

The database used to ship as SQL Server 2008 `.mdf`/`.ldf` files that had to be attached
and merged by hand. Those files are gone.

## Resetting between runs

Re-applying `BreezeTestDb.sql` is the reset. The client's integration tests do it
automatically before every run (`test/global-setup.ts` in breeze-client-v3). By hand:

```bash
sqlcmd -S . -E -Q "IF DB_ID('BreezeTestDb_TestSnapshot') IS NOT NULL DROP DATABASE BreezeTestDb_TestSnapshot; ALTER DATABASE BreezeTestDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE BreezeTestDb; CREATE DATABASE BreezeTestDb;"
sqlcmd -S . -E -d BreezeTestDb -f 65001 -i tests/Databases/BreezeTestDb.sql
```

The first statement drops the database snapshot the client tests leave behind; a database
that has a snapshot cannot be dropped.

### Between spec files: the database snapshot

Rebuilding takes seconds, too long to repeat before each of the client's integration spec
files. Instead, after each rebuild the client has the test host take a SQL Server
[database snapshot](https://learn.microsoft.com/sql/relational-databases/databases/database-snapshots-sql-server)
of the pristine database, `BreezeTestDb_TestSnapshot`, and reverts to it before every
spec file. Both go through a test-host-only controller,
[`TestDbController`](../Test.AspNetCore.EFCore/Controllers/TestDbController.cs):

| | |
|---|---|
| `POST /breeze/TestDb/Snapshot` | drops any snapshot of `BreezeTestDb` and takes a new one (sparse `.ss` files next to the data file) |
| `POST /breeze/TestDb/Reset` | reverts `BreezeTestDb` to the snapshot, then clears the connection pools |

They are **off by default** and return 404 unless the host is started with
`--TestDb:AllowReset=true` (the client's `scripts/test-with-server.ps1` does this), and
even then they only answer requests from the local machine. The controller is part of the
test host alone, which is not packable; it is in no package.

A revert takes about 0.2 seconds. It leaves the cache cold and the connection pool empty,
so one before each of the 27 client files adds about 10 seconds to a run. Database
snapshots need SQL Server 2016 SP1 or later, in any edition.

`SINGLE_USER WITH ROLLBACK IMMEDIATE` disconnects a running test server; it reconnects on
its next query, but restart it so that it re-seeds the inheritance tables.

[`CleanBreezeTestDb.sql`](./CleanBreezeTestDb.sql) predates the script and is **not** a
reliable reset: it deletes rows the save tests are known to add, but cannot restore rows
they delete, misses some they add, and one of its deletes fails on a foreign key. It is due
to be removed; don't rely on it.

`InheritanceDbInitializer.Seed` resets the inheritance tables by deleting and re-inserting
their rows. It deliberately does **not** call `EnsureDeleted`/`EnsureCreated` any more —
that would drop the whole shared database, taking the Northwind and Produce data with it.

## Connection string

One entry, in `Tests/Test.AspNetCore.EFCore/appsettings.json`:

```json
"ConnectionStrings": {
  "BreezeTestDb": "Data Source=.;Initial Catalog=BreezeTestDb;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
}
```

## Encoding — use `-f 65001`

`BreezeTestDb.sql` is UTF-8 and the Northwind data contains accented characters
(`México D.F.`, `San Cristóbal`, `Bergulfsen`). **Always apply it with `sqlcmd -f 65001`.**

Without that flag sqlcmd decodes the file as the system ANSI codepage and silently mangles
every non-ASCII value — `México` becomes `MÃ©xico`. Nothing fails at the time. The damage
only becomes visible if you then regenerate the script from that database, because the
corruption compounds on each round trip until values overflow their columns:

```
Msg 2628 ... String or binary data would be truncated in table 'Customer', column 'City'.
Truncated value: 'San CristÃƒÂ³ba'.
```

The generated file also carries a UTF-8 BOM so that a plain `sqlcmd -i` decodes it
correctly, but pass the flag anyway — it is what the tooling and CI do.
