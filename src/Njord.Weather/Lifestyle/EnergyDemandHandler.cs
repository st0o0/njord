using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Weather.Lifestyle;

public sealed class EnergyDemandHandler(IOptions<EnergyDemandOptions> options) : IDomainHandler
{
    public string ConfigKey => "EnergyDemand";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 48);
        if (hours.Count == 0)
        {
            return [];
        }

        var settings = options.Value;
        var heating = hours.Sum(p => Math.Max(0.0, settings.HeatingSetpoint - p.ApparentTemperature));
        var cooling = hours.Sum(p => Math.Max(0.0, p.ApparentTemperature - settings.CoolingSetpoint));
        var peak = hours.MaxBy(p => Math.Max(settings.HeatingSetpoint - p.ApparentTemperature, p.ApparentTemperature - settings.CoolingSetpoint))!;
        return [new PipelineEvent.EnergyDemandComputed(current.Location, Math.Round(heating, 1), Math.Round(cooling, 1), peak.ValidFrom.Hour)];
    }
}
