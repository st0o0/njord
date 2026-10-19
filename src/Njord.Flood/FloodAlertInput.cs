using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Flood;

public sealed record FloodRiskInput(
    Location Location,
    DomainForecast<FloodPoint> Flood) : IEnrichmentInput;

public sealed record FloodRainInput(
    Location Location,
    DomainForecast<FloodPoint> Flood,
    DomainForecast<WeatherPoint> Weather) : IEnrichmentInput;
