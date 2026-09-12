using Newtonsoft.Json;
using System;
using System.Xml;

namespace Breeze.Core {
  // http://www.w3.org/TR/xmlschema-2/#duration
  /// <summary>
  /// Serializes <see cref="TimeSpan"/> as an XML Schema duration - "PT2H30M" - which is the form
  /// the Breeze client reads and writes.
  /// </summary>
  /// <seealso href="http://www.w3.org/TR/xmlschema-2/#duration">XML Schema duration</seealso>
  public class TimeSpanConverter : JsonConverter {
    /// <summary> Write a TimeSpan as an XML Schema duration string. </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The TimeSpan.  Json.NET handles nulls itself and never passes one here.</param>
    /// <param name="serializer">The serializer in use.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
      // Json.NET writes null values itself and never hands them to a converter.
      var ts = (TimeSpan)value!;
      var tsString = XmlConvert.ToString(ts);
      serializer.Serialize(writer, tsString);
    }

    /// <summary> Read a TimeSpan from an XML Schema duration string. </summary>
    /// <param name="reader">The reader positioned at the value.</param>
    /// <param name="objectType">The target type.</param>
    /// <param name="existingValue">Any value already present.  Unused.</param>
    /// <param name="serializer">The serializer in use.</param>
    /// <returns>The parsed TimeSpan, or null for a JSON null.</returns>
    /// <exception cref="FormatException">The string is not a valid duration.</exception>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
      if (reader.TokenType == JsonToken.Null) {
        return null;
      }

      // Null tokens were handled above, so the string is non-null.
      var value = serializer.Deserialize<String>(reader)!;
      return XmlConvert.ToTimeSpan(value);
    }

    /// <summary> Handles TimeSpan and TimeSpan?. </summary>
    /// <param name="objectType">The type being considered.</param>
    /// <returns>True for TimeSpan and its nullable form.</returns>
    public override bool CanConvert(Type objectType) {
      return objectType == typeof(TimeSpan) || objectType == typeof(TimeSpan?);
    }
  }
}


