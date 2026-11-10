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
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, FeatureCategory.Forecast, name, requirements);
        public bool Enabled { get; } = enabled;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private static readonly StubRule _frost = new("Frost", true,
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.WindGusts, Ensemble: EnsembleParams.TemperatureSpread));

    private static readonly StubRule _pollen = new("Pollen", true,
        new DomainRequirements(AirQuality: AirQualityParams.BirchPollen));

    private static readonly StubRule _disabledPollen = new("Pollen", false,
        new DomainRequirements(AirQuality: AirQualityParams.BirchPollen));

    private static DomainRequirements Resolve(params IFeatureRule[] rules) =>
        rules.Where(r => r.Enabled)
            .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
            .WithBaseParams();

    [Fact]
    public void Enabled_features_produce_correct_requirements()
    {
        var requirements = Resolve(_frost, _pollen);

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
            Resolve(_frost, _pollen).RequiredDomains());
    }

    [Fact]
    public void Disabled_features_are_ignored() => Assert.Equal([DomainKind.Forecast, DomainKind.Ensemble], Resolve(_frost, _disabledPollen).RequiredDomains());

    [Fact]
    public void No_features_produce_defaults()
    {
        var requirements = Resolve();

        Assert.Equal(new DomainRequirements(), requirements);
        Assert.Empty(requirements.RequiredDomains());
    }

    [Fact]
    public void Base_params_are_applied() => Assert.True(Resolve(_frost).AllForecast.HasFlag(ForecastParams.WeatherCode));

    [Fact]
    public void Base_params_do_not_add_unrequired_domains() => Assert.False(Resolve(_frost).Requires(DomainKind.AirQuality));

    [Fact]
    public void Ensemble_domain_is_required_when_features_need_spread() => Assert.True(Resolve(_frost).Requires(DomainKind.Ensemble));
}
