using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Breeze.Core {


  /// <summary> Turns JSON into plain CLR values, which is the form the query parser reads. </summary>
  public static class JsonHelper {
    /// <summary> Parse JSON into nested dictionaries, lists and primitives. </summary>
    /// <param name="json">The JSON text, typically the query a client sent.</param>
    /// <returns>An IDictionary&lt;string, object&gt; for an object, a List&lt;object&gt; for an array, or the primitive value itself.</returns>
    public static object? Deserialize(string json) {
      return ToObject(JToken.Parse(json));
    }

    private static object? ToObject(JToken token) {
      switch (token.Type) {
        case JTokenType.Object:
          return token.Children<JProperty>()
                      .ToDictionary(prop => prop.Name,
                                    prop => ToObject(prop.Value));

        case JTokenType.Array:
          return token.Select(ToObject).ToList();

        default:
          return ((JValue)token).Value;
      }
    }
  }
}
