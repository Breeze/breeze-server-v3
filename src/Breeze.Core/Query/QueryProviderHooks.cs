using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Breeze.Core {

  /// <summary>
  /// Installs a persistence package's query steps (<see cref="EntityQuery.ApplyExpand"/> and the
  /// other pluggable hooks) for the provider a queryable comes from, before the query filters use
  /// them.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <see cref="EntityQuery"/>'s hooks start out as no-ops: <see cref="EntityQuery.ApplyExpand"/>
  /// returns the queryable unchanged. Each persistence package plugs in its own - Entity Framework's
  /// <c>Include</c>, NHibernate's post-processing - and used to do it only in its persistence
  /// manager's static constructor, which runs when the first persistence manager is created: by a
  /// Metadata request or a save. Until then a query's <c>expand</c> was silently ignored: 200, with
  /// every expanded navigation empty. A Breeze client fetches metadata first and never noticed; any
  /// other first caller after a restart (a script, Swagger UI, a test host) did.
  /// </para>
  /// <para>
  /// The query filters call <see cref="EnsureInstalled"/> with the queryable an action returned.
  /// The queryable's provider says which package's hooks it needs; this loads that package and
  /// runs its installer once per provider. Breeze.Core references neither package, so they are
  /// found by name. A provider Breeze has no package for (LINQ to Objects, say) needs no hooks.
  /// </para>
  /// </remarks>
  public static class QueryProviderHooks {

    /// <summary> The provider's assembly, and the Breeze package and type whose static Install() plugs its hooks in. </summary>
    private static readonly (string ProviderAssembly, string HooksAssembly, string HooksType)[] Known = [
      ("Microsoft.EntityFrameworkCore", "Breeze.Persistence.EFCore", "Breeze.Persistence.EFCore.EFQueryHooks"),
      ("NHibernate", "Breeze.Persistence.NH", "Breeze.Persistence.NH.NHQueryHooks"),
    ];

    /// <summary> The provider types already handled, whether or not they had hooks to install. </summary>
    private static readonly ConcurrentDictionary<Type, bool> Handled = new ConcurrentDictionary<Type, bool>();

    /// <summary>
    /// Make sure the query hooks for <paramref name="queryable"/>'s provider are installed. Cheap
    /// after the first call for a provider; safe to call on every query.
    /// </summary>
    /// <param name="queryable">The queryable an action returned; null does nothing.</param>
    public static void EnsureInstalled(IQueryable? queryable) {
      if (queryable == null) { return; }
      Handled.GetOrAdd(queryable.Provider.GetType(), Install);
    }

    /// <summary> Run the installer for a provider, if Breeze has a package for it. </summary>
    /// <remarks>
    /// The provider's base types count too: an NHibernate query that has had <c>Include</c> applied
    /// has Breeze's own <c>NHQueryProvider</c>, which derives from NHibernate's.
    /// </remarks>
    /// <returns>Whether hooks were installed.</returns>
    private static bool Install(Type providerType) {
      for (var type = providerType; type != null; type = type.BaseType) {
        if (Install(type.Assembly.GetName().Name)) { return true; }
      }
      return false;
    }

    private static bool Install(string? providerAssemblyName) {
      foreach (var known in Known) {
        if (known.ProviderAssembly != providerAssemblyName) { continue; }

        Assembly hooksAssembly;
        try {
          hooksAssembly = Assembly.Load(new AssemblyName(known.HooksAssembly));
        } catch (Exception e) when (e is System.IO.FileNotFoundException || e is System.IO.FileLoadException) {
          // The provider is in use without its Breeze package: nothing to install.
          return false;
        }

        var install = hooksAssembly.GetType(known.HooksType, throwOnError: true)!
          .GetMethod("Install", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
          ?? throw new MissingMethodException(known.HooksType, "Install");
        install.Invoke(null, null);
        return true;
      }
      return false;
    }
  }
}
