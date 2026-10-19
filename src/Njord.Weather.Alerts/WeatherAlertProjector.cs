using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Weather.Alerts;

public sealed class WeatherAlertProjector : IProjector<WeatherAlertInput>
{
    public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public WeatherAlertInput? Project(LocationForecast current, LocationForecast? previous) =>
        current.Weather is { } weather
            ? new WeatherAlertInput(current.Location, weather, previous?.Weather)
            : null;
}
