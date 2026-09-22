// Snippets for docs/guide/security.md.

using Breeze.AspNetCore;
using Breeze.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Docs.Snippets {

  #region ControllerAttributes
  [Route("breeze/[controller]/[action]")]
  [Authorize]
  [BreezeQueryFilter(MaxTake = 1000, MaxDepth = 2)]
  #endregion
  public class SecureOrdersController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    public SecureOrdersController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    /// <summary> The signed-in user's customer, from their claims - never from the request body. </summary>
    private Guid CurrentCustomerId {
      get {
        var claim = User.FindFirst("customerId");
        // Fail closed: a principal without the claim gets no rows and saves nothing,
        // rather than a null reference somewhere further in.
        if (claim == null) { throw new UnauthorizedAccessException("No customerId claim."); }
        return Guid.Parse(claim.Value);
      }
    }

    #region UnboundedQuery
    // Every order in the database, to anyone who can reach the endpoint.
    [HttpGet]
    public IQueryable<Order> Orders() {
      return _pm.Context.Orders;
    }
    #endregion

    #region NamedQuery
    // The most this endpoint can return is one customer's orders. Whatever the client
    // asks for is applied on top of that, so it can only narrow the set further.
    [HttpGet]
    public IQueryable<Order> MyOrders() {
      return _pm.Context.Orders.Where(o => o.CustomerID == CurrentCustomerId);
    }
    #endregion

    #region AuthorizeSave
    [HttpPost]
    public Task<SaveResult> SaveChanges([FromBody] JObject saveBundle) {
      _pm.BeforeSaveEntityDelegate = AuthorizeEntity;
      return _pm.SaveChangesAsync(saveBundle);
    }

    private bool AuthorizeEntity(EntityInfo info) {
      // The bundle names its own types, and the lookup reaches every entity type your
      // application has loaded - not just the ones this controller is about. So decide
      // what is saveable here rather than assuming.
      if (!(info.Entity is Order order)) {
        throw Forbid(info, "This endpoint saves orders only.");
      }

      // For an update, the client sent CustomerID like every other property, so testing
      // what arrived only proves the client claims to own the row. Read the stored value.
      if (info.EntityState == EntityState.Modified || info.EntityState == EntityState.Deleted) {
        var ownerOnRecord = _pm.Context.Orders
          .Where(o => o.OrderID == order.OrderID)
          .Select(o => o.CustomerID)
          .FirstOrDefault();
        if (ownerOnRecord != CurrentCustomerId) {
          throw Forbid(info, "That order belongs to someone else.");
        }
      }

      // For an insert, the client does not get to choose whose it is.
      order.CustomerID = CurrentCustomerId;
      return true;
    }
    #endregion

    #region ServerControlledFields
    // A modified entity is written from the JSON the client sent, so a property the user
    // may not set has to be put back rather than merely left out of the UI.
    private bool ResetServerControlledFields(EntityInfo info) {
      if (info.Entity is Order order && info.EntityState == EntityState.Added) {
        order.OrderDate = DateTime.UtcNow;
        order.Freight = CalculateFreight(order);
      }
      return true;
    }
    #endregion

    private static decimal CalculateFreight(Order order) {
      return 0m;
    }

    private static EntityErrorsException Forbid(EntityInfo info, string message) {
      return new EntityErrorsException(message, new[] {
        new EntityError {
          ErrorName = "NotAuthorized",
          EntityTypeName = info.Entity.GetType().FullName,
          ErrorMessage = message,
        }
      });
    }

    internal void Suppress() {
      ResetServerControlledFields(null!);
    }
  }

  #region AllowedTypes
  // The same decision for a whole batch, when several types are legitimately saveable.
  public static class SaveGuard {
    private static readonly HashSet<Type> Saveable = new HashSet<Type> {
      typeof(Order), typeof(Customer),
    };

    public static Dictionary<Type, List<EntityInfo>> RejectOtherTypes(
        Dictionary<Type, List<EntityInfo>> saveMap) {
      var unexpected = saveMap.Keys.Where(t => !Saveable.Contains(t)).ToList();
      if (unexpected.Any()) {
        throw new EntityErrorsException(
          "These types cannot be saved here: " + string.Join(", ", unexpected.Select(t => t.Name)),
          new List<EntityError>());
      }
      return saveMap;
    }
  }
  #endregion
}
