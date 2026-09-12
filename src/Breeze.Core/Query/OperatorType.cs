
namespace Breeze.Core {
  /// <summary> The kind of predicate an <see cref="Operator"/> builds, which decides how its operands are read. </summary>
  public enum OperatorType {

    /// <summary> Quantifies over a collection navigation property - any or all. </summary>
    AnyAll = 1,
    /// <summary> Joins two or more predicates - and, or. </summary>
    AndOr = 2,
    /// <summary> Compares two operands - equality, ordering, the string tests, in. </summary>
    Binary = 3,
    /// <summary> Applies to a single predicate - not. </summary>
    Unary = 4
  }
}