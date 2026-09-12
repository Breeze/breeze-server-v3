
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {

  /// <summary>
  /// One side of a comparison in a where clause, once parsed: a property reference
  /// (<see cref="PropBlock"/>), a literal (<see cref="LitBlock"/>), or a function call
  /// (<see cref="FnBlock"/>).
  /// </summary>
  public abstract class BaseBlock {

    // will return either a PropBlock or a FnBlock
    /// <summary> Parse the left-hand side of a predicate. </summary>
    /// <remarks>
    /// Only a string is accepted here: either a property path, or a function call such as
    /// "toupper(CompanyName)". Literals, objects and arrays are legal only on the right.
    /// </remarks>
    /// <param name="exprSource">The left-hand side as it arrived from the client.</param>
    /// <param name="entityType">The entity type being queried.</param>
    /// <returns>A <see cref="PropBlock"/>, or an <see cref="FnBlock"/> if the string contains a call.</returns>
    /// <exception cref="Exception">The value is null, an object, an array, or any other non-string.</exception>
    public static BaseBlock CreateLHSBlock(Object exprSource,
        Type entityType) {
      if (exprSource == null) {
        throw new Exception(
            "Null expressions are only permitted on the right hand side of a BinaryPredicate");
      }

      if (exprSource is IDictionary) {
        throw new Exception(
            "Object expressions are only permitted on the right hand side of a BinaryPredicate");
      }

      if (exprSource is IList) {
        throw new Exception(
            "Array expressions are only permitted on the right hand side of a BinaryPredicate");
      }

      if (!(exprSource is String)) {
        throw new Exception(
            "Only string expressions are permitted on this predicate");
      }

      String source = (String)exprSource;
      if (source.IndexOf("(") == -1) {
        return new PropBlock(source, entityType);
      } else {
        return FnBlock.CreateFrom(source, entityType);
      }


    }

    // will return either a PropBlock or a LitBlock
    /// <summary> Parse the right-hand side of a predicate. </summary>
    /// <remarks>
    /// This side is permissive: a literal, the name of another property to compare against, an
    /// array for an 'in' clause, or an object carrying an explicit "value" together with either
    /// "dataType" or "isProperty". A bare string is read as a property if the entity type has one
    /// by that name, and as a literal otherwise.
    /// </remarks>
    /// <param name="exprSource">The right-hand side as it arrived from the client; null yields a null literal.</param>
    /// <param name="entityType">The entity type being queried.  When null, strings are taken as literals.</param>
    /// <param name="otherExprDataType">The data type of the left-hand side, used to coerce a literal.</param>
    /// <returns>A <see cref="PropBlock"/> or a <see cref="LitBlock"/>.</returns>
    /// <exception cref="Exception">The value is an object with no "value" property, or of a type that cannot appear here.</exception>
    public static BaseBlock CreateRHSBlock(Object? exprSource,
        Type entityType, DataType? otherExprDataType) {

      if (exprSource == null) {
        return new LitBlock(exprSource, otherExprDataType);
      }

      if (exprSource is String) {
        String source = (String)exprSource;
        if (entityType == null) {
          // if entityType is unknown then assume that the rhs is a
          // literal
          return new LitBlock(source, otherExprDataType);
        }

        if (PropertySignature.IsProperty(entityType, source)) {
          return new PropBlock(source, entityType);
        } else { 
          return new LitBlock(source, otherExprDataType);
        } 
      }

      if (TypeFns.IsPredefinedType(exprSource.GetType())) {
        return new LitBlock(exprSource, otherExprDataType);
      }

      if (exprSource is IDictionary<string, Object>) {
        var exprMap = (IDictionary<string, Object?>)exprSource;
        // note that this is NOT the same a using get and checking for null
        // because null is a valid 'value'.
        if (!exprMap.ContainsKey("value")) {
          throw new Exception(
              "Unable to locate a 'value' property on: "
                  + exprMap.ToString());
        }
        Object? value = exprMap["value"];

        if (exprMap.ContainsKey("isProperty")) {
          // A property reference names a property; a null 'value' here fails in
          // PropBlock, as it always has.
          return new PropBlock((String)value!, entityType);
        } else {
          String? dt = (String?)exprMap["dataType"];
          DataType? dataType = (dt != null) ? DataType.FromName(dt) : otherExprDataType;
          return new LitBlock(value, dataType);
        }
      }

      if (exprSource is IList) {
        // right now this pretty much implies the values on an 'in' clause
        return new LitBlock(exprSource, otherExprDataType);
      }

      if (TypeFns.IsEnumType(exprSource.GetType())) {
        return new LitBlock(exprSource, otherExprDataType);
      }

      throw new Exception(
          "Unable to parse the right hand side of this BinaryExpression: "
              + exprSource.ToString());

    }

    // null for a literal whose type is unknown (e.g. a null literal)
    /// <summary> The data type this side of the comparison yields, or null when it cannot be determined - as for a null literal. </summary>
    public abstract DataType? DataType {
      get;
    }

    

    /// <summary> Build the LINQ expression for this block. </summary>
    /// <param name="inExpr">The query's lambda parameter.</param>
    /// <returns>The expression that produces this side's value.</returns>
    public abstract Expression ToExpression(Expression inExpr);

  }

}