using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Core.Tests.Domain;

public sealed class DomainRequirementsResolutionTests
{
    private sealed class StubRule(string name, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, FeatureCategory.Forecast, name, enabled, requirements);
        public bool Enabled { get; } = enabled;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private static readonly StubRule Frost = new("Frost", true,
        new(Forecast: ForecastParams.Temperature | ForecastParams.WindGusts, Ensemble: EnsembleParams.TemperatureSpread));

    private static readonly StubRule Pollen = new("Pollen", true,
        new(AirQuality: AirQualityParams.BirchPollen));

    private static readonly StubRule DisabledPollen = new("Pollen", false,
        new(AirQuality: AirQualityParams.BirchPollen));

    private static DomainRequirements Resolve(params IFeatureRule[] rules) =>
        rules.Where(r => r.Enabled)
            .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
            .WithBaseParams();

    [Fact]
    public void Enabled_features_produce_correct_requirements()
    {
        var requirements = Resolve(Frost, Pollen);

        Assert.Multiple(
            () => Assert.True(requirements.Requires(DomainKind.Forecast)),
            () => Assert.True(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)));
    }

    [Fact]
    public void Required_domains_match_enabled_features()
    {
        Assert.Equal(
            [DomainKind.Forecast, DomainKind.AirQuality, DomainKind.Ensemble],
            Resolve(Frost, Pollen).RequiredDomains());
    }

    [Fact]
    public void Disabled_features_are_ignored()
    {
        Assert.Equal([DomainKind.Forecast, DomainKind.Ensemble], Resolve(Frost, DisabledPollen).RequiredDomains());
    }

    [Fact]
    public void No_features_produce_defaults()
    {
        var requirements = Resolve();

        Assert.Equal(new DomainRequirements(), requirements);
        Assert.Empty(requirements.RequiredDomains());
    }

    [Fact]
    public void Base_params_are_applied()
    {
        Assert.True(Resolve(Frost).AllForecast.HasFlag(ForecastParams.WeatherCode));
    }

    [Fact]
    public void Base_params_do_not_add_unrequired_domains()
    {
        Assert.False(Resolve(Frost).Requires(DomainKind.AirQuality));
    }

    [Fact]
    public void Ensemble_domain_is_required_when_features_need_spread()
    {
        Assert.True(Resolve(Frost).Requires(DomainKind.Ensemble));
    }
}
