using Breeze.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Linq;
using System.Security.AccessControl;

namespace Breeze.AspNetCore {

  /// <summary> Attribute to apply the request's query string to the returned IQueryable and execute it synchronously.</summary>
  /// <remarks> Put [BreezeQueryFilter] on a Controller class to apply Breeze query filtering
  /// and execution to each method that returns an IQueryable or IEnumerable.
  /// <para></para>
  /// You might want to use this class instead of [BreezeAsyncQueryFilter] because of https://github.com/dotnet/efcore/issues/18221
  /// <para></para>
  /// Register one globally (<c>options.Filters.Add(new BreezeQueryFilterAttribute { MaxTake = 1000, MaxDepth = 2 })</c>)
  /// to bound every query action, and put one on a controller or action to change the limits there.
  /// The filter nearest the action runs the query; a limit it leaves unset comes from the nearest
  /// Breeze query filter at a wider scope (see <see cref="MaxTake"/>).
  /// <para></para>
  /// See <see href="https://breeze.github.io/doc-net/webapi-controller-core#breezequeryfilterattribute"/>
  /// </remarks>
  public class BreezeQueryFilterAttribute : ActionFilterAttribute, IBreezeQueryLimits {
    /// <summary> If true and request is POST, then attempt to extract the Breeze query from the request body. </summary>
    /// <remarks> There is some overhead in extracting the request body, so UsePost should only be used on endpoints that are expected to have queries.
    /// If the request body has already been read (for example, due to model binding), it will need to be rewound before the filter can read it. </remarks>
    public bool UsePost { get; set; }

    /// <summary> Sets max depth for Select and Expand clauses.<br/>
    /// Set to 0 to disallow Selects and Expands requested by the client.<br/>
    /// Set to -1 to allow unlimited depth.<br/>
    /// Left unset, it comes from the nearest Breeze query filter at a wider scope (the controller's, or
    /// one registered globally), and is unlimited if there is none. It reads -1 while unset.<br/>
    /// Returns 400 Bad Request if MaxDepth is violated. <br/>
    /// MaxDepth = 1 on IQueryable&lt;Customer&gt; will allow Expand = "Orders" but not "Orders.OrderDetails" <br/>
    /// MaxDepth = 0 on IQueryable&lt;Customer&gt; will allow Select = "Name" but not Select = "Orders" or Expand = "Orders"
    /// </summary>
    public int MaxDepth { get => _maxDepth ?? -1; set => _maxDepth = value; }
    private int? _maxDepth;

    /// <summary> Sets max rows allowed; adds Take(MaxTake) to query if Take is unspecified or if Take > MaxTake.<br/>
    /// Set to 100 to allow max of 100 rows on any query.<br/>
    /// Set to -1 to allow unlimited rows.<br/>
    /// Set to 0 to not return any rows (but why would you do that?).<br/>
    /// Left unset, it comes from the nearest Breeze query filter at a wider scope (the controller's, or
    /// one registered globally), and is unlimited if there is none. It reads -1 while unset.<br/>
    /// NOTE: MaxTake does not affect Expand/Include subqueries, nor non-EF results (arrays, Lists, etc.)
    /// </summary><remarks>
    /// MaxTake can be used to limit result size if the client accidently (or maliciously) does not apply any filtering.<br/>
    /// MaxTake is not the same as adding a Take() clause in the controller method, because MaxTake is applied after any
    /// Where and OrderBy provided in the request.
    /// <para>
    /// The filter nearest the action runs the query, so a wider filter's limits never reach it on
    /// their own. Before, an attribute that left a limit unset meant "unlimited" even under a global
    /// filter, and a bare <c>[BreezeQueryFilter]</c> lifted both limits; it now keeps the wider ones.
    /// </para>
    /// </remarks>
    public int MaxTake { get => _maxTake ?? -1; set => _maxTake = value; }
    private int? _maxTake;

    int? IBreezeQueryLimits.MaxTakeSetting => _maxTake;
    int? IBreezeQueryLimits.MaxDepthSetting => _maxDepth;

    /// <summary> Check if context.ModelState is valid </summary>
    public override void OnActionExecuting(ActionExecutingContext context) {
      if (!context.ModelState.IsValid) {
        context.Result = new BadRequestObjectResult(context.ModelState);
      }
    }

