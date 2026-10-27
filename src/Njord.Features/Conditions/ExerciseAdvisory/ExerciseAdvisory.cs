using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisory(IOptions<ExerciseAdvisoryConfig> config) : FeatureRule<ForecastAqInput>
{
    public const double SafeScore = 70.0;
    public const double CautionScore = 45.0;

    private const double Pm25Weight = 0.4;
    private const double OzoneWeight = 0.2;
    private const double TemperatureWeight = 0.2;
    private const double HumidityWeight = 0.1;
    private const double UvWeight = 0.1;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Exercise",
        DefaultEnabled: false,
        new DomainRequirements(
        Forecast: ForecastParams.Temperature | ForecastParams.Humidity,
        AirQuality: AirQualityParams.Pm25 | AirQualityParams.Ozone,
        OptionalForecast: ForecastParams.UvIndex));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 12)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(ctx.Current.AirQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .Select(pair => Components(pair.Point, pair.Air!))
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
            >= SafeScore => ExerciseLevel.Safe,
            >= CautionScore => ExerciseLevel.Caution,
            _ => ExerciseLevel.Avoid,
        };

        var reasons = new List<ExerciseFactor>();
        AddReason(reasons, hours.Average(h => h.Pm25), ExerciseFactor.Pm25);
        AddReason(reasons, hours.Average(h => h.Ozone), ExerciseFactor.Ozone);
        AddReason(reasons, hours.Average(h => h.Temperature), ExerciseFactor.Temperature);
        AddReason(reasons, hours.Average(h => h.Humidity), ExerciseFactor.Humidity);
        AddReason(reasons, hours.Average(h => h.Uv), ExerciseFactor.Uv);

        return new ExerciseAdvisoryResult(ctx.Location, level, [.. reasons], best.ValidFrom);
    }

    private static void AddReason(List<ExerciseFactor> reasons, double componentScore, ExerciseFactor factor)
    {
        if (componentScore < 0.5)
        {
            reasons.Add(factor);
        }
    }

    private static Hour? Components(ForecastPoint weather, AirQualityPoint air)
    {
        if (air is not { Pm25: { } airPm25, Ozone: { } airOzone }
            || weather is not { Temperature: { } airTemperature, Humidity: { } relativeHumidity })
        {
            return null;
        }

        var pm25 = 1.0 - Scoring.Ramp(airPm25, 15.0, 55.0);
        var ozone = 1.0 - Scoring.Ramp(airOzone, 100.0, 180.0);
        var temperature = Scoring.Band(airTemperature, -5.0, 8.0, 22.0, 35.0);
        var humidity = 1.0 - Scoring.Ramp(relativeHumidity, 70.0, 100.0);
        var uv = weather.UvIndex is { } uvIndex ? 1.0 - Scoring.Ramp(uvIndex, 5.0, 11.0) : 1.0;
        var total = 100.0 * (Pm25Weight * pm25 + OzoneWeight * ozone + TemperatureWeight * temperature
            + HumidityWeight * humidity + UvWeight * uv);
        return new Hour(weather.ValidFrom, total, pm25, ozone, temperature, humidity, uv);
    }

    private sealed record Hour(
        DateTimeOffset ValidFrom,
        double Total,
        double Pm25,
        double Ozone,
        double Temperature,
        double Humidity,
        double Uv);
}
