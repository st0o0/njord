using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.AirQuality;

public sealed record AirQualityAlertInput(
    Location Location,
    DomainForecast<AirQualityPoint> AirQuality) : IEnrichmentInput;
