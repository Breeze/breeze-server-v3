
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary>
  /// An operator that may appear in a query - a comparison, a boolean connective, or a
  /// collection quantifier - identified by the name a client sends for it.
  /// </summary>
  /// <remarks>
  /// Every operator is a singleton declared as one of the static fields below, and
  /// constructing one registers it so that <see cref="FromString"/> can find it. The names
  /// are part of the wire format and cannot be changed without breaking clients.
  /// </remarks>
  public class Operator {
    /// <summary> Every registered operator, keyed by its lower-cased <see cref="Name"/>.  Prefer <see cref="FromString"/> over reaching into this. </summary>
    public static Dictionary<String, Operator> _opMap = new Dictionary<String, Operator>();

    /// <summary> True when at least one item of a collection satisfies the inner predicate. </summary>
    public static Operator Any = new Operator("any,some", OperatorType.AnyAll);
    /// <summary> True when every item of a collection satisfies the inner predicate. </summary>
    public static Operator All = new Operator("all,every", OperatorType.AnyAll);
    /// <summary> Boolean conjunction of two or more predicates. </summary>
    public static Operator And = new Operator("and,&&", OperatorType.AndOr);
    /// <summary> Boolean disjunction of two or more predicates. </summary>
    public static Operator Or = new Operator("or,||", OperatorType.AndOr);
    /// <summary> Boolean negation of a single predicate. </summary>
    public static Operator Not = new Operator("not,!", OperatorType.Unary);

    /// <summary> Equality.  Declared <c>new</c> because it hides the inherited <see cref="object.Equals(object)"/>. </summary>
    public static new BinaryOperator Equals = new BinaryOperator("eq,==");
    /// <summary> Inequality. </summary>
    public static BinaryOperator NotEquals = new BinaryOperator("ne,!=");
    /// <summary> Ordered comparison, strictly less than. </summary>
    public static BinaryOperator LessThan = new BinaryOperator("lt,<");
    /// <summary> Ordered comparison, less than or equal. </summary>
    public static BinaryOperator LessThanOrEqual = new BinaryOperator("le,<=");
    /// <summary> Ordered comparison, strictly greater than. </summary>
    public static BinaryOperator GreaterThan = new BinaryOperator("gt,>");
    /// <summary> Ordered comparison, greater than or equal. </summary>
    public static BinaryOperator GreaterThanOrEqual = new BinaryOperator("ge,>=");

    /// <summary> String prefix test, compiled to <see cref="string.StartsWith(string)"/>. </summary>
    public static BinaryOperator StartsWith = new BinaryOperator("startswith");
    /// <summary> String suffix test, compiled to <see cref="string.EndsWith(string)"/>. </summary>
    public static BinaryOperator EndsWith = new BinaryOperator("endswith");
    /// <summary> Substring test, compiled to <see cref="string.Contains(string)"/>. </summary>
    public static BinaryOperator Contains = new BinaryOperator("contains");

    /// <summary> Set membership.  Its right-hand argument must be an array, and compiles to List&lt;T&gt;.Contains. </summary>
    public static BinaryOperator In = new BinaryOperator("in");
        
    /// <summary> The operator's primary name - the first of the aliases it was declared with, e.g. "eq". </summary>
    public String Name { get; private set; }
    /// <summary> Which kind of predicate this operator builds, and so how its operands are read. </summary>
    public OperatorType OpType { get; private set; }
    /// <summary> The names this operator was declared with; the first of them is its <see cref="Name"/>. </summary>
    public List<String> _aliases;

    /// <summary> Look up an operator by the name a client sent.  Matching is case-insensitive and is done on <see cref="Name"/>. </summary>
    /// <param name="op">The operator name, e.g. "eq" or "startswith".</param>
    /// <returns>The operator, or null if no operator is registered under that name.</returns>
    public static Operator? FromString(String op) {
      if (_opMap.ContainsKey(op.ToLowerInvariant())) {
        return _opMap[op.ToLowerInvariant()];
      } else {
        return null;
      }
    }

    /// <summary> Create an operator and register it for lookup by <see cref="FromString"/>. </summary>
    /// <param name="aliases">Comma-separated names for the operator; the first becomes its <see cref="Name"/>.</param>
    /// <param name="opType">Which kind of predicate the operator builds.</param>
    public Operator(String aliases, OperatorType opType) {
      _aliases = aliases.Split(',').ToList();
      Name = _aliases[0];
      OpType = opType;
      AddOperator(this);
    }


    private static void AddOperator(Operator op) {
      op._aliases.ForEach(a => _opMap[op.Name.ToLowerInvariant()] = op);
    }
  }

  /// <summary> An operator that compares two operands, such as <see cref="Operator.Equals"/> or <see cref="Operator.StartsWith"/>. </summary>
  public class BinaryOperator : Operator {
    /// <summary> Unused - no code assigns it, so it is always null. </summary>
    public Expression? Expression { get; private set; } // never assigned
    /// <summary> Create a binary operator and register it. </summary>
    /// <param name="name">Comma-separated names; the first becomes the operator's <see cref="Operator.Name"/>.</param>
    public BinaryOperator(String name) : base(name, OperatorType.Binary) {
      
    }

  }
}
