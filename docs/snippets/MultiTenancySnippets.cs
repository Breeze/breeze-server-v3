// Snippets for the Hardening and Multi-tenancy sections of docs/guide/security.md.
//
// A second, self-contained model: the tenant-owned types the multi-tenancy examples need.
// Nothing here runs - see DOCS.md.

using Breeze.AspNetCore;
using Breeze.Persistence;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using EntityState = Breeze.Persistence.EntityState;

namespace Breeze.Docs.Snippets.MultiTenant {

  #region TenantOwned
  /// <summary> Implemented by every entity that belongs to one tenant. </summary>
  public interface ITenantOwned {
    Guid TenantId { get; set; }
  }
  #endregion

  public class Invoice : ITenantOwned {
    public int InvoiceID { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
  }

  public class InvoiceLine : ITenantOwned {
    public int InvoiceLineID { get; set; }
    public Guid TenantId { get; set; }
    public int InvoiceID { get; set; }
    public Invoice? Invoice { get; set; }
  }

  #region TenantContext
  /// <summary> The tenant this request belongs to. </summary>
  public interface ITenantContext {
    Guid TenantId { get; }
  }

  /// <summary>
  /// The tenant comes from the signed-in principal's claims, which the client cannot choose.
  /// A subdomain, header or route value can all be set by the caller, so none of them is an
  /// identity - at most they select which login to demand.
  /// </summary>
  public class ClaimsTenantContext : ITenantContext {
    private readonly IHttpContextAccessor _http;

    public ClaimsTenantContext(IHttpContextAccessor http) {
      _http = http;
    }

    public Guid TenantId {
      get {
        var claim = _http.HttpContext?.User?.FindFirst("tenantId");
        // Fail closed. No tenant means no rows and no saves, not "all rows".
        if (claim == null) { throw new UnauthorizedAccessException("No tenant on this principal."); }
        return Guid.Parse(claim.Value);
      }
    }
  }
  #endregion

  #region TenantQueryFilters
  public class BillingContext : DbContext {
    private readonly Guid _tenantId;

    // AddDbContext resolves the extra parameter from DI, so every context is built for
    // the tenant of the request it belongs to.
    public BillingContext(DbContextOptions<BillingContext> options, ITenantContext tenant)
        : base(options) {
      _tenantId = tenant.TenantId;
    }

    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
      // One filter per tenant-owned type. A type left out has no boundary at all, so this
      // list is the thing to check when a new entity is added to the model.
      // Comparing against the field, not a literal, is what makes EF parameterise it.
      modelBuilder.Entity<Invoice>().HasQueryFilter(i => i.TenantId == _tenantId);
      modelBuilder.Entity<InvoiceLine>().HasQueryFilter(l => l.TenantId == _tenantId);
    }
  }
  #endregion

  #region TenantSaveGuard
  public class BillingPersistenceManager : EFPersistenceManager<BillingContext> {
    private readonly ITenantContext _tenant;

    public BillingPersistenceManager(BillingContext context, ITenantContext tenant) : base(context) {
      _tenant = tenant;
    }

    // Query filters do not apply to saves, so the tenant boundary has to be re-stated here.
    protected override Dictionary<Type, List<EntityInfo>> BeforeSaveEntities(
        Dictionary<Type, List<EntityInfo>> saveMap) {

      foreach (var info in saveMap.SelectMany(kvp => kvp.Value)) {
        if (!(info.Entity is ITenantOwned owned)) {
          throw Reject($"{info.Entity.GetType().Name} has no tenant and cannot be saved here.");
        }

        if (info.EntityState != EntityState.Added && !BelongsToThisTenant(info)) {
          // Same answer whether the row is another tenant's or does not exist, so a client
          // cannot use the error to discover which keys are real.
          throw Reject("No such record.");
        }

        // Stamped, never taken from the payload - on insert and on update alike, because an
        // update rewrites every mapped column from what the client sent.
        owned.TenantId = _tenant.TenantId;
      }

      return saveMap;
    }

    /// <summary>
    /// Whether the stored row is this tenant's. The query runs through the filtered DbSet, so
    /// another tenant's row is simply not there and no comparison is needed.
    /// </summary>
    private bool BelongsToThisTenant(EntityInfo info) {
      // Any() rather than a lookup that materialises the entity: this runs before Breeze
      // attaches the client's instance, and a tracked row with the same key would collide
      // with it. See the warning in security.md.
      switch (info.Entity) {
        case Invoice invoice:
          return Context.Invoices.Any(i => i.InvoiceID == invoice.InvoiceID);
        case InvoiceLine line:
          return Context.InvoiceLines.Any(l => l.InvoiceLineID == line.InvoiceLineID);
        default:
          return false;
      }
    }

    private static EntityErrorsException Reject(string message) {
      return new EntityErrorsException(message, new List<EntityError>());
    }
  }
  #endregion

  internal static class Registration {

    internal static void AddTenancy(WebApplicationBuilder builder) {
      #region TenantRegistration
      builder.Services.AddHttpContextAccessor();
      builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();

      // Scoped, so the context and its query filters belong to one request's tenant.
      builder.Services.AddDbContext<BillingContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("Billing")));
      #endregion
    }

    internal static void AddGlobalQueryLimits(WebApplicationBuilder builder) {
      #region GlobalQueryLimits
      builder.Services.AddControllers(o => {
        // Registered once, so a controller added later is bounded whether or not its author
        // thought about it. Actions that do not return a queryable are unaffected.
        o.Filters.Add(new BreezeQueryFilterAttribute { MaxTake = 1000, MaxDepth = 2 });
      });
      #endregion
    }
  }
}
