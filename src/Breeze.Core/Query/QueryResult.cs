using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary> A page of query results together with the total row count, which is what a query with inlineCount returns. </summary>
  public class QueryResult {

    /// <summary> Pair a set of results with the total count they were drawn from. </summary>
    /// <param name="results">The rows to return, after skip and take.</param>
    /// <param name="inlineCount">The number of rows that matched before skip and take; null when the query did not ask for a count.</param>
    public QueryResult(IEnumerable results, int? inlineCount = null) {
      Results = results;
      InlineCount = inlineCount;
    }

    /// <summary> The rows returned, after skip and take were applied. </summary>
    public IEnumerable Results {
      get; private set;
    }

    /// <summary> The number of rows matching the query before skip and take, or null if it was not requested. </summary>
    public int? InlineCount {
      get; private set;
    }
  }

}
