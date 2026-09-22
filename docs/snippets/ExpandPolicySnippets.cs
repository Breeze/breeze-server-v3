// Snippets for the expand-policy section of docs/guide/security.md.

using Breeze.Core;
using System;
using System.Collections.Generic;

namespace Breeze.Docs.Snippets.Expand {

  #region AttributeDeclaration
  [AllowExpand(nameof(Orders))]
  public class Customer {
    public Guid CustomerID { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<InternalNote> Notes { get; set; } = new List<InternalNote>();
  }

  // Everything except Employee, which carries a salary a customer must not reach.
  [DenyExpand(nameof(Employee))]
  public class Order {
    public int OrderID { get; set; }
    public Employee? Employee { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
  }
  #endregion

  public class Employee { public decimal Salary { get; set; } }
  public class OrderDetail { public int Id { get; set; } }
  public class InternalNote { public string Text { get; set; } = ""; }

  internal static class Registration {

    internal static void Configure() {
      #region RegistrationApi
      // The same rules for a model whose classes are generated, or to override what they say.
      ExpandPolicy.Allow<Customer>(c => c.Orders);
      ExpandPolicy.Deny<Order>(o => o.Employee!);

      // Resolve now, so a contradiction is reported at startup rather than on the request
      // that happens to touch it.
      ExpandPolicy.Validate(typeof(Customer), typeof(Order));
      #endregion
    }

    internal static void DenyByDefault() {
      #region DenyByDefault
      // Nothing is expandable unless something says so. Review the whole model first.
      ExpandPolicy.DenyByDefault = true;
      #endregion
    }
  }
}
