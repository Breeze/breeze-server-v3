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

The Northwind and Produce data still ships as the SQL Server data files in this
directory. To build `BreezeTestDb` on a local default instance:

1. Copy `NorthwindIB.mdf` / `NorthwindIB_log.ldf` to a folder SQL Server can write to
   (do **not** attach them in place — attaching upgrades the files, which would dirty
   the repo), renaming them to `BreezeTestDb.mdf` / `BreezeTestDb_log.ldf`.

2. Attach them under the new name:

   ```sql
   CREATE DATABASE BreezeTestDb
     ON (FILENAME='<path>\BreezeTestDb.mdf'),
        (FILENAME='<path>\BreezeTestDb_log.ldf')
     FOR ATTACH;
   ```

   > These files were created by SQL Server 2008 (internal version 904). Attaching
   > them to a modern instance performs a one-way upgrade. If you get
   > `Operating system error 5 (Access is denied)`, grant your own Windows account
   > full control of the files — `CREATE DATABASE ... FOR ATTACH` checks file access
   > while impersonating the calling login, not the service account.

3. Add the Produce table, by attaching `ProduceTPH.mdf` the same way under a temporary
   name and copying the single table across:

   ```sql
   SELECT * INTO BreezeTestDb.dbo.ItemOfProduce FROM <temp>.dbo.ItemOfProduce;
   ALTER TABLE BreezeTestDb.dbo.ItemOfProduce ALTER COLUMN Id uniqueidentifier NOT NULL;
   ALTER TABLE BreezeTestDb.dbo.ItemOfProduce ADD CONSTRAINT PK_ItemOfProduce PRIMARY KEY CLUSTERED (Id);
   ```

4. Add the inheritance tables by running [`inheritance-schema.sql`](./inheritance-schema.sql)
   against `BreezeTestDb`. That script is generated from the EF model — regenerate it with
   `InheritanceContext.Database.GenerateCreateScript()` if the model changes.

The inheritance tables carry no data in the file; they are populated on every server
startup by `InheritanceDbInitializer.Seed`.

## Resetting between runs

[`CleanBreezeTestDb.sql`](./CleanBreezeTestDb.sql) removes the rows that the save tests
add, returning the Northwind tables to their original state. Run it if a failed test run
leaves the database dirty.

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
