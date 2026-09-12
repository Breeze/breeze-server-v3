using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Breeze.Core {
  /// <summary> A literal value in a query, coerced on construction to the data type it is being compared against. </summary>
  public class LitBlock : BaseBlock {

    private Object? _initialValue;
    private Object? _coercedValue;
    private DataType? _dataType;

    // TODO: doesn't yet handle case where value is an array - i.e. rhs of in clause.
    /// <summary> Create a literal and coerce it to the given data type. </summary>
    /// <param name="value">The value as it arrived from the client, usually a string or a boxed number.</param>
    /// <param name="dataType">The type to coerce to, normally taken from the other side of the comparison; null leaves the value as it is.</param>
    public LitBlock(Object? value, DataType? dataType) {
      _initialValue = value;
      _dataType = dataType;
      _coercedValue = DataType.CoerceData(value, dataType);
    }

    /// <summary> The value after coercion - what the generated expression will compare against. </summary>
    public Object? GetValue() {
      return _coercedValue;
    }

    /// <summary> The data type the literal was coerced to, or null if none was known. </summary>
    public override DataType? DataType {
      get { return _dataType; }
    }

    /// <summary> Build the constant expression for this literal. </summary>
    /// <param name="inExpr">The expression the block is applied to.  Unused here - a literal does not depend on the row.</param>
    /// <returns>A <see cref="ConstantExpression"/> holding the coerced value.</returns>
    public override Expression ToExpression(Expression inExpr) {
      return Expression.Constant(_coercedValue);
    }

  }
}