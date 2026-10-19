using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Alerts;

public sealed record WeatherAlertInput(
    Location Location,
    DomainForecast<WeatherPoint> Weather,
    DomainForecast<WeatherPoint>? Previous) : IEnrichmentInput;
