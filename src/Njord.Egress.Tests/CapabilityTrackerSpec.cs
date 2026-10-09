using Njord.Core.Configuration;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Egress.Tests;

public sealed class CapabilityTrackerSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private static CapabilityTracker CreateTracker() => new(Parameters, [3, 24, 48, 72], 4);

    private static ModelForecast Forecast(string modelId, bool withWind, string location = "lucerne")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 60)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 20.0 + i,
                    [wind] = withWind ? 5.0 : null,
                }))
            .ToList();

        return new ModelForecast(new WeatherModel(modelId), location, new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    [Fact]
    public void First_observation_emits_capability_learned()
    {
        var tracker = CreateTracker();

        var cap = tracker.Track(Forecast("icon_d2", withWind: true));

        Assert.NotNull(cap);
        Assert.Equal("lucerne", cap.Location);
        Assert.Equal("icon_d2", cap.Model.Id);
        Assert.Contains(ParameterRegistry.GetByApiName("temperature_2m")!, cap.SupportedParameters);
        Assert.Contains(ParameterRegistry.GetByApiName("wind_speed_10m")!, cap.SupportedParameters);
    }

    [Fact]
    public void Subset_observation_emits_nothing()
    {
        var tracker = CreateTracker();
        Assert.NotNull(tracker.Track(Forecast("icon_d2", withWind: true)));

        Assert.Null(tracker.Track(Forecast("icon_d2", withWind: false)));
        Assert.Null(tracker.Track(Forecast("icon_d2", withWind: true)));
    }

    [Fact]
    public void Superset_observation_emits_expanded_capability()
    {
        var tracker = CreateTracker();
        var first = tracker.Track(Forecast("icon_d2", withWind: false));
        Assert.NotNull(first);
        Assert.DoesNotContain(ParameterRegistry.GetByApiName("wind_speed_10m")!, first.SupportedParameters);

        var second = tracker.Track(Forecast("icon_d2", withWind: true));

        Assert.NotNull(second);
        Assert.Contains(ParameterRegistry.GetByApiName("wind_speed_10m")!, second.SupportedParameters);
    }

    [Fact]
    public void Capabilities_are_tracked_per_location_and_model()
    {
        var tracker = CreateTracker();
        Assert.NotNull(tracker.Track(Forecast("icon_d2", withWind: true)));

        Assert.NotNull(tracker.Track(Forecast("icon_eu", withWind: true)));
        Assert.NotNull(tracker.Track(Forecast("icon_d2", withWind: true, location: "bern")));
    }

    [Fact]
    public void Applicable_horizons_are_limited_by_model_coverage()
    {
        var tracker = CreateTracker();

        var cap = tracker.Track(Forecast("icon_d2", withWind: true));

        Assert.NotNull(cap);
        Assert.Contains(48, cap.ApplicableHorizons);
        Assert.DoesNotContain(72, cap.ApplicableHorizons);
        Assert.Equal(3, cap.ApplicableDayOffsets.Count);
    }
}
