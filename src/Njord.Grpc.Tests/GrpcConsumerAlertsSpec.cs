using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerAlertsSpec : GrpcConsumerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Raised_alerts_are_merged_per_type_and_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var frost = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.6, NoDetails.Instance);
        var heat = new Alert(AlertType.Heat, AlertSeverity.Orange, 0.9, NoDetails.Instance);
        var frostRed = frost with { Severity = AlertSeverity.Red };

        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, frost, Anchor), ct);
        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, heat, Anchor.AddMinutes(1)), ct);
        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, frostRed, Anchor.AddMinutes(2)), ct);

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts is { } a && a.Alerts.SequenceEqual([frostRed, heat]), ct);
        Assert.NotNull(result.Alerts);
        var alerts = result.Alerts.Alerts;
        Assert.Equal([frostRed, heat], alerts);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_latest_analysis_query_returns_the_last_event_of_a_kind()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var first = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var last = EnrichmentEvents.Alerts(Lucerne, Anchor.AddHours(1)) with { Alert = first.Alert with { Severity = AlertSeverity.Red } };

        await pipeline.OfferAsync(first, ct);
        await pipeline.OfferAsync(last, ct);

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts is { } a && a.Alerts.SequenceEqual([last.Alert]), ct);
        AssertAlerts(Lucerne, [last.Alert], result.Alerts);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Alerts_are_kept_per_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var lucerneAlerts = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var zurichAlerts = EnrichmentEvents.Alerts(Zurich, Anchor.AddHours(1));

        await pipeline.OfferAsync(lucerneAlerts, ct);
        await pipeline.OfferAsync(zurichAlerts, ct);

        var zurich = await AwaitAnalysisAsync(readModel, Zurich, r => r.Alerts is not null, ct);
        var lucerne = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts is not null, ct);
        Assert.Multiple(
            () => AssertAlerts(Lucerne, [lucerneAlerts.Alert], lucerne.Alerts),
            () => AssertAlerts(Zurich, [zurichAlerts.Alert], zurich.Alerts));
    }
}
