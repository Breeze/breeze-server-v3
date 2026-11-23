namespace Breeze.AspNetCore {

  /// <summary>
  /// The limits a Breeze query filter was given explicitly, null where it was left unset - so that
  /// an unset limit can come from a filter at a wider scope (<see cref="BreezeQueryFilterAttribute.EffectiveLimits"/>).
  /// </summary>
  internal interface IBreezeQueryLimits {
    /// <summary> MaxTake as set on the filter, or null if it was not set. </summary>
    int? MaxTakeSetting { get; }

    /// <summary> MaxDepth as set on the filter, or null if it was not set. </summary>
    int? MaxDepthSetting { get; }
  }
}
