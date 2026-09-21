// Snippets for docs/guide/querying.md. Each region is a complete, balanced unit, so what the
// guide renders is something a reader can paste.

using Breeze.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Breeze.Docs.Snippets {

  #region FilteredController
  [Route("breeze/[controller]/[action]")]
  [BreezeQueryFilter]
  public class CustomerQueryController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    public CustomerQueryController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    [HttpGet]
    public IQueryable<Customer> Customers() => _pm.Context.Customers;
  }
  #endregion

  [Route("breeze/[controller]/[action]")]
  [BreezeQueryFilter]
  public class MoreQueryingController : Controller {
    private readonly NorthwindPersistenceManager _pm;

    public MoreQueryingController(NorthwindContext context) {
      _pm = new NorthwindPersistenceManager(context);
    }

    #region ParameterizedQuery
    [HttpGet]
    public IQueryable<Customer> CustomersStartingWith(string companyName) {
      return _pm.Context.Customers.Where(c => c.CompanyName.StartsWith(companyName));
    }
    #endregion

    #region SkipFilter
    [HttpGet]
    public IQueryable<Customer> UnfilteredCustomers() {
      this.SkipBreezeQueryFilter();
      return _pm.Context.Customers;
    }
    #endregion
  }

  #region AsyncFilterAttribute
  [BreezeAsyncQueryFilter(CatchCancellations = true)]
  #endregion
  public class AsyncQueryingController : Controller { }

  #region MaxTakeAttribute
  [BreezeQueryFilter(MaxTake = 1000)]
  #endregion
  public class MaxTakeController : Controller { }

  #region MaxDepthAttribute
  [BreezeQueryFilter(MaxDepth = 2)]
  #endregion
  public class MaxDepthController : Controller { }

  #region UsePostAttribute
  [BreezeQueryFilter(UsePost = true)]
  #endregion
  public class UsePostController : Controller { }
}
