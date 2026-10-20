using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline.Tests;

internal sealed record Seen(LocationForecast Current, LocationForecast? Previous);

internal sealed class RecordingHandler(params DomainKind[] triggers) : IDomainHandler
{
    public string ConfigKey => "WeatherAlerts";

    public List<Seen> Calls { get; } = [];

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind>(triggers);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        Calls.Add(new Seen(current, previous));
        return [];
    }
}

internal sealed class EmittingHandler : IDomainHandler
{
    public string ConfigKey => "WeatherAlerts";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) =>
    [
        new PipelineEvent.AlertRaised(current.Location, Alert.None(AlertType.Frost), time.GetUtcNow()),
    ];
}
