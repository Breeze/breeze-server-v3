using System;
using System.Net;
using System.Reflection;

namespace Breeze.AspNetCore {

  /// <summary>
  /// Ready-made mappings from a database exception to an HTTP status code, for
  /// <see cref="GlobalExceptionFilter.StatusCodeForException"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A duplicate key is a conflict with the current state of the resource, so 409 Conflict says
  /// more than a blanket 500 - and unlike a 500, a client will not retry it.
  /// </para>
  /// <para>
  /// These read the provider's error number off the exception by type name and property, rather
  /// than referencing the provider. Breeze supports EF Core, NHibernate and whatever database is
  /// behind them, so none of the Breeze packages depends on Microsoft.Data.SqlClient, Npgsql or
  /// any other client library, and adding one for this would be the wrong trade. If you would
  /// rather match on the concrete type, write the mapping yourself - it is one expression, and
  /// <see cref="GlobalExceptionFilter.StatusCodeForException"/> takes any delegate:
  /// </para>
  /// <code>
  /// StatusCodeForException = ex => ex.GetBaseException() is SqlException { Number: 2627 or 2601 or 547 }
  ///   ? HttpStatusCode.Conflict : null
  /// </code>
  /// </remarks>
  public static class DbExceptionMappers {

    /// <summary> SQL Server: violation of a unique constraint. </summary>
    public const int SqlServerUniqueConstraint = 2627;
    /// <summary> SQL Server: attempt to insert a duplicate key in a unique index. </summary>
    public const int SqlServerDuplicateKeyInIndex = 2601;
    /// <summary> SQL Server: a foreign key constraint was violated. </summary>
    public const int SqlServerForeignKeyViolation = 547;

    /// <summary>
    /// Maps a SQL Server duplicate-key or foreign-key violation to 409 Conflict, and everything
    /// else to null, which leaves <see cref="GlobalExceptionFilter"/> to use its default.
    /// <code>
    /// o.Filters.Add(new GlobalExceptionFilter {
    ///   StatusCodeForException = DbExceptionMappers.SqlServer
    /// });
    /// </code>
    /// </summary>
    /// <param name="exception">The exception the filter caught.</param>
    /// <returns>409 Conflict, or null to accept the default.</returns>
    public static HttpStatusCode? SqlServer(Exception exception) {
      // EF Core wraps the provider exception in a DbUpdateException, NHibernate in a
      // GenericADOException; GetBaseException walks to the innermost one either way.
      var number = SqlServerErrorNumber(exception?.GetBaseException());
      return number is SqlServerUniqueConstraint or SqlServerDuplicateKeyInIndex or SqlServerForeignKeyViolation
        ? HttpStatusCode.Conflict
        : null;
    }

    /// <summary>
    /// The SQL Server error number carried by an exception, or null if it is not a SqlException.
    /// </summary>
    /// <remarks>
    /// Matched on the full type name so that an unrelated exception that happens to have a
    /// <c>Number</c> property is not mistaken for one. Both the modern
    /// <c>Microsoft.Data.SqlClient</c> and the older <c>System.Data.SqlClient</c> are recognized.
    /// </remarks>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns>The error number, or null.</returns>
    public static int? SqlServerErrorNumber(Exception? exception) {
      if (exception == null) return null;
      var typeName = exception.GetType().FullName;
      if (typeName != "Microsoft.Data.SqlClient.SqlException" &&
          typeName != "System.Data.SqlClient.SqlException") return null;

      var prop = exception.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance);
      return prop?.GetValue(exception) as int?;
    }
  }
}
