using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Breeze.Core {
  /// <summary> only needed because this functionality seems to be broken in JSON.NET 10.0.3 </summary>
  public class ByteArrayConverter : JsonConverter {
    /// <summary> Handles byte[] and nothing else. </summary>
    /// <param name="objectType">The type being considered.</param>
    /// <returns>True for byte[].</returns>
    public override bool CanConvert(Type objectType) {
      return objectType == typeof(byte[]);
    }

    /// <summary> Read a byte array from base64, accepting either a bare string or an object carrying "$value". </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <param name="objectType">The target type.</param>
    /// <param name="existingValue">Any value already present.  Unused.</param>
    /// <param name="serializer">The serializer in use.</param>
    /// <returns>The decoded bytes, or null for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">The value is neither a string nor an object with "$value".</exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
      if (reader.TokenType == JsonToken.Null)
        return null;
      var token = JToken.Load(reader);
      if (token == null)
        return null;
      switch (token.Type) {
        case JTokenType.Null:
          return null;
        case JTokenType.String:
          // A String token always carries a non-null string.
          return Convert.FromBase64String(((string?)token)!);
        case JTokenType.Object: {
            var value = (string?)token["$value"];
            return value == null ? null : Convert.FromBase64String(value);
          }
        default:
          throw new JsonSerializationException("Unknown byte array format");
      }
    }

    /// <summary> This converter writes as well as reads. </summary>
    public override bool CanWrite { get { return true; } }

    /// <summary> Write a byte array as a base64 string. </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The byte array.  Json.NET handles nulls itself and never passes one here.</param>
    /// <param name="serializer">The serializer in use.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
      // Json.NET writes null values itself and never hands them to a converter.
      string base64String = Convert.ToBase64String((byte[])value!);

      serializer.Serialize(writer, base64String);
    }
  }


}
