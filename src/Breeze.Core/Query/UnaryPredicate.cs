using System;
using System.Linq.Expressions;

namespace Breeze.Core {

  /// <summary> A where clause that negates another predicate. </summary>
  public class UnaryPredicate : BasePredicate {
    
    /// <summary> The predicate being negated. </summary>
    public BasePredicate Predicate { get; private set; }
  
    /// <summary> Negate a predicate. </summary>
    /// <param name="op">The unary operator, i.e. <see cref="Operator.Not"/>.</param>
    /// <param name="predicate">The predicate to negate.</param>
    public UnaryPredicate(Operator op, BasePredicate predicate) : base(op) {
      Predicate = predicate;
    }
  

    /// <summary> Validate the negated predicate against the entity type. </summary>
    /// <param name="entityType">The type the query returns.</param>
    public override void Validate(Type entityType) {
      Predicate.Validate(entityType);
    }


    /// <summary> Build the negation of the inner predicate. </summary>
    /// <param name="paramExpr">The query's lambda parameter.</param>
    /// <returns>An expression of type bool.</returns>
    public override Expression ToExpression(ParameterExpression paramExpr) {
      var expr = Predicate.ToExpression(paramExpr);
      return Expression.Not(expr);
    }
  }

  }