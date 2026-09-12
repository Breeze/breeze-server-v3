
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary> A function call in a query, such as "toupper(CompanyName)" or "year(OrderDate)". </summary>
  /// <remarks>
  /// The functions a query may call are fixed, registered by the static constructor below and
  /// listed by name together with their return and argument types. They follow the OData set:
  /// the string functions, the date parts, and rounding.
  /// </remarks>
  public class FnBlock : BaseBlock {
    /// <summary> The name of the function being called, e.g. "toupper". </summary>
    public String FnName { get; private set; }
    private List<BaseBlock> _exprs;

    // first DataType in the list is the return type the rest are argument
    // types;
    private static Dictionary<String, DataType[]> _fnMap = new Dictionary<String, DataType[]>();
    static FnBlock() {
      RegisterFn("toupper", DataType.String, DataType.String);
      RegisterFn("tolower", DataType.String, DataType.String);
      RegisterFn("trim", DataType.String, DataType.String);
      RegisterFn("concat", DataType.String, DataType.String, DataType.String);
      RegisterFn("substring", DataType.String, DataType.String,
              DataType.Int32, DataType.Int32);
      RegisterFn("replace", DataType.String, DataType.String, DataType.String);
      RegisterFn("length", DataType.Int32, DataType.String);
      RegisterFn("indexof", DataType.Int32, DataType.String, DataType.String);

      RegisterFn("year", DataType.Int32, DataType.DateTime);
      RegisterFn("month", DataType.Int32, DataType.DateTime);
      RegisterFn("day", DataType.Int32, DataType.DateTime);
      RegisterFn("hour", DataType.Int32, DataType.DateTime);
      RegisterFn("minute", DataType.Int32, DataType.DateTime);
      RegisterFn("second", DataType.Int32, DataType.DateTime);

      RegisterFn("round", DataType.Int32, DataType.Double);
      RegisterFn("ceiling", DataType.Int32, DataType.Double);
      RegisterFn("floor", DataType.Int32, DataType.Double);

      RegisterFn("substringof", DataType.Boolean, DataType.String,
              DataType.String);
      RegisterFn("startsWith", DataType.Boolean, DataType.String,
              DataType.String);
      RegisterFn("endsWith", DataType.Boolean, DataType.String,
              DataType.String);
    }

    /// <summary> Create a call to a registered function. </summary>
    /// <param name="fnName">The function name.</param>
    /// <param name="exprs">The argument blocks, in order.</param>
    public FnBlock(String fnName, List<BaseBlock> exprs) {
      FnName = fnName;
      _exprs = exprs;
    }

    /// <summary> Parse a function call written as text. </summary>
    /// <param name="source">The call, e.g. "toupper(CompanyName)".</param>
    /// <param name="entityType">The entity type any property arguments are resolved against.</param>
    /// <returns>The parsed call.</returns>
    public static FnBlock CreateFrom(String source, Type entityType) {
      return FnBlockToken.ToExpression(source, entityType);
    }

    /// <summary> The arguments passed to the function, in order. </summary>
    public IEnumerable<BaseBlock> Expressions {
      get { return _exprs.AsReadOnly(); }
    }

    /// <summary> The data type the function returns. </summary>
    public override DataType? DataType {
      get {
        return GetReturnType(FnName);
      }
    }

    /// <summary> Register a function that queries may call.  Names are matched case-insensitively. </summary>
    /// <param name="name">The function name as clients will write it.</param>
    /// <param name="dataTypes">The return type first, then one type per argument.</param>
    public static void RegisterFn(String name, params DataType[] dataTypes) {
      _fnMap[name.ToLowerInvariant()] = dataTypes;
    }

    /// <summary> The type a registered function returns. </summary>
    /// <param name="fnName">The function name.</param>
    /// <returns>The return type.</returns>
    /// <exception cref="KeyNotFoundException">No function is registered under that name.</exception>
    public static DataType? GetReturnType(String fnName) {
      DataType[] dataTypes = _fnMap[fnName.ToLowerInvariant()];
      return (dataTypes != null) ? dataTypes[0] : null;
    }

    /// <summary> The argument types a registered function takes, in order. </summary>
    /// <param name="fnName">The function name.</param>
    /// <returns>One type per argument.</returns>
    /// <exception cref="KeyNotFoundException">No function is registered under that name.</exception>
    public static List<DataType> GetArgTypes(String fnName) {
      DataType[] dataTypes = _fnMap[fnName.ToLowerInvariant()];
      if (dataTypes == null) {
        throw new Exception("Unable to recognize a function named: "
                + fnName);
      }
      return dataTypes.Skip(1).ToList();
    }

    /// <summary> Build the call as a LINQ expression, mapping the function name onto the matching CLR method or property. </summary>
    /// <param name="inExpr">The query's lambda parameter.</param>
    /// <returns>The expression that computes the function's result.</returns>
    /// <exception cref="Exception">The function is registered but has no expression mapping here.</exception>
    public override Expression ToExpression(Expression inExpr) {
      var exprs = _exprs.Select(e => e.ToExpression(inExpr)).ToList();
      var expr = exprs[0];
      // TODO: add the rest ...
      if (FnName == "toupper") {
        var mi = TypeFns.GetMethodByExample((String s) => s.ToUpper());
        return Expression.Call(expr, mi);
      } else if (FnName == "tolower") {
        var mi = TypeFns.GetMethodByExample((String s) => s.ToLower());
        return Expression.Call(expr, mi);
      } else if (FnName == "trim") {
        var mi = TypeFns.GetMethodByExample((String s) => s.Trim());
        return Expression.Call(expr, mi);
      } else if (FnName == "length") {
        return GetPropertyExpression(expr, "Length", typeof(int));
      } else if (FnName == "indexof") {
        var mi = TypeFns.GetMethodByExample((String s) => s.IndexOf("xxx"));
        return Expression.Call(exprs[0], mi, exprs[1]);
      } else if (FnName == "concat") {
        // TODO: check if this works...
        var mi = TypeFns.GetMethodByExample((String s) => String.Concat(s, "xxx"));
        return Expression.Call(mi, exprs[0], exprs[1]);
      } else if (FnName == "substring") {
        var mi = TypeFns.GetMethodByExample((String s) => s.Substring(1, 5));
        return Expression.Call(exprs[0], mi, exprs.Skip(1));
      } else if (FnName == "replace") {
        // TODO: check if this works...
        var mi = TypeFns.GetMethodByExample((String s) => s.Replace("aaa", "bbb"));
        return Expression.Call(exprs[0], mi, exprs[1], exprs[2]);
      } else if (FnName == "year") {
        return GetPropertyExpression(expr, "Year", typeof(int));
      } else if (FnName == "month") {
        return GetPropertyExpression(expr, "Month", typeof(int));
      } else if (FnName == "day") {
        return GetPropertyExpression(expr, "Day", typeof(int));
      } else if (FnName == "hour") {
        return GetPropertyExpression(expr, "Hour", typeof(int));
      } else if (FnName == "minute") {
        return GetPropertyExpression(expr, "Minute", typeof(int));
      } else if (FnName == "second") {
        return GetPropertyExpression(expr, "Second", typeof(int));
      } else if (FnName == "round") {
          // TODO: confirm that this works - is using static method.
          var mi = TypeFns.GetMethodByExample((Double d) => Math.Round(d));
          return Expression.Call(mi, expr);
      } else if (FnName == "ceiling") {
        var mi = TypeFns.GetMethodByExample((Double d) => Math.Ceiling(d));
        return Expression.Call(mi, expr);
      } else if (FnName == "floor") {
        var mi = TypeFns.GetMethodByExample((Double d) => Math.Floor(d));
        return Expression.Call(mi, expr);
      } else if (FnName == "startswith") {
        var mi = TypeFns.GetMethodByExample((String s) => s.StartsWith("xxx"));
        return Expression.Call(exprs[0], mi, exprs[1]);
      } else if (FnName == "endsWith") {
        var mi = TypeFns.GetMethodByExample((String s) => s.EndsWith("xxx"));
        return Expression.Call(exprs[0], mi, exprs[1]);
      } else if (FnName == "substringof") {
        var mi = TypeFns.GetMethodByExample((String s) => s.Contains("xxx"));
        return Expression.Call(exprs[0], mi, exprs[1]);
      } else {
        throw new Exception("Unable to locate Fn: " + FnName);
      }
    }

    private Expression GetPropertyExpression(Expression expr, string propertyName, Type returnType) {
      if (TypeFns.IsNullableType(expr.Type)) {
        var nullBaseExpression = Expression.Constant(null, expr.Type);
        var test = Expression.Equal(expr, nullBaseExpression);
        expr = Expression.Convert(expr, TypeFns.GetNonNullableType(expr.Type));
        Expression propExpr = Expression.PropertyOrField(expr, propertyName);
        // returnType is always int here, which has a nullable counterpart.
        propExpr = Expression.Convert(propExpr, TypeFns.GetNullableType(returnType)!);
        var nullExpr = Expression.Constant(null, TypeFns.GetNullableType(returnType)!);
        return Expression.Condition(test, nullExpr, propExpr);
      } else {
        return Expression.PropertyOrField(expr, propertyName);
      }
    }
  }
}