using Breeze.AspNetCore;
using Breeze.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

// The query hooks are process-wide statics, and one test resets them on purpose: run the tests
// one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Breeze.AspNetCore.Tests {

  // ---- A small model: customers, their orders, the orders' lines --------------------------------

  public class Customer {
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public List<Order> Orders { get; set; } = new List<Order>();
  }

  public class Order {
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public List<Line> Lines { get; set; } = new List<Line>();
  }

  public class Line {
    public int LineId { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public string Product { get; set; } = "";
  }

  public class ShopContext : DbContext {
    public ShopContext(DbContextOptions<ShopContext> options) : base(options) { }
    public DbSet<Customer> Customers => Set<Customer>();
  }

  /// <summary>
  /// An in-process server with the given global query filter (or none), over an in-memory store
  /// of five customers with two orders of two lines each. Nothing here creates a persistence
  /// manager: the queries go through the filters alone, as a first request after a restart does.
  /// </summary>
  public sealed class TestHost : IAsyncDisposable {
    public const int Customers = 5;

    private readonly WebApplication _app;
    public HttpClient Client { get; }

    private TestHost(WebApplication app) {
      _app = app;
      Client = app.GetTestClient();
    }

    public static async Task<TestHost> StartAsync(Action<Microsoft.AspNetCore.Mvc.MvcOptions>? globalFilters = null) {
      var builder = WebApplication.CreateBuilder();
      builder.WebHost.UseTestServer();
      builder.Logging.ClearProviders();

      var database = Guid.NewGuid().ToString();
      builder.Services.AddDbContext<ShopContext>(o => o.UseInMemoryDatabase(database));
      builder.Services.AddControllers(o => globalFilters?.Invoke(o))
        .AddApplicationPart(typeof(TestHost).Assembly)
        .AddNewtonsoftJson(o => JsonSerializationFns.UpdateWithDefaults(o.SerializerSettings));

      var app = builder.Build();
      app.MapControllers();
      await app.StartAsync();

      using (var scope = app.Services.CreateScope()) {
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var lineId = 0;
        for (var c = 1; c <= Customers; c++) {
          db.Customers.Add(new Customer {
            CustomerId = c,
            Name = $"Customer {c}",
            Orders = Enumerable.Range(1, 2).Select(o => new Order {
              OrderId = c * 10 + o,
              Lines = Enumerable.Range(1, 2).Select(l => new Line { LineId = ++lineId, Product = $"Product {l}" }).ToList()
            }).ToList()
          });
        }
        db.SaveChanges();
      }

      return new TestHost(app);
    }

    /// <summary> A Breeze query, as the client sends it: the JSON query is the whole query string. </summary>
    public async Task<(HttpStatusCode Status, string Text, List<JToken> Rows)> QueryAsync(string path, object? query = null) {
      var url = path + (query == null ? "" : "?" + Uri.EscapeDataString(JObject.FromObject(query).ToString(Newtonsoft.Json.Formatting.None)));
      using var response = await Client.GetAsync(url);
      var text = await response.Content.ReadAsStringAsync();
      if (!response.IsSuccessStatusCode) {
        return (response.StatusCode, text, new List<JToken>());
      }
      var body = JToken.Parse(text);
      // A QueryResult (the filter ran the query) or a plain array (it had nothing to do).
      var rows = (body is JObject result ? result["Results"]! : body).Children().ToList();
      return (response.StatusCode, text, rows);
    }

    public async ValueTask DisposeAsync() {
      Client.Dispose();
      await _app.DisposeAsync();
    }
  }

  // ---- Controllers --------------------------------------------------------------------------------

  /// <summary> No Breeze attribute: only a global filter, if the host registers one, applies. </summary>
  [Route("plain/[action]")]
  public class PlainController : ControllerBase {
    private readonly ShopContext _db;
    public PlainController(ShopContext db) { _db = db; }

    [HttpGet] public IQueryable<Customer> Customers() => _db.Customers.OrderBy(c => c.CustomerId);
  }

  /// <summary> A controller attribute that sets MaxTake only, and actions that set less, more or nothing. </summary>
  [Route("scoped/[action]")]
  [BreezeQueryFilter(MaxTake = 4)]
  public class ScopedController : ControllerBase {
    private readonly ShopContext _db;
    public ScopedController(ShopContext db) { _db = db; }

    /// <summary> The controller's attribute alone. </summary>
    [HttpGet] public IQueryable<Customer> Customers() => _db.Customers.OrderBy(c => c.CustomerId);

    /// <summary> A bare attribute on the action: everything comes from wider scopes. </summary>
    [HttpGet, BreezeQueryFilter] public IQueryable<Customer> Bare() => _db.Customers.OrderBy(c => c.CustomerId);

    /// <summary> Sets MaxDepth only. </summary>
    [HttpGet, BreezeQueryFilter(MaxDepth = 2)] public IQueryable<Customer> Deeper() => _db.Customers.OrderBy(c => c.CustomerId);

    /// <summary> Lifts the row cap explicitly. </summary>
    [HttpGet, BreezeQueryFilter(MaxTake = -1)] public IQueryable<Customer> Unlimited() => _db.Customers.OrderBy(c => c.CustomerId);
  }

  /// <summary> The async filter, bare, on the controller. </summary>
  [Route("async/[action]")]
  [BreezeAsyncQueryFilter]
  public class AsyncController : ControllerBase {
    private readonly ShopContext _db;
    public AsyncController(ShopContext db) { _db = db; }

    [HttpGet] public IQueryable<Customer> Customers() => _db.Customers.OrderBy(c => c.CustomerId);
  }
}
