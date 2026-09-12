
using Breeze.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {

  /// <summary> A reference to a property in a query, resolved against the entity type on construction. </summary>
  public class PropBlock : BaseBlock {
    /// <summary> The property path as the client wrote it, e.g. "Customer.CompanyName". </summary>
    public String PropertyPath { get; private set; }
    /// <summary> The resolved property chain behind <see cref="PropertyPath"/>. </summary>
    public PropertySignature Property { get; private set; }
    /// <summary> The entity type the path was resolved against. </summary>
    public Type EntityType { get; private set; } 

    /// <summary> Resolve a property path against an entity type. </summary>
    /// <param name="propertyPath">A property name, or several joined with '.'.</param>
    /// <param name="entityType">The type the path starts from.</param>
    /// <exception cref="Exception">The path does not name a property of that type.</exception>
    public PropBlock(String propertyPath, Type entityType) {
      EntityType = entityType;
      PropertyPath = propertyPath;
      Property = new PropertySignature(entityType, propertyPath);

      if (Property == null) {
        throw new Exception("Unable to validate propertyPath: " + PropertyPath + " on EntityType: " + entityType.Name);
      }

    }


    /// <summary> The data type the property returns. </summary>
    /// <exception cref="Exception">The path ends in a navigation property, which has no data type.</exception>
    public override DataType DataType {
      get {
        try {
          return DataType.FromType(Property.ReturnType);
        } catch {
          throw new Exception("This property expression returns a NavigationProperty not a DataProperty");
        }
      }
    }

    /// <summary> Build the member access that reads this property off the row. </summary>
    /// <param name="inExpr">The query's lambda parameter.</param>
    /// <returns>The member access expression, e.g. ent.Customer.CompanyName.</returns>
    public override Expression ToExpression(Expression inExpr) {
      return Property.BuildMemberExpression((ParameterExpression) inExpr);
    }
  }
}