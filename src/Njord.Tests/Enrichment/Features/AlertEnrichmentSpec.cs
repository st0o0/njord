using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment.Features;

public sealed class AlertEnrichmentSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AlertEnrichment CreateFeature(bool enabled = true)
    {
        var enrichment = new EnrichmentOptions
        {
            Alerts = new AlertThresholdOptions { Enabled = enabled },
        };
        return new AlertEnrichment(Options.Create(enrichment), new FakeTimeProvider(T0));
    }

    private static ModelSnapshot MakeSnapshot()
    {
        var points = Enumerable.Range(0, 48).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?> { [Temperature] = 20.0 }))
            .ToList();
        var forecast = new ModelForecast(new("icon_d2"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        return ModelSnapshot.Empty.Update(forecast);
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_one_enrichment_update_per_location()
    {
        var feature = CreateFeature();
        var snapshot = MakeSnapshot();

        var consensus = ConsensusSnapshot.Compute(snapshot, Parameters, "lucerne", new FakeTimeProvider(T0));
        var events = feature.Compute(consensus).ToList();

        Assert.Single(events);
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        Assert.Equal("lucerne", update.Location);
        Assert.Equal("alerts", update.TypeName);
        Assert.IsType<AlertResult>(update.Result);
    }

    [Fact(Timeout = 5000)]
    public void Normal_conditions_produce_no_severe_alerts()
    {
        var feature = CreateFeature();
        var snapshot = MakeSnapshot();

        var consensus = ConsensusSnapshot.Compute(snapshot, Parameters, "lucerne", new FakeTimeProvider(T0));
        var events = feature.Compute(consensus).ToList();
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        var result = Assert.IsType<AlertResult>(update.Result);

        Assert.All(result.Alerts, a => Assert.Equal(AlertSeverity.None, a.Severity));
    }

    [Fact(Timeout = 5000)]
    public void Frost_condition_produces_frost_alert()
    {
        var feature = CreateFeature();

        ModelForecast MakeFrostForecast(string modelId) =>
            new(new(modelId), "lucerne", new CycleId(T0),
                new ForecastSeries(Enumerable.Range(0, 48).Select(h =>
                    new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?> { [Temperature] = -5.0 }))),
                DailyForecastSeries.Empty);

        var snapshot = ModelSnapshot.Empty
            .Update(MakeFrostForecast("icon_d2"))
            .Update(MakeFrostForecast("ecmwf_ifs025"));

        var consensus = ConsensusSnapshot.Compute(snapshot, Parameters, "lucerne", new FakeTimeProvider(T0));
        var events = feature.Compute(consensus).ToList();
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        var result = Assert.IsType<AlertResult>(update.Result);

        var frostAlert = result.Alerts.FirstOrDefault(a => a.Type == AlertType.Frost);
        Assert.NotNull(frostAlert);
        Assert.NotEqual(AlertSeverity.None, frostAlert.Severity);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }
}
