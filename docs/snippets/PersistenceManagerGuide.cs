// Snippets for docs/guide/persistence-manager.md.

using Breeze.Persistence;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Docs.Snippets {

  [Route("breeze/[controller]/[action]")]
  public class PersistenceManagerGuideController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    #region ControllerConstructor
    public PersistenceManagerGuideController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }
    #endregion

    #region ContextQuery
    [HttpGet]
    public IQueryable<Customer> Customers() => _pm.Context.Customers;
    #endregion

    #region PerRequestInterceptor
    [HttpPost]
    public Task<SaveResult> SaveWithAudit([FromBody] JObject saveBundle) {
      _pm.BeforeSaveEntityDelegate = SetAuditFields;
      return _pm.SaveChangesAsync(saveBundle);
    }
    #endregion

    private bool SetAuditFields(EntityInfo info) => true;

    internal void SetKeyGenerator() {
      #region KeyGenerator
      _pm.KeyGenerator = new NumericKeyGenerator((DbConnection)_pm.GetDbConnection());
      #endregion
    }
  }

  #region SubclassInterceptor
  public class AuditedPersistenceManager : EFPersistenceManager<NorthwindContext> {
    public AuditedPersistenceManager(NorthwindContext context) : base(context) { }

    protected override bool BeforeSaveEntity(EntityInfo entityInfo) {
      // return false to drop this entity from the save
      return true;
    }
  }
  #endregion

  #region AltMetadata
  public class AltMetadataPersistenceManager : EFPersistenceManager<NorthwindContext> {
    public AltMetadataPersistenceManager(NorthwindContext context) : base(context) { }

    protected override string? BuildAltJsonMetadata() {
      return "{ \"uiHints\": { \"Customer\": { \"icon\": \"person\" } } }";
    }
  }
  #endregion
}
