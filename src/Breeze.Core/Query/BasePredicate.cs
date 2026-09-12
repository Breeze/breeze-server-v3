
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary> A where clause, in the tree the query parser builds from the JSON a client sends. </summary>
  /// <remarks>
  /// A predicate is built first and checked afterwards: <see cref="Validate"/> resolves its
  /// property paths against the entity type, and must run before <see cref="ToExpression"/> or
  /// <see cref="ToLambda"/> can build anything.
  /// </remarks>
  public abstract class BasePredicate {

    /// <summary> The operator this predicate applies; exposed by <see cref="Operator"/>. </summary>
    protected Operator _op;

    /// <summary> The operator this predicate applies. </summary>
    public Operator Operator {
      get { return _op; }
    }

    /// <summary> Create a predicate for an operator. </summary>
    /// <param name="op">The operator being applied.</param>
    public BasePredicate(Operator op) {
      _op = op;
    }

    /// <summary> Resolve this predicate's property paths against the entity type being queried. </summary>
    /// <remarks> Must run before <see cref="ToExpression"/>. </remarks>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">A path does not resolve, or an operand is not valid for the operator.</exception>
    public abstract void Validate(Type entityType);

    /// <summary> Build this predicate as a lambda over the entity type, ready to pass to Queryable.Where. </summary>
    /// <param name="entityType">The type the query returns.</param>
    /// <returns>A lambda of the form ent =&gt; &lt;predicate&gt;.</returns>
    public LambdaExpression ToLambda(Type entityType) {
      var paramExpr = Expression.Parameter(entityType, "ent");
      var expr = ToExpression(paramExpr);
      return Expression.Lambda(expr, paramExpr);
    }

    /// <summary> Build one predicate per entry of a parsed where clause. </summary>
    /// <param name="map">The where clause as parsed from JSON - property names, or operator names, to values.</param>
    /// <returns>One predicate per entry, to be combined with <c>and</c>.</returns>
    /// <exception cref="Exception">An entry uses an operator that is not valid in that position, or a value that cannot be resolved.</exception>
    public static List<BasePredicate> PredicatesFromMap(IDictionary<string, object?> map) {
      return map.Keys.Select(k => PredicateFromKeyValue(k, map[k])).ToList();
    }

    /// <returns>null if the map is null or empty</returns>
    public static BasePredicate? PredicateFromMap(IDictionary<string, object?>? sourceMap) {
      if (sourceMap == null) return null;
      List<BasePredicate> preds = PredicatesFromMap(sourceMap);
      return CreateCompoundPredicate(preds);
    }

    private static BasePredicate PredicateFromKeyValue(String key, Object? value) {
      Operator? op = Operator.FromString(key);
      if (op != null) {
        if (op.OpType == OperatorType.AndOr) {
          var preds2 = PredicatesFromObject(value);
          return new AndOrPredicate(op, preds2);
        } else if (op.OpType == OperatorType.Unary) {
          BasePredicate pred = PredicateFromObject(value);
          return new UnaryPredicate(op, pred);
        } else {
          throw new Exception("Invalid operator in context: " + key);
        }
      }

      if (value == null || TypeFns.IsPredefinedType(value.GetType())) {
        return new BinaryPredicate(BinaryOperator.Equals, key, value);
      } else if (value is IDictionary<string, object> && ((IDictionary<string, object>)value).ContainsKey("value")) {
        return new BinaryPredicate(BinaryOperator.Equals, key, value);
      }

      if (!(value is Dictionary<string, object>)) {
        throw new Exception("Unable to resolve value associated with key:" + key);
      }

      var preds = new List<BasePredicate>();
      var map = (Dictionary<string, object?>)value;


      foreach (var subKey in map.Keys) {

        Operator? subOp = Operator.FromString(subKey);
        Object? subVal = map[subKey];
        BasePredicate pred;
        if (subOp != null) {
          if (subOp.OpType == OperatorType.AnyAll) {
            BasePredicate subPred = PredicateFromObject(subVal);
            pred = new AnyAllPredicate(subOp, key, subPred);
          } else if (subOp.OpType == OperatorType.Binary) {
            pred = new BinaryPredicate(subOp, key, subVal);
          } else {
            throw new Exception("Unable to resolve OperatorType for key: " + subKey);
          }
          // next line old check was for null not 'ContainsKey'
        } else if (subVal is IDictionary<string, object> && ((IDictionary<string, object>)subVal).ContainsKey("value")) {
          pred = new BinaryPredicate(BinaryOperator.Equals, key, subVal);
        } else {
          throw new Exception("Unable to resolve BasePredicate after: " + key);
        }
        preds.Add(pred);
      }
      // An empty map (e.g. { "Name": {} }) yields null here; it has always been passed
      // on and failed later, during Validate/ToExpression.
      return CreateCompoundPredicate(preds)!;
    }


    private static BasePredicate PredicateFromObject(Object? source) {
      var preds = PredicatesFromObject(source);
      // Null for an empty source; passed on (and failing later) as it always has been.
      return CreateCompoundPredicate(preds)!;
      //		if (preds.size() > 1) {
      //			throw new RuntimeException("BasePredicateFromObject: should only contain a single item");
      //		} else {
      //		    return preds.get(0);
      //		}
    }

    private static List<BasePredicate> PredicatesFromObject(Object? source) {
      var preds = new List<BasePredicate>();
      if (source is IDictionary<string, Object>) {
        preds = PredicatesFromMap((IDictionary<string, Object?>)source);
      } else if (source is IList) {
        foreach (Object? item in (IList)source) {
          var pred = PredicateFromObject(item);
          preds.Add(pred);
        }
      }
      return preds;
    }

    private static BasePredicate? CreateCompoundPredicate(List<BasePredicate> preds) {
      if (preds.Count > 1) {
        return new AndOrPredicate(Operator.And, preds);
      } else if (preds.Count == 1) {
        return preds[0];
      } else {
        return null;
      }
    }

    /// <summary> Build the boolean expression for this predicate.  <see cref="Validate"/> must have run first. </summary>
    /// <param name="paramExpr">The query's lambda parameter.</param>
    /// <returns>An expression of type bool.</returns>
    public abstract Expression ToExpression(ParameterExpression paramExpr);

  }
}
