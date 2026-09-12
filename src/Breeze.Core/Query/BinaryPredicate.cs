
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Breeze.Core {
  /// <summary>
  /// A where clause comparing two operands - a property against a literal, or a property
  /// against another property.
  /// </summary>
  /// <remarks>
  /// <see cref="Validate"/> does the work of deciding what each side is, including coercing
  /// literals to the property's type and handling enums and 'in' lists.
  /// </remarks>
  public class BinaryPredicate : BasePredicate {
    /// <summary> The left-hand operand as the client wrote it - a property path, or a function call. </summary>
    public Object Expr1Source { get; private set; }
    /// <summary> The right-hand operand as the client wrote it - a literal, a property path, or an array for 'in'. </summary>
    public Object? Expr2Source { get; private set; }
    // Both blocks are built by Validate(), which must run before ToExpression().
    private BaseBlock _block1 = null!;
    private BaseBlock _block2 = null!;

    /// <summary> Compare two operands with an operator. </summary>
    /// <param name="op">The comparison to apply.</param>
    /// <param name="expr1Source">The left-hand operand.</param>
    /// <param name="expr2Source">The right-hand operand; null compares against null.</param>
    public BinaryPredicate(Operator op, Object expr1Source, Object? expr2Source) : base(op) {
      Expr1Source = expr1Source;
      Expr2Source = expr2Source;
    }

    /// <summary> Resolve both operands against the entity type, coercing the right-hand side to the left's type. </summary>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">The left operand is null or unresolvable, or 'in' was given a right-hand operand that is not an array.</exception>
    public override void Validate(Type entityType) {
      if (Expr1Source == null) {
        throw new Exception("Unable to validate 1st expression: " + this.Expr1Source);
      }

      this._block1 = BaseBlock.CreateLHSBlock(Expr1Source, entityType);

      if (_op == Operator.In && !(Expr2Source is IList)) {
        throw new Exception("The 'in' operator requires that its right hand argument be an array");
      }

      // Special purpose Enum handling

      var enumType = GetEnumType(this._block1);
      if (enumType != null) { 
        if (Expr2Source != null) {
          var et = TypeFns.GetNonNullableType(enumType);
          if (Expr2Source is IList) {
            // coerce to list of enum
            var list = DataType.CoerceList((IList)Expr2Source, enumType);
            this._block2 = new LitBlock(list, null);
          } else {
            var expr2Enum = Expr2Source is string ? Enum.Parse(et, (String)Expr2Source) : Enum.ToObject(et, Expr2Source);
            this._block2 = BaseBlock.CreateRHSBlock(expr2Enum, entityType, null);
          }
        } else {
          this._block2 = BaseBlock.CreateRHSBlock(null, entityType, null);
        }
      } else {
        if (Expr2Source is IList) {
          // coerce to list of (potentially nullable) types
          // A left-hand block that is not a PropBlock is an FnBlock, whose registered
          // return types are all non-null DataTypes with a CLR type.
          var propType = (this._block1 is PropBlock pb) ? pb.Property.ReturnType : this._block1.DataType!.GetUnderlyingType()!;
          var list = DataType.CoerceList((IList)Expr2Source, propType);
          this._block2 = new LitBlock(list, null);
        } else {
          this._block2 = BaseBlock.CreateRHSBlock(Expr2Source, entityType, this._block1.DataType);
        }
      }
    }



    /// <summary> Build the comparison, inserting the nullable conversions the two sides need to be comparable. </summary>
    /// <param name="paramExpr">The query's lambda parameter.</param>
    /// <returns>An expression of type bool.</returns>
    public override Expression ToExpression(ParameterExpression paramExpr) {
      // Null only for a BinaryOperator this class does not handle; that has always
      // failed in the caller (Expression.Lambda).
      return BuildBinaryExpr(_block1.ToExpression(paramExpr), _block2.ToExpression(paramExpr), Operator)!;
    }

    private Type? GetEnumType(BaseBlock block) {
      if (block is PropBlock) {
        PropBlock pExpr = (PropBlock)block;
        var prop = pExpr.Property;
        if (prop.IsDataProperty) {
          if (TypeFns.IsEnumType(prop.ReturnType)) {
            return prop.ReturnType;
          }
        }
      }
      return null;
    }

    private Expression? BuildBinaryExpr(Expression expr1, Expression expr2, Operator op) {

      if (expr1.Type != expr2.Type) {
        // don't try to convert if operator is In, because then expr2 is IList
        if (TypeFns.IsNullableType(expr1.Type) && !TypeFns.IsNullableType(expr2.Type) && op != BinaryOperator.In) {
          if (!expr2.Type.IsEnum) {
            expr2 = Expression.Convert(expr2, expr1.Type);
          } else {
            expr1 = Expression.Convert(expr1, expr2.Type);
          }
        } else if (TypeFns.IsNullableType(expr2.Type) && !TypeFns.IsNullableType(expr1.Type)) {
          if (!expr1.Type.IsEnum) {
            expr1 = Expression.Convert(expr1, expr2.Type);
          } else {
            expr2 = Expression.Convert(expr2, expr1.Type);
          }
        }

        // GetNullableType is null for SByte, DateOnly and TimeOnly (predefined but not in
        // TypeFns' nullable map); Expression.Convert then throws, as it always has.
        if (HasNullValue(expr2) && CannotBeNull(expr1)) {
          expr1 = Expression.Convert(expr1, TypeFns.GetNullableType(expr1.Type)!);
        } else if (HasNullValue(expr1) && CannotBeNull(expr2)) {
          expr2 = Expression.Convert(expr2, TypeFns.GetNullableType(expr2.Type)!);
        }
        
      }

      if (op == BinaryOperator.Equals) {
        return Expression.Equal(expr1, expr2);
      } else if (op == BinaryOperator.NotEquals) {
        return Expression.NotEqual(expr1, expr2);
      } else if (op == BinaryOperator.GreaterThan) {
        return Expression.GreaterThan(expr1, expr2);
      } else if (op == BinaryOperator.GreaterThanOrEqual) {
        return Expression.GreaterThanOrEqual(expr1, expr2);
      } else if (op == BinaryOperator.LessThan) {
        return Expression.LessThan(expr1, expr2);
      } else if (op == BinaryOperator.LessThanOrEqual) {
        return Expression.LessThanOrEqual(expr1, expr2);
      } else if (op == BinaryOperator.StartsWith) {
        var mi = TypeFns.GetMethodByExample((String s) => s.StartsWith("abc"));
        return Expression.Call(expr1, mi, expr2);
      } else if (op == BinaryOperator.EndsWith) {
        var mi = TypeFns.GetMethodByExample((String s) => s.EndsWith("abc"));
        return Expression.Call(expr1, mi, expr2);
      } else if (op == BinaryOperator.Contains) {
        var mi = TypeFns.GetMethodByExample((String s) => s.Contains("abc"));
        return Expression.Call(expr1, mi, expr2);
      } else if (op == BinaryOperator.In) {
        // List<T>.Contains always exists.
        var mi = TypeFns.GetMethodByNameAndType(typeof(List<>), "Contains", expr1.Type)!;
        return Expression.Call(expr2, mi, expr1);
      }

      return null;
    }
    
    private bool HasNullValue(Expression expr) {
      var le = expr as ConstantExpression;
      return le == null ? false : le.Value == null;
    }

    private bool CannotBeNull(Expression expr) {
      var t = expr.Type;
      return TypeFns.IsPredefinedType(t) && t != typeof(String);
    }
  }
}
