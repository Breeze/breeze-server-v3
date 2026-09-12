
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace Breeze.Core {
  /// <summary>
  /// The set of data types a Breeze query can refer to, and the conversions between the loosely
  /// typed values that arrive in a query and the CLR types an expression needs.
  /// </summary>
  /// <remarks>
  /// Each data type is a named singleton, declared as one of the static fields below. Constructing
  /// one registers it, so <see cref="FromName"/> and <see cref="FromType"/> can find it afterwards.
  /// The names are part of the wire format - they are what the Breeze client writes into a query -
  /// so they cannot be changed without breaking clients.
  /// </remarks>
  public class DataType {
    private String _name;
    private Type? _type; // null for Binary, the only DataType without a CLR type
    private static Dictionary<String, DataType> _nameMap = new Dictionary<String, DataType>();
    private static Dictionary<Type, DataType> _typeMap = new Dictionary<Type, DataType>();

    /// <summary> A byte array, sent as base64.  The only data type with no CLR type behind it, and so the only one that cannot be coerced. </summary>
    public static DataType Binary = new DataType("Binary");
    /// <summary> A <see cref="System.Guid"/>, sent as its string form. </summary>
    public static DataType Guid = new DataType("Guid", typeof(System.Guid));
    /// <summary> A <see cref="System.String"/>. </summary>
    public static DataType String = new DataType("String", typeof(string));

    /// <summary> A <see cref="System.DateTime"/>. </summary>
    public static DataType DateTime = new DataType("DateTime", typeof(DateTime));
    /// <summary> A <see cref="System.DateTimeOffset"/>.  A plain DateTime coerces to one. </summary>
    public static DataType DateTimeOffset = new DataType("DateTimeOffset", typeof(DateTimeOffset));
    /// <summary> A <see cref="TimeSpan"/>, sent as an ISO 8601 duration such as "PT2H30M". </summary>
    public static DataType Time = new DataType("Time", typeof(TimeSpan));

    /// <summary> An 8-bit unsigned <see cref="byte"/>. </summary>
    public static DataType Byte = new DataType("Byte", typeof(byte));
    /// <summary> A 16-bit signed <see cref="short"/>. </summary>
    public static DataType Int16 = new DataType("Int16", typeof(short));
    /// <summary> A 32-bit signed <see cref="int"/>. </summary>
    public static DataType Int32 = new DataType("Int32", typeof(int));
    /// <summary> A 64-bit signed <see cref="long"/>. </summary>
    public static DataType Int64 = new DataType("Int64", typeof(long));
    /// <summary> A <see cref="bool"/>. </summary>
    public static DataType Boolean = new DataType("Boolean", typeof(bool));

    /// <summary> A <see cref="decimal"/>. </summary>
    public static DataType Decimal = new DataType("Decimal", typeof(Decimal));
    /// <summary> A double-precision <see cref="double"/>. </summary>
    public static DataType Double = new DataType("Double", typeof(Double));
    /// <summary> A single-precision <see cref="float"/>. </summary>
    public static DataType Single = new DataType("Single", typeof(Single));
    /// <summary> A <see cref="System.DateOnly"/> - a date with no time part. </summary>
    public static DataType DateOnly = new DataType("DateOnly", typeof(DateOnly));
    /// <summary> A <see cref="System.TimeOnly"/> - a time of day with no date. </summary>
    public static DataType TimeOnly = new DataType("TimeOnly", typeof(TimeOnly));

    /// <summary> Create a data type with no CLR type behind it, and register it under its name. </summary>
    /// <param name="name">The name used on the wire.  <see cref="FromName"/> looks it up by this.</param>
    public DataType(String name) {
      _name = name;
      _nameMap[name] = this;
    }

    /// <summary> Create a data type and register it under both its name and its CLR type. </summary>
    /// <param name="name">The name used on the wire.  <see cref="FromName"/> looks it up by this.</param>
    /// <param name="type">The CLR type it maps to.  <see cref="FromType"/> looks it up by this.</param>
    public DataType(String name, Type type) {
      _name = name;
      _type = type;
      _nameMap[name] = this;
      _typeMap[type] = this;
    }


    /// <summary> The name this data type is known by on the wire, e.g. "Int32". </summary>
    public String GetName() {
      return _name;
    }

    /// <summary> The CLR type this data type maps to, or null for <see cref="Binary"/>, which has none. </summary>
    public Type? GetUnderlyingType() {
      return _type;
    }

    /// <summary> Look up a data type by the name a client sent. </summary>
    /// <param name="name">One of the names of the static fields above, e.g. "DateTimeOffset".</param>
    /// <returns>The matching data type.</returns>
    /// <exception cref="KeyNotFoundException">No data type is registered under that name.</exception>
    public static DataType FromName(String name) {
      return _nameMap[name];
    }

    /// <summary> Look up the data type for a CLR type.  A nullable type resolves to the same data type as the type it wraps. </summary>
    /// <param name="type">The CLR type, which may be a Nullable&lt;T&gt;.</param>
    /// <returns>The matching data type.</returns>
    /// <exception cref="KeyNotFoundException">No data type maps to that CLR type - enums and entity types among them.</exception>
    public static DataType FromType(Type type) {
      var nnType = TypeFns.GetNonNullableType(type);
      return _typeMap[nnType];
    }

    /// <summary> Convert list to IList of itemType.  Handles case where itemType is enum and/or nullable. </summary>
    /// <param name="list">The values to convert, typically the right-hand side of an 'In' clause.</param>
    /// <param name="itemType">The element type of the returned list.</param>
    /// <returns>A new List&lt;itemType&gt; holding the coerced values.</returns>
    public static IList CoerceList(IList list, Type itemType) {
      var listType = typeof(List<>).MakeGenericType(new[] { itemType });
      // Creating a List<T> never yields null.
      var newList = (IList)Activator.CreateInstance(listType)!;
      var et = TypeFns.GetNonNullableType(itemType);
      if (et.IsEnum) {
        foreach (var item in list) {
          var enumVal = item == null ? null : item is string ? Enum.Parse(et, (String)item) : Enum.ToObject(et, item);
          newList.Add(enumVal);
        }
      } else {
        var dataType = DataType.FromType(et);
        foreach (var item in list) {
          var itemVal = item == null ? null : CoerceData(item, dataType);
          newList.Add(itemVal);
        }
      }
      return newList;
    }

    // Can't use this safely because of missing support for optional parts.
    // private static DateFormat ISO8601_Format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSSZ");

    /// <summary> Convert value to an object of the dataType </summary>
    /// <param name="value">The value as it arrived, usually a string or a boxed number.  Null passes through unchanged.</param>
    /// <param name="dataType">The data type to convert to.  Null passes the value through unchanged.</param>
    /// <returns>The value as the data type's underlying CLR type.</returns>
    public static Object? CoerceData(Object? value, DataType? dataType) {

      // The '!'s on GetUnderlyingType() below: it is null only for Binary, which has never
      // been coercible; those calls throw ArgumentNullException for it, as they always have.
      if (value == null || dataType == null || value.GetType() == dataType.GetUnderlyingType()) {
        return value;
      } else if (value is IList ilist) {
        // this occurs with an 'In' clause
        return CoerceList(ilist, dataType.GetUnderlyingType()!);
      } else if (dataType == DataType.Guid) {
        // object.ToString() is annotated nullable only for unusual overrides.
        return System.Guid.Parse(value.ToString()!);
      } else if (dataType == DataType.DateTimeOffset && value is DateTime) {
        DateTimeOffset result = (DateTime)value;
        return result;
      } else if (dataType == DataType.Time && value is String) {
        return XmlConvert.ToTimeSpan((string)value);
      } else if (dataType == DataType.DateOnly && value is String) {
        return System.DateOnly.Parse((string)value);
      } else if (dataType == DataType.TimeOnly && value is String) {
        return System.TimeOnly.Parse((string)value);
      } else {
        return Convert.ChangeType(value, dataType.GetUnderlyingType()!);
      }


    }
  }
}
