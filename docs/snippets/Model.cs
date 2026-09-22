// The model the guide's examples are written against: a cut-down Northwind, just enough
// for the snippets to compile. Nothing here appears in the documentation.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Breeze.Docs.Snippets {

  public class Customer {
    public Guid CustomerID { get; set; }
    [Required, MaxLength(40)]
    public string CompanyName { get; set; } = null!;
    public string? City { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
  }

  public class Order {
    public int OrderID { get; set; }
    public Guid? CustomerID { get; set; }
    public Customer? Customer { get; set; }
    public decimal? Freight { get; set; }
    public DateTime? OrderDate { get; set; }
  }

  // Small, static reference tables, for the lookups example in querying.md.

  public class Region {
    public int RegionID { get; set; }
    public string RegionDescription { get; set; } = null!;
  }

  public class Territory {
    public int TerritoryID { get; set; }
    public string TerritoryDescription { get; set; } = null!;
    public int RegionID { get; set; }
  }

  public class Category {
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = null!;
  }

  public class NorthwindContext : DbContext {
    public NorthwindContext(DbContextOptions<NorthwindContext> options) : base(options) { }
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Region> Regions { get; set; } = null!;
    public DbSet<Territory> Territories { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
  }

  /// <summary> Implemented by the entities that SetAuditFields stamps. </summary>
  public interface IAudited {
    string? CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string? ModifiedBy { get; set; }
    DateTime ModifiedAt { get; set; }
  }
}
