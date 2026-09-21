// The controller shown in getting-started.md. The usings sit inside the namespace so that
// the region can carry them: the guide's first controller is the place a reader needs them.

namespace Breeze.Docs.Snippets {
  #region Controller
  using Breeze.AspNetCore;
  using Breeze.Persistence;
  using Microsoft.AspNetCore.Mvc;
  using Newtonsoft.Json.Linq;
  using System.Linq;
  using System.Threading.Tasks;

  [Route("breeze/[controller]/[action]")]
  [BreezeQueryFilter]
  public class NorthwindController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    public NorthwindController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    [HttpGet]
    public IActionResult Metadata() => Ok(_pm.Metadata());

    [HttpPost]
    public Task<SaveResult> SaveChanges([FromBody] JObject saveBundle)
      => _pm.SaveChangesAsync(saveBundle);

    [HttpGet]
    public IQueryable<Customer> Customers() => _pm.Context.Customers;

    [HttpGet]
    public IQueryable<Order> Orders() => _pm.Context.Orders;
  }
  #endregion
}
