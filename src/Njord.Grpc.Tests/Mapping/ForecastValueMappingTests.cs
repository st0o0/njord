using System.Collections.Immutable;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Forecasts;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ForecastValueMappingTests
{
    private static readonly Location _vienna = new("_vienna", 48.2, 16.37);

    [Fact]
    public void Growing_degree_maps_cumulative_gdd_and_days()
    {
        var result = new GrowingDegreeResult(_vienna,
            [new GrowingDegreeDay(new DateOnly(2026, 11, 10), 5.2), new GrowingDegreeDay(new DateOnly(2026, 11, 11), 6.1)],
            CumulativeGdd: 11.3);

        var proto = result.ToForecastValue();

        Assert.Equal(11.3, proto.Extra["cumulative_gdd"]);
        Assert.Equal(5.2, proto.Extra["gdd_2026_11_10"]);
        Assert.Equal(6.1, proto.Extra["gdd_2026_11_11"]);
    }

    [Fact]
    public void Seasonal_outlook_maps_months()
    {
        var result = new SeasonalOutlookResult(_vienna,
            [new SeasonalMonth(new DateOnly(2026, 12, 1), 1.5, 15.0, ConfidenceLevel.High)]);

        var proto = result.ToForecastValue();

        Assert.Equal(1.5, proto.Extra["temp_anomaly_2026_12"]);
        Assert.Equal(15.0, proto.Extra["precip_anomaly_2026_12"]);
        Assert.Equal((double)ConfidenceLevel.High, proto.Extra["confidence_2026_12"]);
    }

    [Fact]
    public void Model_comparison_maps_per_model_per_day()
    {
        var day = new ModelComparisonDay(new DateOnly(2026, 11, 10),
            [new ModelDayValues("ecmwf", 18.0, 10.0, 2.5)]);
        var result = new ForecastModelComparisonResult(_vienna, [day]);

        var proto = result.ToForecastValue();

        Assert.Equal(18.0, proto.Extra["ecmwf_2026_11_10_max"]);
        Assert.Equal(10.0, proto.Extra["ecmwf_2026_11_10_min"]);
        Assert.Equal(2.5, proto.Extra["ecmwf_2026_11_10_precip"]);
    }

    [Fact]
    public void Ensemble_model_comparison_maps_per_model_per_day()
    {
        var day = new ModelComparisonDay(new DateOnly(2026, 11, 10),
            [new ModelDayValues("ensemble_a", 20.0, 12.0, null)]);
        var result = new EnsembleModelComparisonResult(_vienna, [day]);

        var proto = result.ToForecastValue();

        Assert.Equal(20.0, proto.Extra["ensemble_a_2026_11_10_max"]);
        Assert.Equal(12.0, proto.Extra["ensemble_a_2026_11_10_min"]);
        Assert.False(proto.Extra.ContainsKey("ensemble_a_2026_11_10_precip"));
    }

    [Fact]
    public void Empty_model_comparison_maps_to_empty_extra()
    {
        var result = new ForecastModelComparisonResult(_vienna, []);

        var proto = result.ToForecastValue();

        Assert.Empty(proto.Extra);
    }
}
