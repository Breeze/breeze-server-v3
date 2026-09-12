
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary>
  /// A where clause that quantifies over a collection navigation property - "any order detail
  /// costs more than 100", "all of them shipped".
  /// </summary>
  public class AnyAllPredicate : BasePredicate {

    /// <summary> The collection navigation property being quantified over, as the client wrote it. </summary>
    public Object ExprSource { get; private set; }
    /// <summary> The resolved navigation property.  Set by <see cref="Validate"/>; null before it runs. </summary>
    public PropBlock? NavPropBlock { get; private set; } // calculated as a result of validate; null before that
    /// <summary> The predicate applied to each item of the collection. </summary>
    public BasePredicate Predicate { get; private set; } 


    /// <summary> Quantify a predicate over a collection navigation property. </summary>
    /// <param name="op">Either <see cref="Operator.Any"/> or <see cref="Operator.All"/>.</param>
    /// <param name="exprSource">The path to the collection navigation property.</param>
    /// <param name="predicate">The predicate each item is tested against.</param>
    public AnyAllPredicate(Operator op, Object exprSource, BasePredicate predicate) : base(op) {
      ExprSource = exprSource;
      Predicate = predicate;
    }

    /// <summary> Resolve the navigation property and validate the inner predicate against the collection's item type. </summary>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">The path is not a property, or names something other than a collection navigation property.</exception>
    public override void Validate(Type entityType) {
      var block = BaseBlock.CreateLHSBlock(ExprSource, entityType);
      if (!(block is PropBlock)) {
        throw new Exception("The first expression of this AnyAllPredicate must be a PropertyExpression");
      }
      this.NavPropBlock = (PropBlock)block;
      var prop = NavPropBlock.Property;
      if (prop.IsDataProperty || prop.ElementType == null) {
        throw new Exception("The first expression of this AnyAllPredicate must be a nonscalar Navigation PropertyExpression");
      }

      
      this.Predicate.Validate(prop.ElementType);

    }

    /// <summary> Build a call to Enumerable.Any or Enumerable.All over the navigation property. </summary>
    /// <param name="paramExpr">The query's lambda parameter.</param>
    /// <returns>An expression of type bool.</returns>
    public override Expression ToExpression(ParameterExpression paramExpr) {
      // Validate() must run first: it sets NavPropBlock and rejects a null ElementType.
      var navExpr = NavPropBlock!.ToExpression(paramExpr);
      var elementType = NavPropBlock.Property.ElementType!;
      MethodInfo mi;
      if (Operator == Operator.Any) {
        mi = TypeFns.GetMethodByExample((IEnumerable<String> list) => list.Any(x => x != null), elementType);
      } else {
        mi = TypeFns.GetMethodByExample((IEnumerable<String> list) => list.All(x => x != null), elementType);
      }
      var lambdaExpr = Predicate.ToLambda(elementType);
      var result = Expression.Call(mi, navExpr, lambdaExpr);
      return result;
    }
  }
}
