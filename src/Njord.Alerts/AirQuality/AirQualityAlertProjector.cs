using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Alerts.AirQuality;

public sealed class AirQualityAlertProjector : IProjector<AirQualityAlertInput>
{
    public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind> { DomainKind.AirQuality };

    public AirQualityAlertInput? Project(LocationForecast current, LocationForecast? previous) =>
        current.AirQuality is { } airQuality
            ? new AirQualityAlertInput(current.Location, airQuality)
            : null;
}
