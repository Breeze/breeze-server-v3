using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Breeze.Core {
  /// <summary> Applies the clauses of an <see cref="EntityQuery"/> to an IQueryable, one clause at a time. </summary>
  /// <remarks>
  /// Order matters: where first, then orderBy, skip and take, then select, and expand last.
  /// Each method is a no-op when the query has no such clause.
  /// </remarks>
  public static class EntityQueryExtensions {
    /// <summary> Apply the query's where clause, if it has one. </summary>
    /// <param name="eq">The query.  It must already have been validated.</param>
    /// <param name="queryable">The queryable to filter.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The filtered queryable, or the original if there is no where clause.</returns>
    public static IQueryable ApplyWhere(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.WherePredicate != null) {
        queryable = QueryBuilder.ApplyWhere(queryable, eleType, eq.WherePredicate);
      }
      return queryable;
    }

    /// <summary> Apply the query's orderBy clause, if it has one. </summary>
    /// <param name="eq">The query.  It must already have been validated.</param>
    /// <param name="queryable">The queryable to sort.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The sorted queryable, or the original if there is no orderBy clause.</returns>
    public static IQueryable ApplyOrderBy(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.OrderByClause != null) {
        queryable = QueryBuilder.ApplyOrderBy(queryable, eleType, eq.OrderByClause);
      }
      return queryable;
    }

    /// <summary> Apply the query's select clause, if it has one, projecting onto a generated type. </summary>
    /// <param name="eq">The query.  It must already have been validated.</param>
    /// <param name="queryable">The queryable to project.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The projected queryable, or the original if there is no select clause.</returns>
    public static IQueryable ApplySelect(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.SelectClause != null) {
        queryable = QueryBuilder.ApplySelect(queryable, eleType, eq.SelectClause);
      }
      return queryable;
    }

    /// <summary> Apply the query's skip count, if it has one. </summary>
    /// <param name="eq">The query.</param>
    /// <param name="queryable">The queryable to skip within.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The queryable, unchanged if no skip was requested.</returns>
    public static IQueryable ApplySkip(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.SkipCount.HasValue) {
        queryable = QueryBuilder.ApplySkip(queryable, eleType, eq.SkipCount.Value);
      }
      return queryable;
    }

    /// <summary> Apply the query's take count, if it has one. </summary>
    /// <param name="eq">The query.</param>
    /// <param name="queryable">The queryable to limit.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The queryable, unchanged if no take was requested.</returns>
    public static IQueryable ApplyTake(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.TakeCount.HasValue) {
        queryable = QueryBuilder.ApplyTake(queryable, eleType, eq.TakeCount.Value);
      }
      return queryable;
    }

    /// <summary> Apply the query's expand clause by calling Include for each path. </summary>
    /// <remarks> Include is resolved dynamically, so this works against any provider that offers one. </remarks>
    /// <param name="eq">The query.</param>
    /// <param name="queryable">The queryable to add Includes to.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The queryable, unchanged if there is no expand clause.</returns>
    public static IQueryable ApplyExpand(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.ExpandClause != null) {
        eq.ExpandClause.PropertyPaths.ToList().ForEach(expand => {
          queryable = ((dynamic)queryable).Include(expand.Replace('/', '.'));
        });
        
      }
      return queryable;
    }

    /// <summary> Gets the minimum Take() value in the queryable Expression </summary>
    public static int? GetTakeValue(IQueryable queryable) {
      var visitor = new TakeFinder();
      visitor.Visit(queryable.Expression);
      return visitor.MinTake;
    }
  }


  /// <summary> Sets MinTake to the minimum Take() value in the queryable Expression </summary>
  public class TakeFinder : ExpressionVisitor {
    /// <summary> The smallest Take() found so far, or null if the expression contains none. </summary>
    public int? MinTake { get; private set; } = null;

    /// <summary> Records the argument of every Queryable.Take call, keeping the smallest. </summary>
    /// <param name="node">The call being visited.</param>
    /// <returns>The node, unchanged - this visitor only observes.</returns>
    protected override Expression VisitMethodCall(MethodCallExpression node) {
      if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == "Take") {
        if (int.TryParse(node.Arguments.Last().ToString(), out int arg)) {
          if (MinTake == null || MinTake > arg) {
            MinTake = arg;
          }
        }
      }
      return base.VisitMethodCall(node);
    }
  }
}
