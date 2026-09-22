using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Breeze.Core {

  /// <summary>
  /// Which navigation properties a client may expand from each entity type.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Rules come from two places: <see cref="AllowExpandAttribute"/> and
  /// <see cref="DenyExpandAttribute"/> on the entity type, and the <see cref="Allow{T}"/> and
  /// <see cref="Deny{T}"/> methods here. The methods exist because a generated or scaffolded model
  /// cannot carry attributes - and because a registration overrides the attributes, so a
  /// deployment can tighten or relax what the model declares without editing it.
  /// </para>
  /// <para>Each navigation is judged by this ladder, highest first:</para>
  /// <list type="number">
  /// <item><description>a registered <see cref="Deny{T}"/> refuses it;</description></item>
  /// <item><description>a registered <see cref="Allow{T}"/> list permits exactly its members and
  /// refuses everything else, shadowing the type's attributes entirely;</description></item>
  /// <item><description>a <see cref="DenyExpandAttribute"/> refuses it;</description></item>
  /// <item><description>an <see cref="AllowExpandAttribute"/> list permits exactly its
  /// members;</description></item>
  /// <item><description>otherwise <see cref="DenyByDefault"/> decides.</description></item>
  /// </list>
  /// <para>
  /// An allow-list always means "these and no others", whichever source it came from. So
  /// <c>Allow&lt;Order&gt;(o =&gt; o.Employee)</c> does not merely lift a <c>DenyExpand</c> on
  /// <c>Employee</c> - it makes <c>Employee</c> the only navigation expandable from <c>Order</c>.
  /// </para>
  /// <para>
  /// With no rules at all nothing is refused, so adding this to an existing application changes
  /// nothing until you declare something or set <see cref="DenyByDefault"/>.
  /// </para>
  /// <para>
  /// This governs <c>expand</c> only. A client can still reach a related entity through
  /// <c>select</c>, which is checked by <c>MaxDepth</c> but not by this policy.
  /// </para>
  /// </remarks>
  public static class ExpandPolicy {

    private sealed class TypeRule {
      internal HashSet<string>? AttributeAllow;
      internal HashSet<string> AttributeDeny = NewSet();
      internal HashSet<string>? RegistrationAllow;
      internal HashSet<string> RegistrationDeny = NewSet();
    }

    private static HashSet<string> NewSet() {
      return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly ConcurrentDictionary<Type, TypeRule> _registered
      = new ConcurrentDictionary<Type, TypeRule>();
    private static readonly ConcurrentDictionary<Type, TypeRule> _effective
      = new ConcurrentDictionary<Type, TypeRule>();

    /// <summary>
    /// Whether a navigation with no rule about it is refused. False by default, so an existing
    /// application is unaffected until it opts in.
    /// </summary>
    /// <remarks>
    /// Setting this true makes every expandable navigation something you declared on purpose,
    /// which is the safer arrangement but needs the whole model reviewed first.
    /// </remarks>
    public static bool DenyByDefault { get; set; }

    /// <summary>
    /// Permit exactly these navigations on <typeparamref name="T"/>, refusing every other one.
    /// </summary>
    /// <typeparam name="T">The entity type the navigations are declared on.</typeparam>
    /// <param name="navigations">Property expressions, as in <c>c =&gt; c.Orders</c>.</param>
    /// <exception cref="ArgumentException">An expression is not a property access.</exception>
    public static void Allow<T>(params Expression<Func<T, object?>>[] navigations) {
      var rule = _registered.GetOrAdd(typeof(T), _ => new TypeRule());
      if (rule.RegistrationAllow == null) { rule.RegistrationAllow = NewSet(); }
      foreach (var nav in navigations) { rule.RegistrationAllow.Add(NameOf(nav)); }
      _effective.Clear();
    }

    /// <summary> Refuse these navigations on <typeparamref name="T"/>. </summary>
    /// <typeparam name="T">The entity type the navigations are declared on.</typeparam>
    /// <param name="navigations">Property expressions, as in <c>o =&gt; o.Employee</c>.</param>
    /// <exception cref="ArgumentException">An expression is not a property access.</exception>
    public static void Deny<T>(params Expression<Func<T, object?>>[] navigations) {
      var rule = _registered.GetOrAdd(typeof(T), _ => new TypeRule());
      foreach (var nav in navigations) { rule.RegistrationDeny.Add(NameOf(nav)); }
      _effective.Clear();
    }

    /// <summary>
    /// Resolve these types' rules now, so a contradiction is reported at startup rather than on
    /// whichever request first touches it.
    /// </summary>
    /// <param name="entityTypes">The entity types to resolve.</param>
    /// <exception cref="InvalidOperationException">A type names the same navigation in both an
    /// allow and a deny from the same source.</exception>
    public static void Validate(params Type[] entityTypes) {
      foreach (var type in entityTypes) { _effective.GetOrAdd(type, Build); }
    }

    /// <summary> Forget every registration, and the resolved rules. Intended for tests. </summary>
    public static void Reset() {
      _registered.Clear();
      _effective.Clear();
      DenyByDefault = false;
    }

    /// <summary>
    /// The leading part of <paramref name="path"/> that policy refuses, or null if the whole path
    /// is permitted.
    /// </summary>
    /// <remarks>
    /// The return value names the hop that was refused rather than the path that was asked for,
    /// so a refusal at the first hop does not disclose whether the rest of the path exists.
    /// A path that does not resolve against the model returns null: that is
    /// <see cref="EntityQuery.Validate"/>'s to report, not this.
    /// </remarks>
    /// <param name="rootType">The entity type the path starts from.</param>
    /// <param name="path">A navigation path, dot- or slash-separated.</param>
    /// <returns>The refused leading path, or null.</returns>
    public static string? FirstForbiddenHop(Type rootType, string path) {
      // Walked here rather than with PropertySignature.GetProperties, which sets the next type to
      // the property's own type - ICollection<Order> for a collection navigation - so the segment
      // after it never resolves. That helper is for select paths through complex types.
      var current = rootType;
      var walked = new List<string>();

      foreach (var segment in path.Replace('/', '.').Split('.')) {
        var member = TypeFns.FindPropertyOrField(
          current, segment, BindingFlags.Instance | BindingFlags.Public);
        if (member == null) { return null; }

        walked.Add(member.Name);
        if (!IsAllowed(current, member.Name)) {
          return string.Join(".", walked);
        }

        var memberType = member is PropertyInfo property
          ? property.PropertyType
          : ((FieldInfo)member).FieldType;
        // string is IEnumerable<char>; unwrapping it would walk into nonsense.
        current = memberType == typeof(string)
          ? memberType
          : TypeFns.GetElementType(memberType) ?? memberType;
      }
      return null;
    }

    private static bool IsAllowed(Type declaringType, string navigation) {
      var rule = _effective.GetOrAdd(declaringType, Build);

      if (rule.RegistrationDeny.Contains(navigation)) { return false; }
      if (rule.RegistrationAllow != null) { return rule.RegistrationAllow.Contains(navigation); }
      if (rule.AttributeDeny.Contains(navigation)) { return false; }
      if (rule.AttributeAllow != null) { return rule.AttributeAllow.Contains(navigation); }
      return !DenyByDefault;
    }

    /// <summary> One type's attributes and registrations, merged once and memoized. </summary>
    private static TypeRule Build(Type type) {
      var rule = new TypeRule();

      foreach (var attr in type.GetCustomAttributes<AllowExpandAttribute>(inherit: true)) {
        if (rule.AttributeAllow == null) { rule.AttributeAllow = NewSet(); }
        foreach (var nav in attr.Navigations) { rule.AttributeAllow.Add(nav); }
      }
      foreach (var attr in type.GetCustomAttributes<DenyExpandAttribute>(inherit: true)) {
        foreach (var nav in attr.Navigations) { rule.AttributeDeny.Add(nav); }
      }

      TypeRule? registered;
      if (_registered.TryGetValue(type, out registered)) {
        rule.RegistrationAllow = registered.RegistrationAllow;
        rule.RegistrationDeny = registered.RegistrationDeny;
      }

      // Contradicting yourself within one source is a mistake. Across sources it is deliberate,
      // and the ladder in IsAllowed resolves it.
      Contradiction(type, "AllowExpand", "DenyExpand", rule.AttributeAllow, rule.AttributeDeny);
      Contradiction(type, "Allow", "Deny", rule.RegistrationAllow, rule.RegistrationDeny);
      return rule;
    }

    private static void Contradiction(
        Type type, string allowName, string denyName, HashSet<string>? allow, HashSet<string> deny) {
      if (allow == null) { return; }
      var overlap = allow.Intersect(deny).ToList();
      if (overlap.Any()) {
        throw new InvalidOperationException(
          $"{type.Name}: {string.Join(", ", overlap)} named in both {allowName} and {denyName}.");
      }
    }

    private static string NameOf<T>(Expression<Func<T, object?>> expression) {
      var body = expression.Body is UnaryExpression unary ? unary.Operand : expression.Body;
      if (body is MemberExpression member) { return member.Member.Name; }
      throw new ArgumentException("Expected a property expression, such as c => c.Orders.");
    }
  }
}
