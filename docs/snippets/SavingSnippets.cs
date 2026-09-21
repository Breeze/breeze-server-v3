// Snippets for docs/guide/saving.md.
//
// Breeze.Persistence.EntityState and Microsoft.EntityFrameworkCore.EntityState are both in
// scope in an EF application, so this file imports neither namespace wholesale for that name
// and aliases the Breeze one, which is what the save hooks receive.

using Breeze.Persistence;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EntityState = Breeze.Persistence.EntityState;

namespace Breeze.Docs.Snippets {

  [Route("breeze/[controller]/[action]")]
  public class SavingController : Controller {
    private readonly NorthwindPersistenceManager _pm;
    private readonly string _user = "someone";

    public SavingController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    #region SaveChangesAction
    [HttpPost]
    public Task<SaveResult> SaveChanges([FromBody] JObject saveBundle)
      => _pm.SaveChangesAsync(saveBundle);
    #endregion

    #region TransactionSettings
    [HttpPost]
    public Task<SaveResult> SaveInTransaction([FromBody] JObject saveBundle) {
      var settings = new TransactionSettings { TransactionType = TransactionType.DbTransaction };
      return _pm.SaveChangesAsync(saveBundle, settings);
    }
    #endregion

    internal void ReadTag() {
      #region SaveOptionsTag
      var tag = _pm.SaveOptions?.Tag as string;
      if (tag == "publish") { /* ... */ }
      #endregion
    }

    #region BeforeSaveEntity
    private bool SetAuditFields(EntityInfo info) {
      if (info.Entity is IAudited audited) {
        if (info.EntityState == EntityState.Added) {
          audited.CreatedBy = _user;
          audited.CreatedAt = DateTime.UtcNow;
        }
        audited.ModifiedBy = _user;
        audited.ModifiedAt = DateTime.UtcNow;
      }
      return true;   // false drops this entity from the save
    }
    #endregion

    #region BeforeSaveEntities
    private Dictionary<Type, List<EntityInfo>> CheckOrders(
        Dictionary<Type, List<EntityInfo>> saveMap) {

      if (saveMap.TryGetValue(typeof(Order), out var orders)) {
        foreach (var info in orders) {
          var order = (Order)info.Entity;
          if (order.Freight > 1000) throw new InvalidOperationException("Freight too high");
        }
      }
      return saveMap;
    }
    #endregion

    internal void Suppress() => SetAuditFields(null!);
  }

  #region ValidateOnSave
  public class ValidatingPersistenceManager : Breeze.Persistence.EFCore.EFPersistenceManager<NorthwindContext> {
    public ValidatingPersistenceManager(NorthwindContext context) : base(context) { }

    protected override Dictionary<Type, List<EntityInfo>> BeforeSaveEntities(
        Dictionary<Type, List<EntityInfo>> saveMap) {
      var validator = new DataAnnotationsValidator(this);
      validator.ValidateEntities(saveMap, throwIfInvalid: true);
      return saveMap;
    }
  }
  #endregion
}
