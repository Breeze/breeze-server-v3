// Snippets for docs/guide/querying.md. Each region is a complete, balanced unit, so what the
// guide renders is something a reader can paste.

using Breeze.AspNetCore;
using Breeze.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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

    #region Lookups
    // One request, three lists. The return type is object, not IQueryable, so the query
    // filter finds nothing to act on and passes the bag through untouched.
    [HttpGet]
    public object Lookups() {
      var regions = _pm.Context.Regions;
      var territories = _pm.Context.Territories;
      var categories = _pm.Context.Categories;

      return new { regions, territories, categories };
    }
    #endregion

    #region LookupsMaterialized
    [HttpGet]
    public object LookupsEagerly() {
      // ToList() runs each query here rather than inside the serializer, so a failure
      // becomes an error response instead of a truncated one - and the sizes are yours
      // to check before they go on the wire.
      return new {
        regions = _pm.Context.Regions.ToList(),
        territories = _pm.Context.Territories.ToList(),
        categories = _pm.Context.Categories.ToList(),
      };
    }
    #endregion
  }

  internal static class AnonymousTypeJson {
    internal static void Configure(WebApplicationBuilder builder) {
      #region AnonBinder
      builder.Services.AddControllers().AddNewtonsoftJson(opt => {
        var settings = JsonSerializationFns.UpdateWithDefaults(opt.SerializerSettings);
        // Keeps the anonymous wrapper's assembly-qualified name out of the payload.
        settings.SerializationBinder = new NoAnonSerializationBinder();
      });
      #endregion
    }
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
