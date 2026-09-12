using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Core {

  /// <summary>
  /// The expand clause of an <see cref="EntityQuery"/> - the related entities the client asked
  /// to have loaded along with the results.
  /// </summary>
  /// <remarks>
  /// Each path names a navigation property, or several joined with '.' to reach further, as in
  /// "OrderDetails.Product".
  /// </remarks>
  public class ExpandClause {
    private List<String> _propertyPaths;


    /// <summary> Build an expand clause, or null if there is nothing to expand. </summary>
    /// <param name="propertyPaths">The navigation paths to expand; null when the query has no expand clause.</param>
    /// <returns>The clause, or null if <paramref name="propertyPaths"/> was null.</returns>
    public static ExpandClause? From(IEnumerable? propertyPaths) {
      return (propertyPaths == null) ? null : new ExpandClause(propertyPaths.Cast<String>());
    }

    /// <summary> Create an expand clause over a set of navigation paths. </summary>
    /// <param name="propertyPaths">The navigation paths to expand, e.g. "OrderDetails.Product".</param>
    public ExpandClause(IEnumerable<String> propertyPaths) {
      _propertyPaths = propertyPaths.ToList();
    }


    /// <summary> The navigation paths to expand. </summary>
    public IEnumerable<String> PropertyPaths {
      get { return _propertyPaths.AsReadOnly(); }
    }

  }

}