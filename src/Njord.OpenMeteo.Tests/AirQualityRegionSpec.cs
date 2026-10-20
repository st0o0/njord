using Njord.Forecast.AirQuality;
using Njord.OpenMeteo.Configuration;

namespace Njord.OpenMeteo.Tests;

public sealed class AirQualityRegionSpec
{
    [Fact]
    public void Global_strips_all_region_bound_flags()
    {
        var flags = AirQualityParams.Pm25 | AirQualityParams.Ammonia | AirQualityParams.OlivePollen | AirQualityParams.UsAqi;

        var filtered = flags.ApplyRegionFilter(AirQualityRegion.Global);

        Assert.Equal(AirQualityParams.Pm25, filtered);
    }

    [Fact]
    public void Europe_keeps_ammonia_and_olive_pollen()
    {
        var flags = AirQualityParams.Pm25 | AirQualityParams.Ammonia | AirQualityParams.OlivePollen | AirQualityParams.UsAqi;

        var filtered = flags.ApplyRegionFilter(AirQualityRegion.Europe);

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Ammonia | AirQualityParams.OlivePollen, filtered);
    }

    [Fact]
    public void UnitedStates_keeps_us_aqi()
    {
        var flags = AirQualityParams.Pm25 | AirQualityParams.Ammonia | AirQualityParams.OlivePollen | AirQualityParams.UsAqi;

        var filtered = flags.ApplyRegionFilter(AirQualityRegion.UnitedStates);

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.UsAqi, filtered);
    }

    [Fact]
    public void Filter_preserves_non_region_bound_flags()
    {
        var flags = AirQualityParams.Dust | AirQualityParams.CarbonMonoxide | AirQualityParams.SulphurDioxide;

        var filtered = flags.ApplyRegionFilter(AirQualityRegion.Global);

        Assert.Equal(flags, filtered);
    }
}
