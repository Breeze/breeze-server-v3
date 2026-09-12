using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {

  /// <summary> A where clause joining two or more predicates with <c>and</c> or <c>or</c>. </summary>
  public class AndOrPredicate : BasePredicate {
    private List<BasePredicate> _predicates;

    /// <summary> Join predicates with a boolean connective. </summary>
    /// <param name="op">Either <see cref="Operator.And"/> or <see cref="Operator.Or"/>.</param>
    /// <param name="predicates">The predicates to join.</param>
    public AndOrPredicate(Operator op, params BasePredicate[] predicates) : this(op, predicates.ToList()) {
    }

    /// <summary> Join predicates with a boolean connective. </summary>
    /// <param name="op">Either <see cref="Operator.And"/> or <see cref="Operator.Or"/>.</param>
    /// <param name="predicates">The predicates to join.</param>
    public AndOrPredicate(Operator op, IEnumerable<BasePredicate> predicates) : base(op) {
      _predicates = predicates.ToList();
    }

    
    /// <summary> The predicates being joined. </summary>
    public IEnumerable<BasePredicate> Predicates {
      get { return _predicates.AsReadOnly(); }
    }

    /// <summary> Validate every joined predicate against the entity type. </summary>
    /// <param name="entityType">The type the query returns.</param>
    public override void Validate(Type entityType) {
      _predicates.ForEach(p => p.Validate(entityType));
    }

    /// <summary> Combine the joined predicates into one expression with AndAlso or OrElse. </summary>
    /// <param name="paramExpr">The query's lambda parameter.</param>
    /// <returns>An expression of type bool.</returns>
    /// <exception cref="Exception">The operator is neither <c>and</c> nor <c>or</c>.</exception>
    public override Expression ToExpression(ParameterExpression paramExpr) {
      var exprs = _predicates.Select(p => p.ToExpression(paramExpr));
      return BuildAndOrExpr(exprs, Operator);
      
    }

    private Expression BuildAndOrExpr(IEnumerable<Expression> exprs, Operator op) {
      if (op == Operator.And) {
        return exprs.Aggregate((result, expr) => Expression.AndAlso(result, expr));
      } else if (op == Operator.Or) {
        return exprs.Aggregate((result, expr) => Expression.OrElse(result, expr));
      } else {
        throw new Exception("Invalid AndOr operator " + op.Name);
      }
    }

  }
}
