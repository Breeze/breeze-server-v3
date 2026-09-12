using NHibernate.Engine;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Breeze.Persistence.NH {
  /// <summary>
  /// An NHibernate query provider that also carries the Include paths for a query, since they
  /// cannot be put in the expression tree.
  /// </summary>
  public class NHQueryProvider : DefaultQueryProvider {
    /// <summary> Wrap an existing provider, taking over its session, collection and options. </summary>
    /// <remarks>
    /// The options live in a private field of DefaultQueryProvider and are copied by reflection.
    /// If a future NHibernate renames that field, this throws rather than silently dropping them.
    /// </remarks>
    /// <param name="source">The provider to wrap.</param>
    public NHQueryProvider(DefaultQueryProvider source) : this(source.Session, source.Collection) {
      // copy the private _options from the source
      // DefaultQueryProvider has always kept its options in a private _options field. Were a
      // future NHibernate to rename it, this copy would be wrong anyway - so fail here, loudly.
      var prop = source.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic
          | System.Reflection.BindingFlags.Instance)!;
      var options = prop.GetValue(source);
      prop.SetValue(this, options);
    }
    /// <summary> Create a provider for a session. </summary>
    /// <param name="session">The session the query runs against.</param>
    public NHQueryProvider(ISessionImplementor session) : this(session, null) { }
    /// <summary> Create a provider for a session, optionally querying within a collection. </summary>
    /// <param name="session">The session the query runs against.</param>
    /// <param name="collection">The collection being queried, or null for a query over the whole type.</param>
    public NHQueryProvider(ISessionImplementor session, object? collection) : base(session, collection) {
      Includes = new List<string>();
    }
    /// <summary> The navigation paths to load after the query runs, added by <see cref="NHQueryHelper.Include"/>. </summary>
    public List<string> Includes { get; }

    /// <summary> Apply query options and return a provider of this type, so the Include paths survive. </summary>
    /// <param name="setOptions">Sets the options - caching and the like.</param>
    /// <returns>A new provider carrying the options.</returns>
    public new IQueryProvider WithOptions(Action<NhQueryableOptions> setOptions) {
      var qp = base.WithOptions(setOptions);
      return new NHQueryProvider((DefaultQueryProvider)qp);
    }
  }
}
