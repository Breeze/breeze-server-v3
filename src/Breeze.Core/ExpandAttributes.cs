using System;

namespace Breeze.Core {

  /// <summary>
  /// Names the navigation properties a client may expand from this entity type. Anything not
  /// named is refused.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A path is judged one hop at a time, by the type each hop starts from, so
  /// <c>Orders.OrderDetails</c> needs <c>Orders</c> allowed on <c>Customer</c> and
  /// <c>OrderDetails</c> allowed on <c>Order</c>. Each type therefore declares only its own
  /// outbound navigations; nobody writes whole paths.
  /// </para>
  /// <para>
  /// An allow-list means "these and no others". Use <see cref="DenyExpandAttribute"/> instead
  /// where a type has many navigations and only a few are sensitive.
  /// </para>
  /// <example>
  /// <code>
  /// [AllowExpand(nameof(Orders))]
  /// public class Customer {
  ///   public ICollection&lt;Order&gt; Orders { get; set; }
  ///   public ICollection&lt;Note&gt; Notes { get; set; }   // refused
  /// }
  /// </code>
  /// </example>
  /// <para>
  /// <see cref="ExpandPolicy"/> has the equivalent for a model whose classes are generated and
  /// cannot carry attributes, and decides what happens when both are present.
  /// </para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
  public class AllowExpandAttribute : Attribute {

    /// <summary> Name the navigations a client may expand from this type. </summary>
    /// <param name="navigations">The navigation property names, as declared on this type.</param>
    public AllowExpandAttribute(params string[] navigations) {
      Navigations = navigations;
    }

    /// <summary> The navigation property names this type permits. </summary>
    public string[] Navigations { get; }
  }

  /// <summary>
  /// Names navigation properties a client may not expand from this entity type. Everything else
  /// is still permitted.
  /// </summary>
  /// <remarks>
  /// The counterpart of <see cref="AllowExpandAttribute"/>, and the better choice when a type has
  /// many navigations and only one or two must not be reachable - an <c>Order</c> whose
  /// <c>Employee</c> carries salary, say. A name that appears in both attributes on the same type
  /// is a contradiction and throws.
  /// <example>
  /// <code>
  /// [DenyExpand(nameof(Employee))]
  /// public class Order {
  ///   public Employee Employee { get; set; }              // refused
  ///   public ICollection&lt;OrderDetail&gt; OrderDetails { get; set; }
  /// }
  /// </code>
  /// </example>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
  public class DenyExpandAttribute : Attribute {

    /// <summary> Name the navigations a client may not expand from this type. </summary>
    /// <param name="navigations">The navigation property names, as declared on this type.</param>
    public DenyExpandAttribute(params string[] navigations) {
      Navigations = navigations;
    }

    /// <summary> The navigation property names this type refuses. </summary>
    public string[] Navigations { get; }
  }
}
