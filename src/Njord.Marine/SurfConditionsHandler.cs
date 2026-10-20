using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Marine;

public sealed class SurfConditionsHandler : IDomainHandler
{
    public string ConfigKey => "SurfConditions";

    public DomainRequirements Requirements { get; } = new(
        Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
        OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
        OptionalWeather: WeatherParams.WindDirection);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Marine is not { } marine)
        {
            return [];
        }

        return SurfConditionsRule.Evaluate(marine, current.Weather, time.GetUtcNow()) is { } surf
            ? [new PipelineEvent.SurfConditionsComputed(
                current.Location, surf.Score, surf.Rating, surf.SwellHeight, surf.SwellPeriod, surf.WindRelation, surf.ValidFrom)]
            : [];
    }
}
