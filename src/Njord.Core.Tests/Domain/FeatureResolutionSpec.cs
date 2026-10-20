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
    private sealed class StubFeature(string name, FeatureDescriptor feature) : IFeature
    {
        public string Name { get; } = name;

        public FeatureDescriptor Feature { get; } = feature;
    }

    private sealed class StubHandler(string name, FeatureCategory category, bool defaultEnabled, DomainRequirements requirements) : IDomainHandler
    {
        public string Name { get; } = name;

        public FeatureDescriptor Feature { get; } = new(OutputKind.Condition, category, defaultEnabled, requirements);

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private sealed class StubMultiHandler(params IFeature[] features) : IMultiFeatureHandler
    {
        public IReadOnlyList<IFeature> Features { get; } = features;

        public string Name => Features[0].Name;

        public FeatureDescriptor Feature => Features[0].Feature;

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static FeatureOptions Options(params (string Category, string Name, bool Enabled)[] entries)
    {
        var options = new FeatureOptions();
        foreach (var (category, name, enabled) in entries)
        {
            options.Set(category, name, enabled);
        }

        return options;
    }

    private static readonly IDomainHandler[] All =
    [
        new StubHandler("Frost", FeatureCategory.Weather, true, new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts)),
        new StubHandler("Commute", FeatureCategory.Lifestyle, true, new(Weather: WeatherParams.Temperature | WeatherParams.Precipitation)),
        new StubHandler("Pollen", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.BirchPollen)),
        new StubHandler("Pm25", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.Pm25)),
        new StubHandler("FloodRisk", FeatureCategory.Flood, false, new(Flood: FloodParams.Discharge)),
        new StubHandler("Swimming", FeatureCategory.Marine, false, new(Marine: MarineParams.WaveHeight)),
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
        var domains = FeatureResolution.RequiredDomains(Options(("Flood", "FloodRisk", true)), All);

        Assert.Equal(
            new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood },
            domains.ToHashSet());
    }

    [Fact]
    public void Should_drop_air_quality_when_no_air_quality_feature_is_enabled()
    {
        var options = Options(("AirQuality", "Pollen", false), ("AirQuality", "Pm25", false));

        var domains = FeatureResolution.RequiredDomains(options, All);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather }, domains.ToHashSet());
    }

    [Fact]
    public void Should_keep_only_enabled_handlers()
    {
        var options = Options(("Lifestyle", "Commute", false), ("Marine", "Swimming", true));

        var names = FeatureResolution.EnabledHandlers(options, All).Select(h => h.Name);

        Assert.Equal(["Frost", "Pollen", "Pm25", "Swimming"], names);
    }

    [Fact]
    public void Should_list_the_enabled_features()
    {
        var names = FeatureResolution.EnabledFeatures(Options(("Flood", "FloodRisk", true)), All).Select(f => f.Name);

        Assert.Equal(
            ["Frost", "Commute", "Pollen", "Pm25", "FloodRisk"],
            names);
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
        var requirements = FeatureResolution.RequiredParams(Options(("Marine", "Swimming", true)), All);

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
        var frost = new StubFeature("Frost", new(OutputKind.Alert, FeatureCategory.Weather, true, new(Weather: WeatherParams.Temperature)));
        var storm = new StubFeature("Storm", new(OutputKind.Alert, FeatureCategory.Weather, true, new(Weather: WeatherParams.WindGusts)));
        IDomainHandler[] handlers = [new StubMultiHandler(frost, storm)];
        var frostOff = Options(("Weather", "Frost", false));
        var bothOff = Options(("Weather", "Frost", false), ("Weather", "Storm", false));

        Assert.Multiple(
            () => Assert.Equal(WeatherParams.WindGusts, FeatureResolution.RequiredParams(frostOff, handlers).Weather),
            () => Assert.Single(FeatureResolution.EnabledHandlers(frostOff, handlers)),
            () => Assert.Empty(FeatureResolution.EnabledHandlers(bothOff, handlers)));
    }
}
