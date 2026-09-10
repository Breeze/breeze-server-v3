using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

// Regenerates tests/Databases/BreezeTestDb.sql from a live BreezeTestDb.
//
//   dotnet run --project tools/DbScripter -- <output.sql> [server] [database]

var outPath  = args.Length > 0 ? args[0] : "BreezeTestDb.sql";
var instance = args.Length > 1 ? args[1] : ".";
var dbName   = args.Length > 2 ? args[2] : "BreezeTestDb";

var csb = new SqlConnectionStringBuilder {
    DataSource = instance,
    InitialCatalog = dbName,
    IntegratedSecurity = true,
    Encrypt = false,
    TrustServerCertificate = true,
};
Console.WriteLine($"connecting: {csb.ConnectionString}");

var server = new Server(new ServerConnection(new SqlConnection(csb.ConnectionString)));
var db = server.Databases[dbName] ?? throw new InvalidOperationException($"{dbName} not found");

var tables = db.Tables.Cast<Table>()
    .Where(t => !t.IsSystemObject && t.Name != "sysdiagrams")
    .OrderBy(t => t.Name)
    .ToList();
Console.WriteLine($"scripting {tables.Count} tables");
var urns = tables.Select(t => t.Urn).ToArray();

static ScriptingOptions BaseOptions() => new() {
    IncludeHeaders = false,
    NoCollation = true,
    AnsiPadding = false,
    ExtendedProperties = false,
    ScriptBatchTerminator = false,
};

var schemaOpts = BaseOptions();
schemaOpts.ScriptSchema = true;
schemaOpts.ScriptData = false;
schemaOpts.DriAll = true;
schemaOpts.Indexes = true;
schemaOpts.Triggers = true;

var dataOpts = BaseOptions();
dataOpts.ScriptSchema = false;
dataOpts.ScriptData = true;

var schema = new Scripter(server) { Options = schemaOpts }.EnumScript(urns).ToList();
Console.WriteLine($"  schema statements: {schema.Count}");
var data = new Scripter(server) { Options = dataOpts }.EnumScript(urns).ToList();
Console.WriteLine($"  data statements:   {data.Count}");

using var w = new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false));
w.WriteLine("-- BreezeTestDb - complete schema and seed data for the Breeze test suite.");
w.WriteLine("--");
w.WriteLine("-- Regenerate with:");
w.WriteLine("--   dotnet run --project tools/DbScripter -- tests/Databases/BreezeTestDb.sql");
w.WriteLine("--");
w.WriteLine("-- Apply to a fresh database:");
w.WriteLine("--   sqlcmd -S . -E -Q \"CREATE DATABASE BreezeTestDb\"");
w.WriteLine("--   sqlcmd -S . -E -d BreezeTestDb -i tests/Databases/BreezeTestDb.sql");
w.WriteLine();
w.WriteLine("SET NOCOUNT ON;");
w.WriteLine("GO");
w.WriteLine();
w.WriteLine("-- ==================== schema ====================");
foreach (var s in schema) { w.WriteLine(s); w.WriteLine("GO"); }
w.WriteLine();
w.WriteLine("-- ==================== data ====================");
w.WriteLine("-- Constraints are disabled around the inserts so table order does not matter.");
w.WriteLine("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';");
w.WriteLine("GO");
foreach (var s in data) { w.WriteLine(s); }
w.WriteLine("GO");
w.WriteLine("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';");
w.WriteLine("GO");
Console.WriteLine($"wrote {outPath}");
