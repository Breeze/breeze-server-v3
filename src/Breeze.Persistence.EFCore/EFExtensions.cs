using Breeze.Core;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Breeze.Persistence.EFCore {


  /// <summary> The Entity Framework implementations of the query steps <see cref="EntityQuery"/> leaves to the persistence layer. </summary>
  /// <remarks> <c>EFPersistenceManager</c> plugs these in through EntityQuery's static hooks. </remarks>
  public static class EFExtensions {

    /// <summary> Apply the query's expand clause as EF Include calls. </summary>
    /// <param name="eq">The query.</param>
    /// <param name="queryable">The queryable to add Includes to.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The queryable, unchanged if there is no expand clause.</returns>
    public static IQueryable ApplyExpand(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.ExpandClause != null) {
        eq.ExpandClause.PropertyPaths.ToList().ForEach(expand => {
          queryable = EFQueryBuilder.ApplyExpand(queryable, eleType, expand.Replace('/', '.'));
        });
      }
      return queryable;
    }

    /// <summary> Add AsNoTracking before a projection that may include an owned type. </summary>
    /// <remarks>
    /// EF Core requires an owned type to be queried together with its owner unless tracking is
    /// off. The test here is broader than it needs to be - any non-System return type counts -
    /// so AsNoTracking is sometimes applied when it was not strictly necessary.
    /// </remarks>
    /// <param name="eq">The query.</param>
    /// <param name="queryable">The queryable to apply it to.</param>
    /// <param name="eleType">The element type of the queryable.</param>
    /// <returns>The queryable, unchanged unless the query projects a non-System type.</returns>
    public static IQueryable ApplyAsNoTracking(this EntityQuery eq, IQueryable queryable, Type eleType) {
      // This was added for EF Core 3 because of requirment that all owned types have their owners as part of the query
      // unless we add 'AsNoTracking'
      if (eq.SelectClause != null) {
        // TODO: this isn't quite right - we only want to ApplyAsNoTracking if the result of the select includes an 'owned' type
        // but currently the code just checks for any non system type, so we are applying 'noTracking' more than we should.
        // but we need to apply the AsNoTracking before the select ( much simpler that way).
        // A property of a closed entity type has a closed type, which always has a FullName.
        var areAllSystemTypes = eq.SelectClause.Properties.All(p => p.ReturnType.FullName!.StartsWith("System."));
        if (!areAllSystemTypes) {
          queryable = EFQueryBuilder.ApplyAsNoTracking(queryable, eleType);
        }
      }
      return queryable;
    }
  }

  /// <summary> Builds the EF query operators for an element type that is only known at runtime. </summary>
  public class EFQueryBuilder {

    /// <summary> Apply one EF Include to an untyped queryable. </summary>
    /// <param name="source">The queryable.</param>
    /// <param name="elementType">Its element type.</param>
    /// <param name="expand">The navigation path to include, dot-delimited.</param>
    /// <returns>The queryable with the Include applied.</returns>
    public static IQueryable ApplyExpand(IQueryable source, Type elementType, string expand) {
      var method = TypeFns.GetMethodByExample((IQueryable<String> q) => 
        EntityFrameworkQueryableExtensions.Include<String>(q, "dummyPath"), elementType);
      var func = QueryBuilder.BuildIQueryableFunc(elementType, method, expand);
      return func(source);
    }

    /// <summary> Apply EF AsNoTracking to an untyped queryable. </summary>
    /// <param name="source">The queryable.</param>
    /// <param name="elementType">Its element type.</param>
    /// <returns>The queryable with AsNoTracking applied.</returns>
    public static IQueryable ApplyAsNoTracking(IQueryable source, Type elementType) {
      var method = TypeFns.GetMethodByExample((IQueryable<Object> q) =>
        EntityFrameworkQueryableExtensions.AsNoTracking<Object>(q), elementType);
      var func = QueryBuilder.BuildIQueryableFunc(elementType, method);
      return func(source);
    }


  }
}

