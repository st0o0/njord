using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisory(IOptions<ExerciseAdvisoryConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Exercise",
        new DomainRequirements(
        Forecast: ForecastParams.Temperature | ForecastParams.Humidity,
        AirQuality: AirQualityParams.Pm25 | AirQualityParams.Ozone,
        OptionalForecast: ForecastParams.UvIndex),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new ForecastAqInput(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 12)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(ctx.Current.AirQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .Select(pair => Components(pair.Point, pair.Air!, cfg))
            .OfType<Hour>()
            .ToList();
        if (hours.Count == 0)
        {
            return null;
        }

        var average = hours.Average(h => h.Total);
        var best = hours.MaxBy(h => h.Total)!;
        var level = average switch
        {
            _ when average >= cfg.SafeScore => ExerciseLevel.Safe,
            _ when average >= cfg.CautionScore => ExerciseLevel.Caution,
            _ => ExerciseLevel.Avoid,
        };

        var reasons = new List<ExerciseFactor>();
        AddReason(reasons, hours.Average(h => h.Pm25), ExerciseFactor.Pm25, cfg.ReasonThreshold);
        AddReason(reasons, hours.Average(h => h.Ozone), ExerciseFactor.Ozone, cfg.ReasonThreshold);
        AddReason(reasons, hours.Average(h => h.Temperature), ExerciseFactor.Temperature, cfg.ReasonThreshold);
        AddReason(reasons, hours.Average(h => h.Humidity), ExerciseFactor.Humidity, cfg.ReasonThreshold);
        AddReason(reasons, hours.Average(h => h.Uv), ExerciseFactor.Uv, cfg.ReasonThreshold);

        return new ExerciseAdvisoryResult(ctx.Location, level, [.. reasons], best.ValidFrom);
    }

    private static void AddReason(List<ExerciseFactor> reasons, double componentScore, ExerciseFactor factor, double threshold)
    {
        if (componentScore < threshold)
        {
            reasons.Add(factor);
        }
    }

    internal static Hour? Components(ForecastPoint weather, AirQualityPoint air, ExerciseAdvisoryConfig cfg)
    {
        if (air is not { Pm25: { } airPm25, Ozone: { } airOzone }
            || weather is not { Temperature: { } airTemperature, Humidity: { } relativeHumidity })
        {
            return null;
        }

        var pm25 = 1.0 - Scoring.Ramp(airPm25, cfg.Pm25Low, cfg.Pm25High);
        var ozone = 1.0 - Scoring.Ramp(airOzone, cfg.OzoneLow, cfg.OzoneHigh);
        var temperature = Scoring.Band(airTemperature, cfg.TemperatureCold, cfg.TemperatureCool, cfg.TemperatureWarm, cfg.TemperatureHot);
        var humidity = 1.0 - Scoring.Ramp(relativeHumidity, cfg.HumidityLow, cfg.HumidityHigh);
        var uv = weather.UvIndex is { } uvIndex ? 1.0 - Scoring.Ramp(uvIndex, cfg.UvModerate, cfg.UvHigh) : 1.0;
        var total = 100.0 * (cfg.Pm25Weight * pm25 + cfg.OzoneWeight * ozone + cfg.TemperatureWeight * temperature
            + cfg.HumidityWeight * humidity + cfg.UvWeight * uv);
        return new Hour(weather.ValidFrom, total, pm25, ozone, temperature, humidity, uv);
    }

    internal sealed record Hour(
        DateTimeOffset ValidFrom,
        double Total,
        double Pm25,
        double Ozone,
        double Temperature,
        double Humidity,
        double Uv);
}
