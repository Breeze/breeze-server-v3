using Breeze.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;

namespace Test.AspNetCore.Controllers {

  /// <summary>
  /// Lets the client test suite put BreezeTestDb back into a known state between spec files.
  ///
  ///   POST ~/breeze/TestDb/Snapshot  takes a SQL Server database snapshot of the database
  ///                                  (dropping any earlier one)
  ///   POST ~/breeze/TestDb/Reset     reverts the database to that snapshot
  ///
  /// The client's test/global-setup.ts rebuilds the database, seeds the inheritance tables
  /// and calls Snapshot once per run; test/integration-setup.ts calls Reset before every
  /// integration spec file. It has to be an HTTP endpoint because in browser mode the spec
  /// files run in Chromium, which cannot shell out to sqlcmd.
  ///
  /// This is a TEST HOST controller. It lives under tests/, never in a package, and it is
  /// off unless the host is started with TestDb:AllowReset=true (the client's
  /// scripts/test-with-server.ps1 passes --TestDb:AllowReset=true). Even then it only
  /// answers requests from the local machine. Otherwise both actions return 404.
  /// </summary>
  [Route("breeze/[controller]/[action]")]
  public class TestDbController : Controller {
    public const string AllowResetKey = "TestDb:AllowReset";

    private readonly IConfiguration _configuration;

    public TestDbController(IConfiguration configuration) {
      _configuration = configuration;
    }

    [HttpPost]
    public IActionResult Snapshot() {
      if (!IsAllowed()) return NotFound();
      var (db, snapshot) = Names();
      var sw = Stopwatch.StartNew();
      using (var conn = OpenMaster()) {
        // RESTORE ... FROM DATABASE_SNAPSHOT refuses to run while the database has more than
        // one snapshot, so drop every snapshot of it, not just ours.
        foreach (var name in Query(conn, "SELECT name FROM sys.databases WHERE source_database_id = DB_ID(@db)", db)) {
          Execute(conn, $"DROP DATABASE {Quote(name)}");
        }

        // A snapshot needs one sparse file per data file of the source database. Put each
        // next to the file it shadows, where SQL Server is known to be able to write.
        var files = new List<string>();
        using (var cmd = new SqlCommand("SELECT name, physical_name FROM sys.master_files WHERE database_id = DB_ID(@db) AND type = 0", conn)) {
          cmd.Parameters.AddWithValue("@db", db);
          using (var reader = cmd.ExecuteReader()) {
            while (reader.Read()) {
              var logicalName = reader.GetString(0);
              var dir = Path.GetDirectoryName(reader.GetString(1));
              var path = Path.Combine(dir, $"{snapshot}_{logicalName}.ss");
              files.Add($"(NAME = {Quote(logicalName)}, FILENAME = {Literal(path)})");
            }
          }
        }
        if (files.Count == 0) {
          return StatusCode(409, $"Database '{db}' does not exist.");
        }
        Execute(conn, $"CREATE DATABASE {Quote(snapshot)} ON {string.Join(", ", files)} AS SNAPSHOT OF {Quote(db)}");
      }
      return Ok(new { database = db, snapshot, milliseconds = sw.ElapsedMilliseconds });
    }

    [HttpPost]
    public IActionResult Reset() {
      if (!IsAllowed()) return NotFound();
      var (db, snapshot) = Names();
      var sw = Stopwatch.StartNew();
      using (var conn = OpenMaster()) {
        var found = Query(conn, "SELECT name FROM sys.databases WHERE source_database_id = DB_ID(@db)", db);
        if (!found.Contains(snapshot)) {
          return StatusCode(409, $"No snapshot '{snapshot}' of '{db}' to reset to. " +
            "Run the client tests once without BREEZE_SKIP_DB_RESET (-SkipDbReset) to take one.");
        }
        // Close this server's idle pooled connections first. ROLLBACK IMMEDIATE would kill
        // them anyway, but killing an idle pooled session takes SQL Server about 3 seconds;
        // with none open the whole revert takes a few hundred milliseconds.
        SqlConnection.ClearAllPools();
        // One batch, so nothing can take the single-user slot between the statements.
        Execute(conn,
          $"ALTER DATABASE {Quote(db)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"RESTORE DATABASE {Quote(db)} FROM DATABASE_SNAPSHOT = {Literal(snapshot)}; " +
          $"ALTER DATABASE {Quote(db)} SET MULTI_USER;");
      }
      // Again afterwards: a connection in use during the restore was killed by it, and a
      // request that picked it up would fail with a transport-level error.
      SqlConnection.ClearAllPools();
      ResetKeyGeneratorCache();
      return Ok(new { database = db, snapshot, milliseconds = sw.ElapsedMilliseconds });
    }

    private bool IsAllowed() {
      if (!_configuration.GetValue<bool>(AllowResetKey)) return false;
      var remote = HttpContext.Connection.RemoteIpAddress;
      return remote == null || IPAddress.IsLoopback(remote);
    }

    private (string db, string snapshot) Names() {
      var db = new SqlConnectionStringBuilder(_configuration.GetConnectionString("BreezeTestDb")).InitialCatalog;
      return (db, db + "_TestSnapshot");
    }

    private SqlConnection OpenMaster() {
      var builder = new SqlConnectionStringBuilder(_configuration.GetConnectionString("BreezeTestDb")) {
        InitialCatalog = "master",
        Pooling = false,
      };
      var conn = new SqlConnection(builder.ConnectionString);
      conn.Open();
      return conn;
    }

    private static List<string> Query(SqlConnection conn, string sql, string db) {
      var result = new List<string>();
      using (var cmd = new SqlCommand(sql, conn)) {
        cmd.Parameters.AddWithValue("@db", db);
        using (var reader = cmd.ExecuteReader()) {
          while (reader.Read()) result.Add(reader.GetString(0));
        }
      }
      return result;
    }

    private static void Execute(SqlConnection conn, string sql) {
      using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 }) {
        cmd.ExecuteNonQuery();
      }
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";

    /// <summary>
    /// NumericKeyGenerator hands out ids from a block it reserves in the NextId table and
    /// caches in static fields. The restore rewinds NextId but not the cache, so the cache
    /// would later re-reserve ids it has already handed out. Emptying it makes the next save
    /// reserve a fresh block from the restored table. The fields are private, hence
    /// reflection; if they are ever renamed this quietly does nothing.
    /// </summary>
    private static void ResetKeyGeneratorCache() {
      var type = typeof(NumericKeyGenerator);
      var flags = BindingFlags.NonPublic | BindingFlags.Static;
      var lockField = type.GetField("__lock", flags);
      var nextId = type.GetField("__nextId", flags);
      var maxNextId = type.GetField("__maxNextId", flags);
      if (lockField?.GetValue(null) is not object gate || nextId == null || maxNextId == null) return;
      lock (gate) {
        nextId.SetValue(null, 0L);
        maxNextId.SetValue(null, 0L);
      }
    }
  }
}
