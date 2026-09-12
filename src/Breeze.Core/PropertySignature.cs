using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;


namespace Breeze.Core {

  /// <summary>
  /// A property path such as "Customer.Address.City", resolved against a starting type into the
  /// chain of <see cref="PropertyInfo"/> it names, and buildable into the LINQ expression that
  /// reads it.
  /// </summary>
  /// <remarks>
  /// Query clauses - where, orderBy, select, expand - all address properties by path, so each of
  /// them resolves its paths through this class.  Resolution happens in the constructor, so an
  /// instance that was constructed successfully always holds a valid chain.
  /// </remarks>
  public class PropertySignature {
    /// <summary> Resolve a property path against a type. </summary>
    /// <param name="instanceType">The type the path starts from.</param>
    /// <param name="propertyPath">A property name, or several joined with '.' to walk into related types.</param>
    /// <exception cref="Exception">A segment of the path does not name a public instance property or field of the type it is applied to.</exception>
    public PropertySignature(Type instanceType, String propertyPath) {
      InstanceType = instanceType;
      PropertyPath = propertyPath;
      Properties = GetProperties(InstanceType, PropertyPath).ToList();
    }

    /// <summary> Whether a property path resolves against a type, without throwing if it does not. </summary>
    /// <param name="instanceType">The type the path starts from.</param>
    /// <param name="propertyPath">The path to test.</param>
    /// <returns>True if the path resolves.</returns>
    public static bool IsProperty(Type instanceType, String propertyPath) {
      return GetProperties(instanceType, propertyPath, false).Any(pi => pi != null);
    }

    /// <summary> The type the path starts from. </summary>
    public Type InstanceType { get; private set; }
    /// <summary> The path as it was written, e.g. "Customer.Address.City". </summary>
    public String PropertyPath { get; private set; }
    /// <summary> The resolved chain, one entry per segment of the path. </summary>
    public List<PropertyInfo> Properties { get; private set; }

    /// <summary> The path with its segments joined by '_', giving a name usable as an identifier - "Customer_Address_City". </summary>
    public String Name {
      get { return Properties.Select(p => p.Name).ToAggregateString("_"); }
    }

    /// <summary> The type of the last property in the chain - what reading the path yields. </summary>
    public Type ReturnType {
      get { return Properties.Last().PropertyType; }
    }

    /// <summary> For a path ending in a collection, the type of its items; null for a scalar property. </summary>
    public Type? ElementType {
      get { return TypeFns.GetElementType(ReturnType); }

    }

    /// <summary> Whether the path ends in a value - a predefined type or an enum - rather than in another entity. </summary>
    public bool IsDataProperty {
      get { return TypeFns.IsPredefinedType(ReturnType) || TypeFns.IsEnumType(ReturnType); }
    }

    /// <summary> Whether the path ends in another entity, or a collection of them, rather than in a value. </summary>
    public bool IsNavigationProperty {
      get { return !IsDataProperty; }
    }



    /// <summary> Walk a property path, yielding the property behind each segment. </summary>
    /// <param name="instanceType">The type the path starts from.</param>
    /// <param name="propertyPath">A property name, or several joined with '.'.</param>
    /// <param name="throwOnError">When true an unresolvable segment throws; when false the walk stops there, yielding only the segments that did resolve.</param>
    /// <returns>The properties named by the path, in order.</returns>
    /// <exception cref="Exception">A segment does not resolve and <paramref name="throwOnError"/> is true.</exception>
    public static IEnumerable<PropertyInfo> GetProperties(Type instanceType, String propertyPath, bool throwOnError = true) {
      var propertyNames = propertyPath.Split('.');

      var nextInstanceType = instanceType;
      foreach (var propertyName in propertyNames) {
        var property = GetProperty(nextInstanceType, propertyName, throwOnError);
        if (property != null) {
          yield return property;

          nextInstanceType = property.PropertyType;
        } else {
          break;
        }
      }
    }

    private static PropertyInfo? GetProperty(Type instanceType, String propertyName, bool throwOnError = true) {
      var propertyInfo = (PropertyInfo?)TypeFns.FindPropertyOrField(instanceType, propertyName,
        BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
      if (propertyInfo == null) {
        if (throwOnError) {
          var msg = String.Format("Unable to locate property '{0}' on type '{1}'.", propertyName, instanceType);
          throw new Exception(msg);
        } else {
          return null;
        }
      }
      return propertyInfo;
    }

    /// <summary> Build the expression that reads this path off a parameter - for the path "Address.City" and parameter x, the expression x.Address.City. </summary>
    /// <param name="parmExpr">The parameter the path is read from, typically the lambda parameter of the query.</param>
    /// <returns>The member access expression.</returns>
    public Expression BuildMemberExpression(ParameterExpression parmExpr) {
      Expression memberExpr = BuildPropertyExpression(parmExpr, Properties.First());
      foreach (var property in Properties.Skip(1)) {
        memberExpr = BuildPropertyExpression(memberExpr, property);
      }
      return memberExpr;
    }

    /// <summary> Build a single property access - one step of the chain that <see cref="BuildMemberExpression"/> assembles. </summary>
    /// <param name="baseExpr">The expression the property is read from.</param>
    /// <param name="property">The property to read.</param>
    /// <returns>The member access expression.</returns>
    public Expression BuildPropertyExpression(Expression baseExpr, PropertyInfo property) {
      return Expression.Property(baseExpr, property);
    }



  }


}
