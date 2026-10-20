using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline.Tests;

internal sealed record Seen(LocationForecast Current, LocationForecast? Previous);

internal sealed class RecordingHandler(params DomainKind[] triggers) : IDomainHandler
{
    public string ConfigKey => "WeatherAlerts";

    public List<Seen> Calls { get; } = [];

    public DomainRequirements Requirements { get; } = HandlerRequirements.For(triggers);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        Calls.Add(new Seen(current, previous));
        return [];
    }
}

internal sealed class EmittingHandler : IDomainHandler
{
    public string ConfigKey => "WeatherAlerts";

    public DomainRequirements Requirements { get; } = HandlerRequirements.For(DomainKind.Weather);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) =>
    [
        new PipelineEvent.AlertRaised(current.Location, Alert.None(AlertType.Frost), time.GetUtcNow()),
    ];
}

internal static class HandlerRequirements
{
    public static DomainRequirements For(params DomainKind[] domains) =>
        domains.Aggregate(default(DomainRequirements), (all, domain) => all | domain switch
        {
            DomainKind.Weather => new DomainRequirements(Weather: WeatherParam.Temperature),
            DomainKind.AirQuality => new DomainRequirements(AirQuality: AirQualityParam.Pm25),
            DomainKind.Flood => new DomainRequirements(Flood: FloodParam.Discharge),
            DomainKind.Marine => new DomainRequirements(Marine: MarineParam.WaveHeight),
            _ => default,
        });
}