    /// <summary> Extract the IQueryable from the context, apply the query, and execute it. </summary>
    public override void OnActionExecuted(ActionExecutedContext context) {

      // don't attempt to process queryable if we are throwing an error or cancelling
      if (context.Result is IStatusCodeActionResult scar && scar.StatusCode >= 400) {
        base.OnActionExecuted(context);
        return;
      }

      // don't process queryable if flag has been set for this request
      if (QueryFns.IsSkipBreezeQueryFilter(context)) {
        base.OnActionExecuted(context);
        return;
      }

      var qs = QueryFns.ExtractAndDecodeQueryString(context, UsePost);
      var queryable = QueryFns.ExtractQueryable(context);
      // The provider's query steps (EF's Include, ...), before anything below uses them.
      QueryProviderHooks.EnsureInstalled(queryable);
      var (maxTake, maxDepth) = EffectiveLimits(context, this);

      // Both checks are false for a null queryable, so testing it here changes nothing at
      // runtime - it is what tells the compiler queryable is non-null from here on.
      if (queryable == null || (!EntityQuery.NeedsExecution(qs, queryable) && !NeedsMaxTake(queryable, maxTake))) {
        base.OnActionExecuted(context);
        return;
      }

      var eq = new EntityQuery(qs);
      var eleType = TypeFns.GetElementType(queryable.GetType())!;   // a queryable has an element type
      eq.Validate(eleType);

      var msg = CheckMaxDepth(eq, maxDepth);
      if (msg != null) {
        context.Result = new BadRequestObjectResult(msg);
        return;
      }

      msg = CheckExpandPolicy(eq, eleType);
      if (msg != null) {
        context.Result = new BadRequestObjectResult(msg);
        return;
      }

      var originalQueryable = queryable;
      queryable = eq.ApplyWhere(queryable, eleType);

      int? inlineCount = null;
      if (eq.IsInlineCountEnabled) {
        inlineCount = (int)Queryable.Count((dynamic)queryable);
      }

      if (context.HttpContext.RequestAborted.IsCancellationRequested) {
        context.Result = GetEmptyResult();
        base.OnActionExecuted(context);
        return;
      }

      queryable = EntityQuery.ApplyCustomLogic(eq, queryable, eleType);
      queryable = eq.ApplyOrderBy(queryable, eleType);
      queryable = eq.ApplySkip(queryable, eleType);
      queryable = eq.ApplyTake(queryable, eleType);
      queryable = ApplyMaxTake(queryable, eleType, maxTake);
      queryable = eq.ApplySelect(queryable, eleType);
      queryable = EntityQuery.ApplyExpand(eq, queryable, eleType);

      if (queryable != originalQueryable) {
        // if a select or expand was encountered we need to
        // execute the DbQueries here, so that any exceptions thrown can be properly returned.
        // if we wait to have the query executed within the serializer, some exceptions will not
        // serialize properly.
        var listResult = Enumerable.ToList((dynamic)queryable);
        listResult = EntityQuery.AfterExecution(eq, queryable, listResult);

        var qr = new QueryResult(listResult, inlineCount);
        context.Result = new ObjectResult(qr);
      }

      base.OnActionExecuted(context);

    }

    /// <summary>
    /// The limits a Breeze query filter applies: its own where set, otherwise those of the nearest
    /// Breeze query filter at a wider scope, otherwise -1 (unlimited).
    /// </summary>
    /// <remarks>
    /// MVC orders <see cref="FilterContext.Filters"/> by scope, global first, so the wider filters
    /// are the ones before <paramref name="self"/>. Found by reference: <see cref="System.Attribute.Equals(object)"/>
    /// compares field values, so two attributes with the same limits would be "equal".
    /// </remarks>
    internal static (int MaxTake, int MaxDepth) EffectiveLimits(FilterContext context, IBreezeQueryLimits self) {
      int? maxTake = self.MaxTakeSetting;
      int? maxDepth = self.MaxDepthSetting;

      var filters = context.Filters;
      var index = -1;
      for (var i = 0; i < filters.Count; i++) {
        if (ReferenceEquals(filters[i], self)) { index = i; break; }
      }

      for (var i = index - 1; i >= 0 && (maxTake == null || maxDepth == null); i--) {
        if (filters[i] is IBreezeQueryLimits wider) {
          maxTake ??= wider.MaxTakeSetting;
          maxDepth ??= wider.MaxDepthSetting;
        }
      }

      return (maxTake ?? -1, maxDepth ?? -1);
    }

    /// <summary> Produce empty result for request cancellations </summary>
    private ObjectResult GetEmptyResult() {
      var emptyResult = new QueryResult(Enumerable.Empty<dynamic>(), null);
      return new ObjectResult(emptyResult) { StatusCode = 499 };
    }

    /// <summary>
    /// Check each expand path against <see cref="ExpandPolicy"/>, which is a no-op until the
    /// application declares something. Covers expand only; select is MaxDepth's business.
    /// </summary>
    internal static string? CheckExpandPolicy(EntityQuery eq, System.Type eleType) {
      if (eq.ExpandClause == null) { return null; }

      foreach (var path in eq.ExpandClause.PropertyPaths) {
        var forbidden = ExpandPolicy.FirstForbiddenHop(eleType, path);
        if (forbidden != null) {
          return $"Expand not allowed: {forbidden}";
        }
      }
      return null;
    }

    /// <summary> Check select and expand to see if MaxDepth is exceeded </summary>
    internal static string? CheckMaxDepth(EntityQuery eq, int maxDepth) {
      if (maxDepth >= 0 && eq.SelectClause != null) {
        // check selects
        foreach (var sp in eq.SelectClause.Properties) {
          // okay to exceed the count by one, iff selecting a data property
          if (sp.Properties.Count > maxDepth + 1 || (sp.Properties.Count == maxDepth + 1 && !sp.IsDataProperty)) {
            return $"MaxDepth exceeded: {sp.PropertyPath}";
          }
        }
      }

      if (maxDepth >= 0 && eq.ExpandClause != null) {
        // check expands
        foreach (var path in eq.ExpandClause.PropertyPaths) {
          var sp = path.Split('/', '.');
          // okay to exceed the count by one, iff selecting a data property
          if (sp.Length > maxDepth) {
            return $"MaxDepth exceeded: {path}";
          }
        }
      }

      return null;
    }

    /// <summary> Check query and apply Take(maxTake) if needed. </summary>
    internal static IQueryable ApplyMaxTake(IQueryable queryable, System.Type eleType, int maxTake) {
      if (!NeedsMaxTake(queryable, maxTake)) { return queryable; }
      var curTake = EntityQueryExtensions.GetTakeValue(queryable);
      if (curTake == null || curTake.Value > maxTake) {
        queryable = QueryBuilder.ApplyTake(queryable, eleType, maxTake);
      }
      return queryable;
    }

    /// <summary> Check query and apply Take(maxTake) if needed. </summary>
    internal static bool NeedsMaxTake(IQueryable? queryable, int maxTake) {
      if (maxTake < 0 || queryable == null) { return false; }
      if (!queryable.GetType().FullName!.Contains("EntityFramework")) { return false; }
      return true;
    }


  }

}

