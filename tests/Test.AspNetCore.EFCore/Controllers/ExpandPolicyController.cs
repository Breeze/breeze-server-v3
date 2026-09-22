// Exercises ExpandPolicy - which navigations a client may expand.
//
// The model here is local to this file on purpose. ExpandPolicy is keyed by entity type and is
// process-wide, so declaring rules on the Northwind types would change what every other
// controller allows. These types are served by nothing else.
//
// The store is the EF Core in-memory provider rather than BreezeTestDb: expand becomes
// Include, which needs a real EF provider, and this demo should not require tables.

#if EFCORE

// The test host does not enable nullable reference types; this file does, so the demo model
// reads the way a real one would.
#nullable enable

using Breeze.AspNetCore;
using Breeze.Core;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Test.AspNetCore.Controllers {

  /// <summary> Orders may be expanded from here; Notes may not. </summary>
  [AllowExpand(nameof(Orders))]
  public class PolicyCustomer {
    public int PolicyCustomerID { get; set; }
    public string Name { get; set; } = "";
    public List<PolicyOrder> Orders { get; set; } = new List<PolicyOrder>();
    public List<PolicyNote> Notes { get; set; } = new List<PolicyNote>();
  }

  /// <summary> Everything except Agent, which carries a commission a customer must not see. </summary>
  [DenyExpand(nameof(Agent))]
  public class PolicyOrder {
    public int PolicyOrderID { get; set; }
    public int PolicyCustomerID { get; set; }
    public PolicyAgent? Agent { get; set; }
    public List<PolicyLine> Lines { get; set; } = new List<PolicyLine>();
  }

  /// <summary> No attributes: governed by the registration in the controller's static ctor. </summary>
  public class PolicyLine {
    public int PolicyLineID { get; set; }
    public int PolicyOrderID { get; set; }
    public PolicyProduct? Product { get; set; }
    public PolicySupplier? Supplier { get; set; }
  }

  public class PolicyAgent {
    public int PolicyAgentID { get; set; }
    public decimal Commission { get; set; }
  }

  public class PolicyProduct {
    public int PolicyProductID { get; set; }
    public string Name { get; set; } = "";
  }

  public class PolicySupplier {
    public int PolicySupplierID { get; set; }
    public string Name { get; set; } = "";
  }

  public class ExpandPolicyContext : DbContext {
    public ExpandPolicyContext(DbContextOptions<ExpandPolicyContext> options) : base(options) { }
    public DbSet<PolicyCustomer> PolicyCustomers { get; set; } = null!;
    public DbSet<PolicyOrder> PolicyOrders { get; set; } = null!;
  }

  /// <summary>
  /// A Breeze controller whose expandable navigations are declared rather than assumed.
  /// The policy is enforced by [BreezeQueryFilter] itself; nothing extra is applied here.
  /// </summary>
  [Route("breeze/[controller]/[action]")]
  [BreezeQueryFilter]
  public class ExpandPolicyController : Controller {

    static ExpandPolicyController() {
      // The other half of the API, for a model whose classes cannot carry attributes. An
      // allow-list means "these and no others", so Supplier is refused from PolicyLine.
      ExpandPolicy.Allow<PolicyLine>(l => l.Product!);
      ExpandPolicy.Validate(typeof(PolicyCustomer), typeof(PolicyOrder), typeof(PolicyLine));
    }

    private readonly ExpandPolicyContext _db;
    private readonly EFPersistenceManager<ExpandPolicyContext> _pm;

    public ExpandPolicyController() {
      var options = new DbContextOptionsBuilder<ExpandPolicyContext>()
        .UseInMemoryDatabase("ExpandPolicyDemo")
        .Options;
      _db = new ExpandPolicyContext(options);
      _pm = new EFPersistenceManager<ExpandPolicyContext>(_db);
      Seed(_db);
    }

    /// <summary> So a breeze client can build a model for these types and query them. </summary>
    [HttpGet]
    public IActionResult Metadata() {
      return Ok(_pm.Metadata());
    }

    [HttpGet]
    public IQueryable<PolicyCustomer> Customers() {
      return _db.PolicyCustomers;
    }

    [HttpGet]
    public IQueryable<PolicyOrder> Orders() {
      return _db.PolicyOrders;
    }

    private static void Seed(ExpandPolicyContext db) {
      if (db.PolicyCustomers.Any()) { return; }
      db.PolicyCustomers.Add(new PolicyCustomer {
        PolicyCustomerID = 1,
        Name = "Acme",
        Orders = new List<PolicyOrder> {
          new PolicyOrder {
            PolicyOrderID = 1,
            Agent = new PolicyAgent { PolicyAgentID = 1, Commission = 12.5m },
            Lines = new List<PolicyLine> {
              new PolicyLine {
                PolicyLineID = 1,
                Product = new PolicyProduct { PolicyProductID = 1, Name = "Widget" },
                Supplier = new PolicySupplier { PolicySupplierID = 1, Name = "Globex" },
              }
            }
          }
        },
        Notes = new List<PolicyNote> {
          new PolicyNote { PolicyNoteID = 1, Text = "internal" }
        },
      });
      db.SaveChanges();
    }
  }

  public class PolicyNote {
    public int PolicyNoteID { get; set; }
    public int PolicyCustomerID { get; set; }
    public string Text { get; set; } = "";
  }
}

#endif
