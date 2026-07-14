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

    private static AlertEnrichment CreateFeature(bool enabled = true)
    {
        var enrichment = new EnrichmentOptions
        {
            Alerts = new AlertThresholdOptions { Enabled = enabled },
        };
        return new AlertEnrichment(Options.Create(enrichment), TimeProvider.System);
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

        var events = feature.Compute(snapshot, ["lucerne"]).ToList();

        Assert.Single(events);
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        Assert.Equal("lucerne", update.Location);
        Assert.Equal("alerts", update.TypeName);
        Assert.IsType<AlertResult>(update.Result);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }
}
