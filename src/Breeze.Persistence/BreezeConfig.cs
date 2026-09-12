using Breeze.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Breeze.Persistence {

  /// <summary> Server-wide Breeze settings: JSON serialization, transactions, and enum handling. </summary>
  /// <remarks>
  /// To change any of it, derive from this class and override the member concerned - the
  /// subclass is found by scanning the loaded assemblies, so nothing needs to register it.
  /// Exactly one subclass may be present.
  /// </remarks>
  public class BreezeConfig {

    
    /// <summary> The configuration in force: the one subclass of <see cref="BreezeConfig"/> found in the loaded assemblies, or a default instance if there is none. </summary>
    /// <exception cref="Exception">More than one subclass was found; only one may be defined.</exception>
    public static BreezeConfig Instance {
      get {
        lock (__lock) {
          AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
          if (__instance == null) {
            var typeCandidates = ProbeAssemblies.SelectMany(a => GetTypes(a));
            var types = typeCandidates.Where(t => t != typeof (BreezeConfig) && typeof (BreezeConfig).IsAssignableFrom(t) && !t.IsAbstract).ToList();

            if (types.Count == 0) {
              __instance = new BreezeConfig();
            } else if (types.Count == 1) {
              // Creating a (non-Nullable<T>) class instance never yields null.
              __instance = (BreezeConfig) Activator.CreateInstance(types[0])!;
            } else {
              throw new Exception(
                "More than one BreezeConfig implementation was found in the currently loaded assemblies - limit is one.");
            }
          }
          return __instance;
        }
      }
    }

    /// <summary> The serializer settings used for query results, created once and cached. </summary>
    /// <returns>The settings.  Override <c>CreateJsonSerializerSettings</c> to change them.</returns>
    public JsonSerializerSettings GetJsonSerializerSettings() {
      lock (__lock) {
        if (_jsonSerializerSettings == null) {
          _jsonSerializerSettings = CreateJsonSerializerSettings();
        }
        return _jsonSerializerSettings;
      }
    }

    /// <summary> The serializer settings used for reading a save bundle, created once and cached. </summary>
    /// <returns>The settings.  Override <c>CreateJsonSerializerSettingsForSave</c> to change them.</returns>
    public JsonSerializerSettings GetJsonSerializerSettingsForSave() {
      lock (__lock) {
        if (_jsonSerializerSettingsForSave == null) {
          _jsonSerializerSettingsForSave = CreateJsonSerializerSettingsForSave();
        }
        return _jsonSerializerSettingsForSave;
      }
    }

    /// <summary> The loaded assemblies that Breeze searches for entity types, key generators and a <see cref="BreezeConfig"/> subclass. </summary>
    /// <remarks> Framework assemblies are excluded, and the list is rebuilt when new assemblies load. </remarks>
    public static ReadOnlyCollection<Assembly> ProbeAssemblies {
      get {
        lock (__lock) {
          if (__assemblyCount == 0 || __assemblyCount != __assemblyLoadedCount) {
            // Cache the ProbeAssemblies.
            __probeAssemblies = new ReadOnlyCollection<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsFrameworkAssembly(a)).ToList());
            __assemblyCount = __assemblyLoadedCount;
          }
          // __assemblyCount is only non-zero after the block above has assigned __probeAssemblies.
          return __probeAssemblies!;
        }
      }
    }

    private bool _useIntEnums;
    /// <summary> Whether to serialize enum values as string (false) or int (true).
    /// Default is false for backward compatibility.  Switch to true for more C#-like behavior and
    /// see https://github.com/Breeze/breeze.server.net/issues/196 for guidance.
    /// Set this value early in startup, because it affects JSON serialization settings and metadata generation.
    /// </summary>
    public virtual bool UseIntEnums { get => _useIntEnums; set => _useIntEnums = value; }

    private string? _queryParamName;
    /// <summary>
    /// Name of query parameter for JSON query string.  Note that this must agree with what the client sends.<br/>
    /// Default is null, which means no parameter -- the JSON starts at the question mark: <code>?{"take":5}</code><br/>
    /// If non-null, then the JSON is the value of a named parameter.  E.g. if QueryParamName = "bq", then <code>?bq={"take":5}</code>
    /// </summary>
    public virtual string? QueryParamName { get => _queryParamName; set => _queryParamName = value; }

    static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args) {
      Interlocked.Increment(ref __assemblyLoadedCount);
    }
    private static ReadOnlyCollection<Assembly>? __probeAssemblies;
    private static int __assemblyCount = 0;
    private static int __assemblyLoadedCount = 0;

    /// <summary>
    /// Override to use a specialized JsonSerializer implementation.
    /// </summary>
    protected virtual JsonSerializerSettings CreateJsonSerializerSettings() {

      var jsonSerializerSettings = new JsonSerializerSettings();
      return JsonSerializationFns.UpdateWithDefaults(jsonSerializerSettings, false, BreezeConfig.Instance.UseIntEnums);

    }

    /// <summary>
    /// Override to use a specialized JsonSerializer implementation for saving.
    /// Base implementation uses CreateJsonSerializerSettings() then sets TypeNameHandling to None
    /// </summary>
    protected virtual JsonSerializerSettings CreateJsonSerializerSettingsForSave() {
      var settings = CreateJsonSerializerSettings();
      settings.TypeNameHandling = TypeNameHandling.None;
      return settings;
    }

    /// <summary> Whether an assembly is a framework assembly, and so not worth searching for application types. </summary>
    /// <param name="assembly">The assembly to test.</param>
    /// <returns>True for the Microsoft, Entity Framework and NHibernate assemblies, and for anything whose product name is in <see cref="FrameworkProductNames"/>.</returns>
    public static bool IsFrameworkAssembly(Assembly assembly) {
      // Loaded (runtime) assemblies always have a FullName.
      var fullName = assembly.FullName!;
      if (fullName.StartsWith("Microsoft.")) return true;
      if (fullName.StartsWith("EntityFramework")) return true;
      if (fullName.StartsWith("NHibernate")) return true;
      var attrs = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false).OfType<AssemblyProductAttribute>();
      var attr = attrs.FirstOrDefault();
      if (attr == null) {
        return false;
      }
      var productName = attr.Product;
      return FrameworkProductNames.Any(nm => productName.StartsWith(nm));
    }

    /// <summary> The types in an assembly, or an empty array if they cannot all be loaded. </summary>
    /// <remarks> A failure is traced rather than thrown: one unloadable assembly should not stop the probe. </remarks>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>Its types, or an empty array.</returns>
    protected static IEnumerable<Type> GetTypes(Assembly assembly) {

      try {
        return assembly.GetTypes();
      } catch (Exception ex) {
        string msg = string.Empty;
        if (ex is System.Reflection.ReflectionTypeLoadException) {
          msg = ((ReflectionTypeLoadException)ex).LoaderExceptions.ToAggregateString(". ");
        }
        Trace.WriteLine("Breeze probing: Unable to execute Assembly.GetTypes() for "
          + assembly.ToString() + "." + msg);

        return new Type[] { };
      }
    }

    /// <summary> Assembly product names treated as framework code by <see cref="IsFrameworkAssembly"/>.  Matched as prefixes. </summary>
    protected static readonly List<String> FrameworkProductNames = new List<String> {
      "Microsoft®",
      "Microsoft (R)",
      "Microsoft ASP.",
      "System.Net.Http",
      "Json.NET",
      "Antlr3.Runtime",
      "Iesi.Collections",
      "WebGrease",
      "Breeze.ContextProvider",
      "Breeze.Persistence",
      "Breeze.Core",
      "Breeze.AspNetCore"
    };

    /// <summary>
    /// Returns TransactionSettings.Default.  Override to return different settings.
    /// </summary>
    /// <returns></returns>
    public virtual TransactionSettings GetTransactionSettings()
    {
        return TransactionSettings.Default;
    }

    private static Object __lock = new Object();
    private static BreezeConfig? __instance;

    private JsonSerializerSettings? _jsonSerializerSettings = null;
    private JsonSerializerSettings? _jsonSerializerSettingsForSave = null;

  }


}
