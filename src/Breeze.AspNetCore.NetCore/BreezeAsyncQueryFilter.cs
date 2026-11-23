
using Breeze.AspNetCore.NetCore;
using Breeze.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.AspNetCore {

  /// <summary> Attribute to apply the request's query string to the returned IQueryable and execute it asynchronously. </summary>
  /// <remarks> Put [BreezeAsyncQueryFilter] on a Controller class to apply Breeze query filtering
  /// and execution to each method that returns an IQueryable or IEnumerable.
  /// <para></para>
  /// You might want to use this class instead of [BreezeQueryFilter] to get the benefits of async tasks and cancellable queries.
  /// <para></para>
  /// The filter nearest the action runs the query; a limit it leaves unset comes from the nearest
  /// Breeze query filter at a wider scope, sync or async (see <see cref="MaxTake"/>).
  /// <para></para>
  /// See <see href="https://breeze.github.io/doc-net/webapi-controller-core#breezequeryfilterattribute"/>
  /// </remarks>
  public class BreezeAsyncQueryFilterAttribute : ActionFilterAttribute, IAsyncActionFilter, IBreezeQueryLimits {
    /// <summary> If true, OperationCanceledExceptions will be caught and an empty result will be returned. </summary>
    public bool CatchCancellations { get; set; }
    /// <summary> Status code when cancellation results are returned; default is 499 Client Closed Connection </summary>
    public int CancellationStatusCode { get; set; } = 499;
    /// <summary> If true and request is POST, then attempt to extract the Breeze query from the request body. </summary>
    /// <remarks> There is some overhead in extracting the request body, so UsePost should only be used on endpoints that are expected to have queries.
    /// If the request body has already been read (for example, due to model binding), it will need to be rewound before the filter can read it. </remarks>
    public bool UsePost { get; set; }

    /// <summary> Sets max depth for Select and Expand clauses.
    /// Set to 0 to disallow Selects and Expands requested by the client.
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
    /// filter, and a bare <c>[BreezeAsyncQueryFilter]</c> lifted both limits; it now keeps the wider ones.
    /// </para>
    /// </remarks>
    public int MaxTake { get => _maxTake ?? -1; set => _maxTake = value; }
    private int? _maxTake;

    int? IBreezeQueryLimits.MaxTakeSetting => _maxTake;
    int? IBreezeQueryLimits.MaxDepthSetting => _maxDepth;

    /// <summary> Extract the IQueryable from the context, apply the query, and execute it. </summary>
    override public async Task OnActionExecutionAsync(ActionExecutingContext executingContext, ActionExecutionDelegate next) {

      OnActionExecuting(executingContext);
      if (!executingContext.ModelState.IsValid) {
        executingContext.Result = new BadRequestObjectResult(executingContext.ModelState);
        return;
      }

      var executedContext = await next();

      // don't attempt to process queryable if we are throwing an error
      if (executedContext.Result is IStatusCodeActionResult scar && scar.StatusCode >= 400) {
        return;
      }

      // don't process queryable if flag has been set for this request
      if (QueryFns.IsSkipBreezeQueryFilter(executedContext)) {
        base.OnActionExecuted(executedContext);
        return;
      }

      var qs = QueryFns.ExtractAndDecodeQueryString(executedContext, UsePost);
      var queryable = QueryFns.ExtractQueryable(executedContext);
      // The provider's query steps (EF's Include, ...), before anything below uses them.
      QueryProviderHooks.EnsureInstalled(queryable);
      var (maxTake, maxDepth) = BreezeQueryFilterAttribute.EffectiveLimits(executedContext, this);

      // Both checks are false for a null queryable, so testing it here changes nothing at
      // runtime - it is what tells the compiler queryable is non-null from here on.
      if (queryable == null || (!EntityQuery.NeedsExecution(qs, queryable) && !BreezeQueryFilterAttribute.NeedsMaxTake(queryable, maxTake))) {
        base.OnActionExecuted(executedContext);
        return;
      }

      var eq = new EntityQuery(qs);
      var eleType = TypeFns.GetElementType(queryable.GetType())!;   // a queryable has an element type
      eq.Validate(eleType);

      var msg = BreezeQueryFilterAttribute.CheckMaxDepth(eq, maxDepth);
      if (msg != null) {
        executedContext.Result = new BadRequestObjectResult(msg);
        return;
      }

      msg = BreezeQueryFilterAttribute.CheckExpandPolicy(eq, eleType);
      if (msg != null) {
        executedContext.Result = new BadRequestObjectResult(msg);
        return;
      }

      var originalQueryable = queryable;
      queryable = eq.ApplyWhere(queryable, eleType);

      try {
        int? inlineCount = null;
        if (eq.IsInlineCountEnabled) {
          inlineCount = await queryable.Cast<dynamic>().CountAsync(executedContext.HttpContext.RequestAborted);
        }

        queryable = EntityQuery.ApplyCustomLogic(eq, queryable, eleType);
        queryable = eq.ApplyOrderBy(queryable, eleType);
        queryable = eq.ApplySkip(queryable, eleType);
        queryable = eq.ApplyTake(queryable, eleType);
        queryable = BreezeQueryFilterAttribute.ApplyMaxTake(queryable, eleType, maxTake);
        queryable = eq.ApplySelect(queryable, eleType);
        queryable = EntityQuery.ApplyExpand(eq, queryable, eleType);

        if (queryable != originalQueryable) {
          // if a select or expand was encountered we need to
          // execute the DbQueries here, so that any exceptions thrown can be properly returned.
          // if we wait to have the query executed within the serializer, some exceptions will not
          // serialize properly.
          var result = await queryable.Cast<dynamic>().ToListAsync(executedContext.HttpContext.RequestAborted);
          var listResult = EntityQuery.AfterExecution(eq, queryable, result);

          var qr = new QueryResult(listResult, inlineCount);
          executedContext.Result = new ObjectResult(qr);
        }

      } catch (OperationCanceledException) {
        if (CatchCancellations) {
          executedContext.Result = GetEmptyResult();
        } else {
          throw;
        }
      }

      OnActionExecuted(executedContext);
    }

    private ObjectResult GetEmptyResult() {
      var emptyResult = new QueryResult(Enumerable.Empty<dynamic>(), null);
      return new ObjectResult(emptyResult) { StatusCode = CancellationStatusCode };
    }

  }

}

