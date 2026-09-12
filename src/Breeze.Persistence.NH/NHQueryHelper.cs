using Breeze.Core;
using NHibernate;
using NHibernate.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Breeze.Persistence.NH {
  /// <summary> Query helpers for NHibernate: an Include operator, and the hooks Breeze calls around query execution. </summary>
  public static class NHQueryHelper {

    /// <summary> Record a navigation path to be loaded with the results, in the manner of Entity Framework's Include. </summary>
    /// <remarks>
    /// The path is not added to the expression tree - NHibernate's LINQ parser would reject it -
    /// but kept on the query provider and applied after the query runs.  Unlike Fetch, this issues
    /// a second query rather than a join, which keeps the row count of the original query intact.
    /// </remarks>
    /// <typeparam name="TEntity">The element type of the query.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="navigationPropertyPath">The navigation path to load, e.g. "Orders".</param>
    /// <returns>A query carrying the recorded path; the original if its provider is not an NHibernate one.</returns>
    public static IQueryable<TEntity> Include<TEntity>(this IQueryable<TEntity> source, string navigationPropertyPath) where TEntity : class {
      // Defensive, and kept: callers compiled without nullable annotations can still pass null.
      if (source == null) return source!;
      var provider = source.Provider as DefaultQueryProvider;
      if (provider == null) return source;
      if (provider is NHQueryProvider) {
        ((NHQueryProvider)provider).Includes.Add(navigationPropertyPath);
        return source;
      } else {
        var nhp = new NHQueryProvider(provider);
        nhp.Includes.Add(navigationPropertyPath);
        var q = nhp.CreateQuery<TEntity>(source.Expression);
        return q;
      }
    }

    /// <summary> Whether Breeze should execute the queryable itself, rather than leave it to the framework. </summary>
    /// <param name="queryString">The Breeze query string, or null if the request carried none.</param>
    /// <param name="queryable">The queryable, or null if the action did not return one.</param>
    /// <returns>True when there is a queryable and either a query string or an NHibernate provider to run it with.</returns>
    public static bool NeedsExecution(string? queryString, IQueryable? queryable) {
      return (queryable != null && (queryString != null || queryable.Provider is DefaultQueryProvider));
    }

    /// <summary> After a query runs, load the expanded properties and close the session. </summary>
    /// <remarks>
    /// Lazy proxies must be resolved before the session closes, or serializing the results would
    /// fail. Paths come from both the query's expand clause and any <see cref="Include"/> calls.
    /// </remarks>
    /// <param name="eq">The query that was executed.</param>
    /// <param name="queryable">The queryable it was executed against.</param>
    /// <param name="result">The rows returned.</param>
    /// <returns>The same rows, with their expanded properties loaded.</returns>
    public static IList PostExecuteQuery(this EntityQuery eq, IQueryable queryable, IList result) {
      var expands = new List<string>();
      var provider = queryable.Provider as NHQueryProvider;
      if (provider != null && provider.Includes != null) {
        expands.AddRange(provider.Includes);
      }
      if (eq != null && eq.ExpandClause != null) {
        expands.AddRange(eq.ExpandClause.PropertyPaths);
      }
      if (expands.Count > 0) {
        NHExpander.InitializeList(result, expands);
      }

      if (queryable != null) {
        var session = GetSession(queryable);
        if (session != null) {
          if (session.IsOpen) session.Close();
        }
      }

      return result;
    }

    /// <summary>
    /// Get the ISession from the IQueryable.
    /// </summary>
    /// <param name="queryable"></param>
    /// <returns>the session if queryable.Provider is NHibernate.Linq.DefaultQueryProvider, else null</returns>
    private static ISession? GetSession(IQueryable queryable) {
      if (queryable == null) return null;
      var provider = queryable.Provider as DefaultQueryProvider;
      if (provider == null) return null;
      var sessionImpl = provider.Session as NHibernate.Impl.SessionImpl;
      return sessionImpl;
    }

  }
}
