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
    public List<Seen> Calls { get; } = [];

    public string Name => "Frost";

    public FeatureDescriptor Feature { get; } = HandlerRequirements.Describe(HandlerRequirements.For(triggers));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        Calls.Add(new Seen(current, previous));
        return [];
    }
}

internal sealed class RequiringHandler(DomainRequirements requirements) : IDomainHandler
{
    public List<Seen> Calls { get; } = [];

    public string Name => "Frost";

    public FeatureDescriptor Feature { get; } = HandlerRequirements.Describe(requirements);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        Calls.Add(new Seen(current, previous));
        return [new AlertResult(current.Location, "frost", Alert.None(), time.GetUtcNow())];
    }
}

internal sealed class EmittingHandler : IDomainHandler
{
    public string Name => "Frost";

    public FeatureDescriptor Feature { get; } = HandlerRequirements.Describe(HandlerRequirements.For(DomainKind.Weather));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) =>
    [
        new AlertResult(current.Location, "frost", Alert.None(), time.GetUtcNow()),
    ];
}

internal static class HandlerRequirements
{
    public static FeatureDescriptor Describe(DomainRequirements requirements) =>
        new(OutputKind.Alert, FeatureCategory.Weather, DefaultEnabled: true, requirements);

    public static DomainRequirements For(params DomainKind[] domains) =>
        domains.Aggregate(default(DomainRequirements), (all, domain) => all | domain switch
        {
            DomainKind.Weather => new DomainRequirements(Weather: WeatherParams.Temperature),
            DomainKind.AirQuality => new DomainRequirements(AirQuality: AirQualityParams.Pm25),
            DomainKind.Flood => new DomainRequirements(Flood: FloodParams.Discharge),
            DomainKind.Marine => new DomainRequirements(Marine: MarineParams.WaveHeight),
            _ => default,
        });
}
