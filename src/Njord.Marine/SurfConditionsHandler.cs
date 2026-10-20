using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Marine;

public sealed class SurfConditionsHandler : IDomainHandler
{
    public string Name => "Surf";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Marine,
        DefaultEnabled: false,
        new DomainRequirements(
        Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
        OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
        OptionalWeather: WeatherParams.WindDirection));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Marine is not { } marine)
        {
            return [];
        }

        return SurfConditionsRule.Evaluate(marine, current.Weather, time.GetUtcNow()) is { } surf
            ? [new ConditionResult(current.Location, this.WireKey(), surf.Rating, Fields(surf))]
            : [];
    }

    private static IReadOnlyDictionary<string, string> Fields(SurfAssessment surf)
    {
        var fields = new Dictionary<string, string>
        {
            ["score"] = FeatureData.Number(surf.Score),
            ["swell_height"] = FeatureData.Number(surf.SwellHeight),
            ["swell_period"] = FeatureData.Number(surf.SwellPeriod),
            ["wind_considered"] = FeatureData.Flag(surf.WindRelation is not null),
            ["best_start"] = FeatureData.Time(surf.ValidFrom),
        };

        if (surf.WindRelation is { } relation)
        {
            fields["wind"] = relation;
        }

        return fields;
    }
}
