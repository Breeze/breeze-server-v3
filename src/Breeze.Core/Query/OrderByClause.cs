
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary> The orderBy clause of an <see cref="EntityQuery"/> - the properties to sort by, and their directions. </summary>
  /// <remarks>
  /// Each path may carry a direction, as in "OrderDate desc"; anything other than "desc" after
  /// the path sorts ascending. A path may walk navigation properties, as in "Customer.CompanyName".
  /// </remarks>
  public class OrderByClause {

    private List<OrderByItem> _orderByItems;
    private List<String> _propertyPaths;

    
    // need to be able to take in a List<Object>
    /// <summary> Build an orderBy clause, or null if the query does not sort. </summary>
    /// <param name="propertyPaths">The sort paths; null when the query has no orderBy clause.</param>
    /// <returns>The clause, or null if <paramref name="propertyPaths"/> was null.</returns>
    public static OrderByClause? From(IEnumerable? propertyPaths) {
      return (propertyPaths == null) ? null : new OrderByClause(propertyPaths.Cast<String>());
    }

    /// <summary> Create an orderBy clause, splitting each path from its optional direction. </summary>
    /// <param name="propertyPaths">The sort paths, each optionally followed by "desc", e.g. "OrderDate desc".</param>
    public OrderByClause(IEnumerable<String> propertyPaths) {
      _propertyPaths = propertyPaths.ToList();
      _orderByItems = _propertyPaths.Select(pp => {
        var itemTrimmed = Regex.Replace(pp, @"\s+", " ").Trim();
        String[] itemParts = itemTrimmed.Split(' ');
        var isDesc = itemParts.Length == 1 ? false : itemParts[1].Equals("desc");
        return new OrderByItem(itemParts[0], isDesc);
      }).ToList();
    }

    /// <summary> Resolve every sort path against the entity type being queried. </summary>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">A path does not name a property of that type.</exception>
    public void Validate(Type entityType) {
      foreach (OrderByItem item in _orderByItems) {
        item.Validate(entityType);
      }
    }

    /// <summary> The sort paths as they were written, direction included. </summary>
    public IEnumerable<String> PropertyPaths {
      get { return _propertyPaths.AsReadOnly(); }
    }

    /// <summary> The sort paths split into property and direction. </summary>
    public IEnumerable<OrderByItem> OrderByItems {
      get { return _orderByItems.AsReadOnly(); }
    }

    /// <summary> One property to sort by, and whether it sorts descending. </summary>
    public class OrderByItem {
      /// <summary> The property path to sort by, without any direction suffix. </summary>
      public string PropertyPath { get; private set; }
      /// <summary> Whether this path sorts descending. </summary>
      public bool IsDesc { get; private set; }
      /// <summary> The resolved property.  Set by <see cref="Validate"/>; null before it runs. </summary>
      public PropertySignature? Property { get; private set; } // set by Validate(); null before that

      /// <summary> Create a sort term. </summary>
      /// <param name="propertyPath">The property path to sort by.</param>
      /// <param name="isDesc">True to sort descending.</param>
      public OrderByItem(String propertyPath, bool isDesc) {
        PropertyPath = propertyPath;
        IsDesc = isDesc;
      }


      /// <summary> Resolve this term's path against the entity type being queried. </summary>
      /// <param name="entityType">The type the query returns.</param>
      /// <exception cref="Exception">The path does not name a property of that type.</exception>
      public void Validate(Type entityType) {
        Property = new PropertySignature(entityType, PropertyPath);
      }

    }
  }
}