using Njord.Core.AirQuality;

namespace Njord.OpenMeteo.Configuration;

public static class AirQualityRegionExtensions
{
    private static readonly AirQualityParams AllRegionBound =
        AirQualityParams.Ammonia | AirQualityParams.OlivePollen | AirQualityParams.UsAqi;

    public static AirQualityParams RegionBoundFlags(this AirQualityRegion region) => region switch
    {
        AirQualityRegion.Global => AirQualityParams.None,
        AirQualityRegion.Europe => AirQualityParams.Ammonia | AirQualityParams.OlivePollen,
        AirQualityRegion.UnitedStates => AirQualityParams.UsAqi,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };

    public static AirQualityParams ApplyRegionFilter(this AirQualityParams flags, AirQualityRegion region) =>
        (flags & ~AllRegionBound) | (flags & region.RegionBoundFlags());
}
