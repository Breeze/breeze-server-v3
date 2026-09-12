using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Breeze.Core {
  /// <summary>
  /// A query as it arrived from a Breeze client: a resource name, a where clause, and the
  /// orderBy, select, expand, skip and take that go with it.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A query is parsed from JSON by the <see cref="EntityQuery(string)"/> constructor, checked
  /// against the entity type by <see cref="Validate"/>, and then applied to an IQueryable by the
  /// extension methods in <see cref="EntityQueryExtensions"/>.
  /// </para>
  /// <para>
  /// Every clause method returns a new query rather than changing this one, so a query can be
  /// safely shared and built on.
  /// </para>
  /// </remarks>
  public class EntityQuery {

    private String? _resourceName;
    private BasePredicate? _wherePredicate;
    private OrderByClause? _orderByClause;
    private ExpandClause? _expandClause;
    private SelectClause? _selectClause;
    private int? _skipCount;
    private int? _takeCount;
    private bool? _inlineCountEnabled;
    private Dictionary<String, Object?>? _parameters;
    private Type? _entityType;

    static EntityQuery() {
      // default implementations of pluggable static extension methods
      EntityQuery.NeedsExecution = (qs, iq) => (qs != null && iq != null);
      EntityQuery.ApplyCustomLogic = (eq, iq, type) => iq;
      EntityQuery.ApplyExpand = (eq, iq, type) => iq;
      EntityQuery.AfterExecution = (eq, iq, list) => list;
    }

    /// <summary> Create an empty query, to be built up with the clause methods. </summary>
    public EntityQuery() {

    }

    /// <summary> Parse a query from the JSON a Breeze client sent. </summary>
    /// <param name="json">The serialized query.  Null or empty yields an empty query.</param>
    /// <exception cref="Exception">The string is not valid JSON, or is not a JSON object.</exception>
    public EntityQuery(String? json) {
      if (json == null || json.Length == 0) {
        return;
      }
      Dictionary<string, object?> qmap;
      try {
        var dmap = JsonHelper.Deserialize(json);
        // The JSON literal null is not a query object either.
        qmap = (Dictionary<string, object?>)(dmap ?? throw new InvalidCastException());
      } catch (Exception) {
        throw new Exception(
                "This EntityQuery ctor requires a valid json string. The following is not json: "
                        + json);
      }

      this._resourceName = GetMapValue<string>(qmap, "resourceName");
      this._skipCount = GetMapInt(qmap, "skip");
      this._takeCount = GetMapInt(qmap, "take");
      this._wherePredicate = BasePredicate.PredicateFromMap(GetMapValue<Dictionary<string, object?>>(qmap, "where"));
      this._orderByClause = OrderByClause.From(GetMapValue<List<Object>>(qmap, "orderBy"));
      this._selectClause = SelectClause.From(GetMapValue<List<Object>>(qmap, "select"));
      this._expandClause = ExpandClause.From(GetMapValue<List<Object>>(qmap, "expand"));
      this._parameters = GetMapValue<Dictionary<string, object?>>(qmap, "parameters");
      this._inlineCountEnabled = GetMapValue<bool?>(qmap, "inlineCount");

    }


    /// <summary> Copy a query.  Used by the clause methods, which each return a new query. </summary>
    /// <param name="query">The query to copy.</param>
    public EntityQuery(EntityQuery query) {
      this._resourceName = query._resourceName;
      this._skipCount = query._skipCount;
      this._takeCount = query._takeCount;
      this._wherePredicate = query._wherePredicate;
      this._orderByClause = query._orderByClause;
      this._selectClause = query._selectClause;
      this._expandClause = query._expandClause;
      this._inlineCountEnabled = query._inlineCountEnabled;
      this._parameters = query._parameters;

    }



    /// <summary> Return a new query with an additional where clause, parsed from JSON. </summary>
    /// <param name="json">The where clause as JSON.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Where(String json) {
      var qmap = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
      var pred = BasePredicate.PredicateFromMap(qmap);
      // pred is null for empty (or "null") JSON; it has always been passed on as is.
      return this.Where(pred!);
    }

    private T? GetMapValue<T>(IDictionary<string, object?> map, string key) {
      if (map.ContainsKey(key)) {
        return (T?)map[key];
      } else {
        return default(T);
      }
    }

    private int? GetMapInt(IDictionary<string, object?> map, string key) {
      if (map.ContainsKey(key)) {
        return Convert.ToInt32(map[key]);
      } else {
        return null;
      }
    }

    /// <summary> Return a new query with an additional where clause, combined with the existing one using <c>and</c>. </summary>
    /// <param name="predicate">The where clause to add.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Where(BasePredicate predicate) {
      EntityQuery eq = new EntityQuery(this);
      if (eq._wherePredicate == null) {
        eq._wherePredicate = predicate;
      } else if (eq._wherePredicate.Operator == Operator.And) {
        AndOrPredicate andOrPred = (AndOrPredicate)eq._wherePredicate;
        var preds = new List<BasePredicate>(andOrPred.Predicates);
        preds.Add(predicate);
        eq._wherePredicate = new AndOrPredicate(Operator.And, preds);
      } else {
        eq._wherePredicate = new AndOrPredicate(Operator.And,
                eq._wherePredicate, predicate);
      }
      return eq;
    }

    /// <summary> Return a new query with the given orderBy clauses appended. </summary>
    /// <param name="propertyPaths">Property paths, each optionally followed by "desc".</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery OrderBy(params String[] propertyPaths) {
      return OrderBy(propertyPaths.ToList());
    }

    /// <summary> Return a new query with the given orderBy clauses appended. </summary>
    /// <param name="propertyPaths">Property paths, each optionally followed by "desc".</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery OrderBy(List<String> propertyPaths) {
      EntityQuery eq = new EntityQuery(this);
      if (this._orderByClause == null) {
        eq._orderByClause = new OrderByClause(propertyPaths);
      } else {
        var propPaths = this._orderByClause.PropertyPaths.ToList();
        propPaths.AddRange(propertyPaths);
        eq._orderByClause = new OrderByClause(propPaths);
      }
      return eq;
    }

    /// <summary> Return a new query with the given expand clauses appended. </summary>
    /// <param name="propertyPaths">Navigation paths to load, each dot-delimited.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Expand(params String[] propertyPaths) {
      return Expand(propertyPaths.ToList());
    }

    /// <summary> Return a new query with the given expand clauses appended. </summary>
    /// <param name="propertyPaths">Navigation paths to load, each dot-delimited.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Expand(List<String> propertyPaths) {
      EntityQuery eq = new EntityQuery(this);
      if (this._expandClause == null) {
        eq._expandClause = new ExpandClause(propertyPaths);
      } else {
        // think about checking if any prop paths are duped.
        var propPaths = this._expandClause.PropertyPaths.ToList();
        propPaths.AddRange(propertyPaths);
        eq._expandClause = new ExpandClause(propPaths);
      }
      return eq;
    }

    // Impl of following functions is deferred to whatever Persistence framework is being used, i.e. EF vs NHibernate 

    /// <summary> Whether query string needs execution </summary>
    /// <remarks> Either argument may be null: the query string when the request has none,
    /// the IQueryable when the action result is not a queryable. </remarks>
    public static Func<string?, IQueryable?, bool> NeedsExecution {
      get;
      set;
    }
    /// <summary> Apply logic to the IQueryable after the Where clause is applied, but before Order/Skip/Take/Select </summary>
    public static Func<EntityQuery, IQueryable, Type, IQueryable> ApplyCustomLogic {
      get;
      set;
    }
    /// <summary> Apply expand clauses to the IQueryable after Order/Skip/Take/Select, but before execution </summary>
    public static Func<EntityQuery, IQueryable, Type, IQueryable> ApplyExpand {
      get;
      set;
    }
    /// <summary> After the query was executed, post-process the resulting list and return the list </summary>
    public static Func<EntityQuery, IQueryable, IList, IList> AfterExecution {
      get;
      set;
    }

    /// <summary> Return a new query with the given select (projection) clauses appended. </summary>
    /// <param name="propertyPaths">Property paths to project, each dot-delimited.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Select(params String[] propertyPaths) {
      return Select(propertyPaths.ToList());
    }

    /// <summary> Return a new query with the given select (projection) clauses appended. </summary>
    /// <param name="propertyPaths">Property paths to project, each dot-delimited.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Select(IEnumerable<String> propertyPaths) {
      EntityQuery eq = new EntityQuery(this);
      if (this._selectClause == null) {
        eq._selectClause = new SelectClause(propertyPaths);
      } else {
        // think about checking if any prop paths are duped.
        var propPaths = this._selectClause.PropertyPaths.ToList();
        propPaths.AddRange(propertyPaths);
        eq._selectClause = new SelectClause(propPaths);
      }
      return eq;
    }


    /// <summary> Return a new query limited to the first n rows. </summary>
    /// <param name="takeCount">The number of rows to take.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Take(int takeCount) {
      EntityQuery eq = new EntityQuery(this);
      eq._takeCount = takeCount;
      return eq;
    }

    /// <summary> Return a new query that skips the first n rows. </summary>
    /// <param name="skipCount">The number of rows to skip.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery Skip(int skipCount) {
      EntityQuery eq = new EntityQuery(this);
      eq._skipCount = skipCount;
      return eq;
    }

    /// <summary> Return a new query that does, or does not, ask for the total row count alongside the results. </summary>
    /// <param name="inlineCountEnabled">True to return the count as well as the rows.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery EnableInlineCount(bool inlineCountEnabled) {
      EntityQuery eq = new EntityQuery(this);
      eq._inlineCountEnabled = inlineCountEnabled;
      return eq;
    }

    /// <summary> Return a new query aimed at a different resource. </summary>
    /// <param name="resourceName">The name of the url resource to query.</param>
    /// <returns>A new query; this one is unchanged.</returns>
    public EntityQuery WithResourceName(String resourceName) {
      EntityQuery eq = new EntityQuery(this);
      eq._resourceName = resourceName;
      return eq;
    }

    private List<String>? ToStringList(Object? src) {
      if (src == null)
        return null;
      if (src is List<String>) {
        return (List<String>)src;
      } else if (src is String) {
        var list = new List<String>();
        list.Add((String)src);
        return list;

      }
      throw new Exception("Unable to convert to a List<String>");
    }

    /// <summary> Check every clause of this query against the type being queried, resolving their property paths. </summary>
    /// <remarks> Must run before the query is applied to an IQueryable.  It also records <see cref="EntityType"/>. </remarks>
    /// <param name="entityType">The type the query returns.</param>
    /// <exception cref="Exception">A clause names a property the type does not have, or compares values that cannot be compared.</exception>
    public void Validate(Type entityType) {
      _entityType = entityType;
      if (_wherePredicate != null) {
        _wherePredicate.Validate(entityType);
      }
      if (_orderByClause != null) {
        _orderByClause.Validate(entityType);
      }
      if (_selectClause != null) {
        _selectClause.Validate(entityType);
      }
    }


    /// <summary> The type this query was validated against.  Null until <see cref="Validate"/> has run. </summary>
    public Type? EntityType {
      get { return _entityType; }
    }

    /// <summary> The url resource being queried, or null if the query did not name one. </summary>
    public String? ResourceName {
      get { return _resourceName; }
    }

    /// <summary> The where clause, or null if the query has none. </summary>
    public BasePredicate? WherePredicate {
      get { return _wherePredicate; }
    }

    /// <summary> The orderBy clause, or null if the query does not sort. </summary>
    public OrderByClause? OrderByClause {
      get { return _orderByClause; }
    }

    /// <summary> The expand clause, or null if the query expands nothing. </summary>
    public ExpandClause? ExpandClause {
      get { return _expandClause; }
    }

    /// <summary> The select clause, or null if the query returns whole entities. </summary>
    public SelectClause? SelectClause {
      get { return _selectClause; }
    }

    /// <summary> The number of rows to skip, or null if the query does not skip. </summary>
    public int? SkipCount {
      get { return _skipCount; }
    }

    /// <summary> The number of rows to take, or null if the query is unlimited. </summary>
    public int? TakeCount {
      get { return _takeCount; }
    }

    /// <summary> Whether the query asked for the total row count alongside the results. </summary>
    public bool IsInlineCountEnabled {
      get { return _inlineCountEnabled.HasValue && _inlineCountEnabled.Value; }
    }

    /// <summary> The parameters the client sent, for a named query method that takes some. </summary>
    /// <returns>The parameters by name, or null if the query carried none.</returns>
    public IDictionary<string, object?>? GetParameters() {
      return _parameters;
    }

  }
}



