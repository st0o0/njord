using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureResolutionSpec
{
    private sealed class StubHandler(FeatureKind kind, bool defaultEnabled, DomainRequirements requirements) : IDomainHandler
    {
        public FeatureDescriptor Feature { get; } = new(kind, FeatureCategory.Lifestyle, "stub", defaultEnabled, requirements);

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private sealed class StubMultiHandler(params FeatureDescriptor[] features) : IMultiFeatureHandler
    {
        public IReadOnlyList<FeatureDescriptor> Features { get; } = features;

        public FeatureDescriptor Feature => Features[0];

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static readonly IDomainHandler[] All =
    [
        new StubHandler(FeatureKind.FrostAlert, true, new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts)),
        new StubHandler(FeatureKind.CommuteSafety, true, new(Weather: WeatherParams.Temperature | WeatherParams.Precipitation)),
        new StubHandler(FeatureKind.PollenAlert, true, new(AirQuality: AirQualityParams.BirchPollen)),
        new StubHandler(FeatureKind.Pm25Alert, true, new(AirQuality: AirQualityParams.Pm25)),
        new StubHandler(FeatureKind.FloodRisk, false, new(Flood: FloodParams.Discharge)),
        new StubHandler(FeatureKind.SwimmingConditions, false, new(Marine: MarineParams.WaveHeight)),
    ];

    [Fact]
    public void Should_require_weather_and_air_quality_for_the_default_features()
    {
        var domains = FeatureResolution.RequiredDomains(new FeatureOptions(), All);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality }, domains.ToHashSet());
    }

    [Fact]
    public void Should_add_the_flood_domain_when_flood_risk_is_enabled()
    {
        var domains = FeatureResolution.RequiredDomains(new FeatureOptions { [FeatureKind.FloodRisk] = true }, All);

        Assert.Equal(
            new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood },
            domains.ToHashSet());
    }

    [Fact]
    public void Should_drop_air_quality_when_no_air_quality_feature_is_enabled()
    {
        var options = new FeatureOptions { [FeatureKind.PollenAlert] = false, [FeatureKind.Pm25Alert] = false };

        var domains = FeatureResolution.RequiredDomains(options, All);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather }, domains.ToHashSet());
    }

    [Fact]
    public void Should_keep_only_enabled_handlers()
    {
        var options = new FeatureOptions { [FeatureKind.CommuteSafety] = false, [FeatureKind.SwimmingConditions] = true };

        var kinds = FeatureResolution.EnabledHandlers(options, All).Select(h => h.Feature.Kind);

        Assert.Equal([FeatureKind.FrostAlert, FeatureKind.PollenAlert, FeatureKind.Pm25Alert, FeatureKind.SwimmingConditions], kinds);
    }

    [Fact]
    public void Should_list_the_enabled_features()
    {
        var kinds = FeatureResolution.EnabledFeatures(new FeatureOptions { [FeatureKind.FloodRisk] = true }, All).Select(f => f.Kind);

        Assert.Equal(
            [FeatureKind.FrostAlert, FeatureKind.CommuteSafety, FeatureKind.PollenAlert, FeatureKind.Pm25Alert, FeatureKind.FloodRisk],
            kinds);
    }

    [Fact]
    public void Should_union_the_params_of_all_enabled_features()
    {
        var requirements = FeatureResolution.RequiredParams(new FeatureOptions(), All);

        Assert.Multiple(
            () => Assert.Equal(
                WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.Precipitation,
                requirements.Weather),
            () => Assert.Equal(AirQualityParams.BirchPollen | AirQualityParams.Pm25, requirements.AirQuality),
            () => Assert.Equal(FloodParams.None, requirements.Flood),
            () => Assert.Equal(MarineParams.None, requirements.Marine));
    }

    [Fact]
    public void Should_include_the_params_of_a_newly_enabled_feature()
    {
        var requirements = FeatureResolution.RequiredParams(new FeatureOptions { [FeatureKind.SwimmingConditions] = true }, All);

        Assert.Equal(MarineParams.WaveHeight, requirements.Marine);
    }

    [Fact]
    public void Should_require_nothing_when_no_handler_is_registered()
    {
        Assert.Equal(default, FeatureResolution.RequiredParams(new FeatureOptions(), []));
    }

    [Fact]
    public void Should_resolve_each_feature_of_a_multi_feature_handler_on_its_own()
    {
        var frost = new FeatureDescriptor(FeatureKind.FrostAlert, FeatureCategory.WeatherAlerts, "frost", true, new(Weather: WeatherParams.Temperature));
        var storm = new FeatureDescriptor(FeatureKind.StormAlert, FeatureCategory.WeatherAlerts, "storm", true, new(Weather: WeatherParams.WindGusts));
        IDomainHandler[] handlers = [new StubMultiHandler(frost, storm)];
        var frostOff = new FeatureOptions { [FeatureKind.FrostAlert] = false };
        var bothOff = new FeatureOptions { [FeatureKind.FrostAlert] = false, [FeatureKind.StormAlert] = false };

        Assert.Multiple(
            () => Assert.Equal(WeatherParams.WindGusts, FeatureResolution.RequiredParams(frostOff, handlers).Weather),
            () => Assert.Single(FeatureResolution.EnabledHandlers(frostOff, handlers)),
            () => Assert.Empty(FeatureResolution.EnabledHandlers(bothOff, handlers)));
    }
}
