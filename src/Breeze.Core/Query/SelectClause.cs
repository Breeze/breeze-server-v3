using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary>
  /// The select clause of an <see cref="EntityQuery"/> - the properties to project, in place of
  /// whole entities.
  /// </summary>
  /// <remarks>
  /// Each path names a property, or a path through navigation properties to one, as in
  /// "Customer.CompanyName". The paths are only resolved once <see cref="Validate"/> has been
  /// given the entity type.
  /// </remarks>
  public class SelectClause {
    private List<String> _propertyPaths;
    private List<PropertySignature> _properties = null!; // set by Validate()

    /// <summary> Build a select clause, or null if the query does not project. </summary>
    /// <param name="propertyPaths">The property paths to select; null when the query has no select clause.</param>
    /// <returns>The clause, or null if <paramref name="propertyPaths"/> was null.</returns>
    public static SelectClause? From(IEnumerable? propertyPaths) {
      return (propertyPaths == null) ? null : new SelectClause(propertyPaths.Cast<String>());
    }

    /// <summary> Create a select clause over a set of property paths. </summary>
    /// <param name="propertyPaths">The property paths to select, e.g. "Customer.CompanyName".</param>
    public SelectClause(IEnumerable<String> propertyPaths) {
      _propertyPaths = propertyPaths.ToList();
    }


    /// <summary> The property paths to select, as they were written. </summary>
    public IEnumerable<String> PropertyPaths {
      get { return _propertyPaths.AsReadOnly(); }
    }

    /// <summary> The resolved properties.  Only available after <see cref="Validate"/> has run; reading it before that throws. </summary>
    public IEnumerable<PropertySignature> Properties {
      get { return _properties.AsReadOnly(); }
    }

    /// <summary> Resolve every path against the entity type being queried, filling in <see cref="Properties"/>. </summary>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">A path does not name a property of that type.</exception>
    public void Validate(Type entityType) {
      _properties = _propertyPaths.Select(pp => new PropertySignature(entityType, pp)).ToList();
    }

  }
}