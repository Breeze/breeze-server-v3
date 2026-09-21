// Snippets for docs/guide/error-handling.md.

using Breeze.AspNetCore;
using Breeze.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Breeze.Docs.Snippets {

  internal static class ErrorHandlingSetup {

    internal static void AddFilter(WebApplicationBuilder builder) {
      #region AddFilter
      builder.Services.AddControllers().AddMvcOptions(o => {
        o.Filters.Add(new GlobalExceptionFilter());
      });
      #endregion
    }

    internal static void AddFilterWithDbMapper(WebApplicationBuilder builder) {
      builder.Services.AddControllers().AddMvcOptions(o => {
        #region DbExceptionMapper
        o.Filters.Add(new GlobalExceptionFilter {
          StatusCodeForException = DbExceptionMappers.SqlServer
        });
        #endregion
      });
    }

    internal static void StackTraces() {
      #region StackTraces
      BreezeConfig.Instance.IncludeStackTraceInErrors = true;   // NOT in production
      #endregion
    }

    #region ThrowEntityErrors
    private static bool CheckFreight(EntityInfo info) {
      if (info.Entity is Order order && order.Freight > 1000) {
        throw new EntityErrorsException("Validation errors", new[] {
          new EntityError {
            ErrorName = "FreightTooHigh",
            EntityTypeName = typeof(Order).FullName,
            KeyValues = new object[] { order.OrderID },
            PropertyName = "Freight",
            ErrorMessage = "Freight may not exceed 1000",
          }
        });
      }
      return true;
    }
    #endregion

    internal static void ThrowConcurrency(IReadOnlyList<Conflict> conflicts) {
      #region ThrowConcurrency
      var errors = conflicts.Select(c =>
        ConcurrencyErrorsException.CreateEntityError(c.TypeName, c.KeyValues));
      throw new ConcurrencyErrorsException(
        ConcurrencyErrorsException.CreateMessage(conflicts.Count), errors);
      #endregion
    }

    internal static void Suppress() => CheckFreight(null!);

    /// <summary> Whatever your ORM gives you about one stale row. </summary>
    internal class Conflict {
      public string? TypeName { get; set; }
      public object[]? KeyValues { get; set; }
    }
  }
}
