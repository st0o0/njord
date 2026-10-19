using System.Text;
using Google.Protobuf;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class ProtoMappingGoldenMasterSpec
{
    private static readonly AlertScenario Fixture = AlertScenario.Create();

    private static readonly JsonFormatter Formatter = new(
        JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithIndentation());

    [Fact]
    public Task Alert_events_match_golden_master()
    {
        var text = new StringBuilder();
        foreach (var analysis in Fixture.Analyses)
        {
            foreach (var alert in analysis.Alerts.Alerts)
            {
                var mapped = EnrichmentEventMappings.ToEnrichmentEvent(
                    new PipelineEvent.AlertRaised(analysis.Alerts.Location, alert, AlertScenario.Now));
                Assert.NotNull(mapped);
                text.AppendLine(Formatter.Format(mapped));
            }
        }

        return Verify(text.ToString());
    }

    [Fact]
    public Task Domain_data_updates_match_golden_master()
    {
        var start = AlertScenario.Now;
        var location = DomainData.Lucerne;
        PipelineEvent[] events =
        [
            new PipelineEvent.WeatherDataReceived(location, DomainData.Weather(start, 2)),
            new PipelineEvent.AirQualityDataReceived(location, new DomainForecast<AirQualityPoint>(
                DomainKind.AirQuality, start, TimeResolution.Hourly,
                [new AirQualityPoint(start, 8.5, 12.0, 60.0, BirchPollen: 3.0)])),
            new PipelineEvent.FloodDataReceived(location, DomainData.Flood(start)),
            new PipelineEvent.SolarDataReceived(location, new DomainForecast<SolarPoint>(
                DomainKind.Solar, start, TimeResolution.Hourly, [new SolarPoint(start, 410.0, 90.0, 500.0, 3300.0)])),
            new PipelineEvent.MarineDataReceived(location, new DomainForecast<MarinePoint>(
                DomainKind.Marine, start, TimeResolution.Hourly, [new MarinePoint(start, 0.8, 5.5, 220.0, 17.5)])),
        ];

        var text = new StringBuilder();
        foreach (var evt in events)
        {
            var mapped = evt.ToDomainDataUpdate();
            Assert.NotNull(mapped);
            text.AppendLine(Formatter.Format(mapped));
        }

        return Verify(text.ToString());
    }

    [Fact]
    public void Fixture_maps_every_alert_type_into_the_alert_events()
    {
        var lucerne = Fixture.For(AlertScenario.Lucerne);
        var mapped = AlertProtoMappings.ToProto(lucerne.Alerts);

        var types = mapped.Alerts.Select(a => a.Type).ToHashSet();
        var expected = Enum.GetValues<AlertType>()
            .Where(t => t is not (AlertType.Unspecified or AlertType.Pollen or AlertType.Pm25 or AlertType.FloodRisk or AlertType.FloodRain));
        Assert.All(expected, t => Assert.Contains(t, types));
        Assert.All(mapped.Alerts, a => Assert.NotEqual(AlertSeverity.None, a.Severity));
    }
}
