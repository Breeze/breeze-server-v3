

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;


namespace Breeze.Core {

  /// <summary>
  /// Used to build up a Queryable.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public class QueryBuilder {

    /// <summary> Apply a where clause by building the Queryable.Where call for the element type. </summary>
    /// <param name="source">The queryable to filter.</param>
    /// <param name="elementType">The element type of the queryable.</param>
    /// <param name="predicate">The validated where clause.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable ApplyWhere(IQueryable source, Type elementType, BasePredicate predicate) {
      var method = TypeFns.GetMethodByExample((IQueryable<String> q) => q.Where(s => s != null), elementType);
      var lambdaExpr = predicate.ToLambda(elementType);
      var func = BuildIQueryableFunc(elementType, method, lambdaExpr);
      return func(source);
    }



    /// <summary> Apply a select clause, projecting onto a type generated to hold exactly the selected properties. </summary>
    /// <param name="source">The queryable to project.</param>
    /// <param name="elementType">The element type of the queryable.</param>
    /// <param name="selectClause">The validated select clause.</param>
    /// <returns>A queryable over the generated projection type.</returns>
    public static IQueryable ApplySelect(IQueryable source, Type elementType, SelectClause selectClause) {
      var propSigs = selectClause.Properties;
      var dti = DynamicTypeInfo.FindOrCreate(propSigs.Select(ps => ps.Name), propSigs.Select(ps => ps.ReturnType));
      var lambdaExpr = CreateNewLambda(dti, propSigs);
      var method = TypeFns.GetMethodByExample((IQueryable<String> q) => q.Select(s => s.Length), elementType, dti.DynamicType);
      var func = BuildIQueryableFunc(elementType, method, lambdaExpr);
      return func(source);
    }

    /// <summary> Apply an orderBy clause, using OrderBy for the first term and ThenBy for the rest. </summary>
    /// <param name="source">The queryable to sort.</param>
    /// <param name="elementType">The element type of the queryable.</param>
    /// <param name="orderByClause">The validated orderBy clause.</param>
    /// <returns>The sorted queryable.</returns>
    /// <exception cref="Exception">A sort path does not resolve to a property.</exception>
    public static IQueryable ApplyOrderBy(IQueryable source, Type elementType, OrderByClause orderByClause) {
      var orderByItems = orderByClause.OrderByItems;
      var isThenBy = false;
      orderByItems.ToList().ForEach(obi => {
        var funcOb = QueryBuilder.BuildOrderByFunc(isThenBy, elementType, obi);
        source = funcOb(source);
        isThenBy = true;
      });
      return source;
    }

    /// <summary> Apply Queryable.Skip for the element type. </summary>
    /// <param name="source">The queryable to skip within.</param>
    /// <param name="elementType">The element type of the queryable.</param>
    /// <param name="skipCount">The number of rows to skip.</param>
    /// <returns>The queryable with Skip applied.</returns>
    public static IQueryable ApplySkip(IQueryable source, Type elementType, int skipCount) {
      var method = TypeFns.GetMethodByExample((IQueryable<String> q) => Queryable.Skip<String>(q, 999), elementType);
      var func = BuildIQueryableFunc(elementType, method, skipCount);
      return func(source);
    }

    /// <summary> Apply Queryable.Take for the element type. </summary>
    /// <param name="source">The queryable to limit.</param>
    /// <param name="elementType">The element type of the queryable.</param>
    /// <param name="takeCount">The number of rows to take.</param>
    /// <returns>The queryable with Take applied.</returns>
    public static IQueryable ApplyTake(IQueryable source, Type elementType, int takeCount) {
      var method = TypeFns.GetMethodByExample((IQueryable<String> q) => Queryable.Take<String>(q, 999), elementType);
      var func = BuildIQueryableFunc(elementType, method, takeCount);
      return func(source);
    }

    // TODO: Check if the ThenBy portion of this works
    private static Func<IQueryable, IQueryable> BuildOrderByFunc(bool isThenBy, Type elementType, OrderByClause.OrderByItem obi) {
      var propertyPath = obi.PropertyPath;
      bool isDesc = obi.IsDesc;
      var paramExpr = Expression.Parameter(elementType, "o");
      Expression nextExpr = paramExpr;
      var propertyNames = propertyPath.Split('.').ToList();
      propertyNames.ForEach(pn => {
        var nextElementType = nextExpr.Type;
        var propertyInfo = nextElementType.GetTypeInfo().GetProperty(pn);
        if (propertyInfo == null) {
          throw new Exception("Unable to locate property: " + pn + " on type: " + nextElementType.ToString());
        }
        nextExpr = Expression.MakeMemberAccess(nextExpr, propertyInfo);
      });
      var lambdaExpr = Expression.Lambda(nextExpr, paramExpr);

      var orderByMethod = GetOrderByMethod(isThenBy, isDesc, elementType, nextExpr.Type);

      var baseType = isThenBy ? typeof(IOrderedQueryable<>) : typeof(IQueryable<>);
      var func = BuildIQueryableFunc(elementType, orderByMethod, lambdaExpr, baseType);
      return func;
    }



    private static MethodInfo GetOrderByMethod(bool isThenBy, bool isDesc, Type elementType, Type nextExprType) {
      MethodInfo orderByMethod;
      if (isThenBy) {
        orderByMethod = isDesc
                          ? TypeFns.GetMethodByExample(
                            (IOrderedQueryable<String> q) => q.ThenByDescending(s => s.Length),
                            elementType, nextExprType)
                          : TypeFns.GetMethodByExample(
                            (IOrderedQueryable<String> q) => q.ThenBy(s => s.Length),
                            elementType, nextExprType);
      } else {
        orderByMethod = isDesc
                          ? TypeFns.GetMethodByExample(
                            (IQueryable<String> q) => q.OrderByDescending(s => s.Length),
                            elementType, nextExprType)
                          : TypeFns.GetMethodByExample(
                            (IQueryable<String> q) => q.OrderBy(s => s.Length),
                            elementType, nextExprType);
      }
      return orderByMethod;
    }

    private static LambdaExpression CreateNewLambda(DynamicTypeInfo dti, IEnumerable<PropertySignature> selectors) {
      var paramExpr = Expression.Parameter(selectors.First().InstanceType, "t");
      // cannot create a NewExpression on a dynamic type becasue of EF restrictions
      // so we always create a MemberInitExpression with bindings ( i.e. a new Foo() { a=1, b=2 } instead of new Foo(1,2);
      var newExpr = Expression.New(dti.DynamicEmptyConstructor);
      var propertyExprs = selectors.Select(s => s.BuildMemberExpression(paramExpr));
      var dynamicProperties = dti.DynamicType.GetTypeInfo().GetProperties();
      var bindings = dynamicProperties.Zip(propertyExprs, (prop, expr) => Expression.Bind(prop, expr));
      var memberInitExpr = Expression.MemberInit(newExpr, bindings.Cast<MemberBinding>());
      var newLambda = Expression.Lambda(memberInitExpr, paramExpr);
      return newLambda;
    }

    /// <summary> Compile a delegate that calls a one-argument Queryable method on an untyped IQueryable. </summary>
    /// <remarks>
    /// The Queryable methods are generic, but the element type is only known at runtime, so the
    /// call is built as an expression - casting in to IQueryable&lt;T&gt; and back out again - and compiled.
    /// </remarks>
    /// <param name="instanceType">The element type to close the method over.</param>
    /// <param name="method">The Queryable method to call.</param>
    /// <returns>A delegate applying that method.</returns>
    public static Func<IQueryable, IQueryable> BuildIQueryableFunc(Type instanceType, MethodInfo method) {
      
      var queryableBaseType = typeof(IQueryable<>);
      
      var paramExpr = Expression.Parameter(typeof(IQueryable));
      var queryableType = queryableBaseType.MakeGenericType(instanceType);
      var castParamExpr = Expression.Convert(paramExpr, queryableType);

      var callExpr = Expression.Call(method, castParamExpr );
      var castResultExpr = Expression.Convert(callExpr, typeof(IQueryable));
      var lambda = Expression.Lambda(castResultExpr, paramExpr);
      var func = (Func<IQueryable, IQueryable>)lambda.Compile();
      return func;
    }

    /// <summary> Compile a delegate that calls a two-argument Queryable method on an untyped IQueryable, with the second argument fixed. </summary>
    /// <typeparam name="TArg">The type of the fixed argument - a lambda for Where and OrderBy, an int for Skip and Take.</typeparam>
    /// <param name="instanceType">The element type to close the method over.</param>
    /// <param name="method">The Queryable method to call.</param>
    /// <param name="parameter">The value passed as the method's second argument.</param>
    /// <param name="queryableBaseType">The interface to cast the source to; defaults to IQueryable&lt;T&gt;, and is IOrderedQueryable&lt;T&gt; for ThenBy.</param>
    /// <returns>A delegate applying that method.</returns>
    public static Func<IQueryable, IQueryable> BuildIQueryableFunc<TArg>(Type instanceType, MethodInfo method, TArg parameter, Type? queryableBaseType = null) {
      if (queryableBaseType == null) {
        queryableBaseType = typeof(IQueryable<>);
      }
      var paramExpr = Expression.Parameter(typeof(IQueryable));
      var queryableType = queryableBaseType.MakeGenericType(instanceType);
      var castParamExpr = Expression.Convert(paramExpr, queryableType);


      var callExpr = Expression.Call(method, castParamExpr, Expression.Constant(parameter));
      var castResultExpr = Expression.Convert(callExpr, typeof(IQueryable));
      var lambda = Expression.Lambda(castResultExpr, paramExpr);
      var func = (Func<IQueryable, IQueryable>)lambda.Compile();
      return func;
    }
  }
}
