using Breeze.Core;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Breeze.AspNetCore.Tests {

  /// <summary>
  /// An expand works on the first query, before anything has created a persistence manager.
  /// </summary>
  /// <remarks>
  /// <see cref="EntityQuery.ApplyExpand"/> is a no-op until a persistence package installs its own.
  /// EF Core's used to be installed only by <c>EFPersistenceManager</c>'s static constructor - a
  /// Metadata request or a save - so a query that came first got 200 with every expanded
  /// navigation empty. Nothing in this test project creates a persistence manager.
  /// </remarks>
  public class ExpandBeforeAnyPersistenceManagerTests {

    [Fact]
    public async Task An_expand_is_applied_on_the_first_query() {
      ResetQueryHooks();
      await using var host = await TestHost.StartAsync(o => o.Filters.Add(new BreezeQueryFilterAttribute()));

      var (status, text, rows) = await host.QueryAsync("/plain/Customers", new { expand = new[] { "Orders.Lines" } });

      Assert.True(status == HttpStatusCode.OK, text);
      var first = rows.First();
      Assert.Equal(2, first["Orders"]!.Count());
      Assert.Equal(2, first["Orders"]![0]!["Lines"]!.Count());
    }

    [Fact]
    public async Task The_async_filter_applies_it_too() {
      ResetQueryHooks();
      await using var host = await TestHost.StartAsync();

      var (status, text, rows) = await host.QueryAsync("/async/Customers", new { expand = new[] { "Orders" } });

      Assert.True(status == HttpStatusCode.OK, text);
      Assert.Equal(2, rows.First()["Orders"]!.Count());
    }

    /// <summary>
    /// Put the hooks back as they are when the process starts, as far as EF is concerned: the
    /// no-op expand, and no provider handled yet. Another test may already have installed them.
    /// </summary>
    private static void ResetQueryHooks() {
      EntityQuery.ApplyExpand = (eq, iq, type) => iq;
      EntityQuery.ApplyCustomLogic = (eq, iq, type) => iq;
      var handled = (ConcurrentDictionary<Type, bool>)typeof(QueryProviderHooks)
        .GetField("Handled", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;
      handled.Clear();
    }
  }

  /// <summary>
  /// A Breeze query filter on a controller or action keeps the limits of a wider one for whatever
  /// it leaves unset. The filter nearest the action runs the query and the wider ones then have
  /// nothing to do, so before, an attribute that left a limit unset meant "unlimited" - a bare
  /// <c>[BreezeQueryFilter]</c> lifted a global filter's limits entirely.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The global filter here: at most 3 rows, one level of expand. The <c>scoped</c> controller sets
  /// MaxTake = 4 and nothing else.
  /// </para>
  /// <para>
  /// Every query has a <c>where</c>. The nearest filter answers only if applying the query changes
  /// the queryable; one it leaves unchanged (an expand while expand is a no-op, say) passes the
  /// queryable on, and the next filter out applies its own limits. Without the <c>where</c>, a test
  /// could pass on the global filter's limits for the wrong reason.
  /// </para>
  /// </remarks>
  public class InheritedLimitsTests : IAsyncLifetime {
    private TestHost _host = null!;

    public async Task InitializeAsync() {
      _host = await TestHost.StartAsync(o => o.Filters.Add(new BreezeQueryFilterAttribute { MaxTake = 3, MaxDepth = 1 }));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    /// <summary> Every customer, so the query changes the queryable; with the given expands. </summary>
    internal static object Query(params string[] expand) =>
      expand.Length == 0
        ? new { where = new { CustomerId = new { gt = 0 } } }
        : new { where = new { CustomerId = new { gt = 0 } }, expand };

    [Fact]
    public async Task A_controller_without_an_attribute_gets_the_global_limits() {
      Assert.Equal(3, (await _host.QueryAsync("/plain/Customers", Query())).Rows.Count);
      Assert.Equal(HttpStatusCode.BadRequest, (await _host.QueryAsync("/plain/Customers", Query("Orders.Lines"))).Status);
    }

    [Fact]
    public async Task A_controller_attribute_replaces_what_it_sets_and_keeps_the_rest() {
      // MaxTake 4 from the controller, MaxDepth 1 from the global filter.
      Assert.Equal(4, (await _host.QueryAsync("/scoped/Customers", Query())).Rows.Count);
      Assert.Equal(HttpStatusCode.OK, (await _host.QueryAsync("/scoped/Customers", Query("Orders"))).Status);
      Assert.Equal(HttpStatusCode.BadRequest, (await _host.QueryAsync("/scoped/Customers", Query("Orders.Lines"))).Status);
    }

    [Fact]
    public async Task A_bare_action_attribute_keeps_the_wider_limits() {
      Assert.Equal(4, (await _host.QueryAsync("/scoped/Bare", Query())).Rows.Count);

      var (status, text, _) = await _host.QueryAsync("/scoped/Bare", Query("Orders.Lines"));
      Assert.Equal(HttpStatusCode.BadRequest, status);
      Assert.Contains("MaxDepth exceeded: Orders.Lines", text);
    }

    [Fact]
    public async Task An_action_can_raise_one_limit_and_keep_the_other() {
      var (status, text, rows) = await _host.QueryAsync("/scoped/Deeper", Query("Orders.Lines"));
      Assert.True(status == HttpStatusCode.OK, text);
      Assert.Equal(4, rows.Count);
      Assert.Equal(2, rows.First()["Orders"]![0]!["Lines"]!.Count());
    }

    [Fact]
    public async Task An_explicit_minus_one_still_means_unlimited() {
      Assert.Equal(TestHost.Customers, (await _host.QueryAsync("/scoped/Unlimited", Query())).Rows.Count);
    }

    [Fact]
    public async Task A_bare_async_attribute_keeps_the_global_limits() {
      Assert.Equal(3, (await _host.QueryAsync("/async/Customers", Query())).Rows.Count);
      Assert.Equal(HttpStatusCode.BadRequest, (await _host.QueryAsync("/async/Customers", Query("Orders.Lines"))).Status);
    }
  }

  /// <summary>
  /// Without a global filter: a limit no filter sets is unlimited, as before; one the controller's
  /// attribute sets still reaches a bare attribute on the action.
  /// </summary>
  public class WithoutAGlobalFilterTests : IAsyncLifetime {
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_bare_attribute_is_unlimited() {
      var (status, text, rows) = await _host.QueryAsync("/async/Customers", InheritedLimitsTests.Query("Orders.Lines"));
      Assert.True(status == HttpStatusCode.OK, text);
      Assert.Equal(TestHost.Customers, rows.Count);
    }

    [Fact]
    public async Task A_bare_action_attribute_takes_the_controllers_limits() {
      Assert.Equal(4, (await _host.QueryAsync("/scoped/Bare", InheritedLimitsTests.Query())).Rows.Count);
      Assert.Equal(HttpStatusCode.OK, (await _host.QueryAsync("/scoped/Bare", InheritedLimitsTests.Query("Orders.Lines"))).Status);
    }
  }
}
