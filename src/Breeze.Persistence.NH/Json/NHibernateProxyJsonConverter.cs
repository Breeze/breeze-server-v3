using Newtonsoft.Json;
using NHibernate;
using NHibernate.Proxy;
using System;

namespace Breeze.Persistence.NH {
  /// <summary>
  /// JsonConverter for handling NHibernate proxies.  
  /// Only serializes the object if it is initialized, i.e. the proxied object has been loaded.
  /// </summary>
  /// <seealso href="http://james.newtonking.com/projects/json/help/html/T_Newtonsoft_Json_JsonConverter.htm">Newtonsoft.Json JsonConverter</seealso>
  public class NHibernateProxyJsonConverter : JsonConverter {
    /// <summary> Write a proxied entity, unwrapping the proxy first, and write null for one that was never loaded. </summary>
    /// <remarks>
    /// An entity already written in this payload is written again as a "$ref" pointing at it.
    /// That has to be done by hand here, because a converter bypasses the reference handling
    /// Json.NET would otherwise apply.
    /// </remarks>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The proxy, or the entity behind it.</param>
    /// <param name="serializer">The serializer in use.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
      if (NHibernateUtil.IsInitialized(value)) {
        var proxy = value as INHibernateProxy;
        if (proxy != null) {
          value = proxy.HibernateLazyInitializer.GetImplementation();
        }

        // JsonSerializer creates a default resolver the first time this is read; it is never null.
        var resolver = serializer.ReferenceResolver!;
        // IsInitialized(null) is false, so value is non-null by here.
        if (resolver.IsReferenced(serializer, value!)) {
          // we've already written the object once; this time, just write the reference
          // We have to do this manually because we have our own JsonConverter.
          var valueRef = resolver.GetReference(serializer, value!);
          writer.WriteStartObject();
          writer.WritePropertyName("$ref");
          writer.WriteValue(valueRef);
          writer.WriteEndObject();
        } else {
          serializer.Serialize(writer, value);
        }
      } else {
        serializer.Serialize(writer, null);
      }
    }

    /// <summary> Not supported: proxies are written to the client but never read back from it. </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <param name="objectType">The target type.</param>
    /// <param name="existingValue">Any value already present.</param>
    /// <param name="serializer">The serializer in use.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotImplementedException">Always.</exception>
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
      throw new NotImplementedException();
    }

    /// <summary> Handles NHibernate proxy types. </summary>
    /// <param name="objectType">The type being considered.</param>
    /// <returns>True if the type is an INHibernateProxy.</returns>
    public override bool CanConvert(Type objectType) {
      return typeof(INHibernateProxy).IsAssignableFrom(objectType);
    }
  }
}
