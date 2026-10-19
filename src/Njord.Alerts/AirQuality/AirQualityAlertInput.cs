using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Alerts.AirQuality;

public sealed record AirQualityAlertInput(
    Location Location,
    DomainForecast<AirQualityPoint> AirQuality) : IEnrichmentInput;
