using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Weather;

public sealed class WeatherRule(IOptions<WeatherConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Weather,
        FeatureCategory.Forecast,
        Name: "Weather",
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.WeatherCode | ForecastParams.Humidity
                | ForecastParams.WindSpeed | ForecastParams.WindDirection | ForecastParams.Pressure
                | ForecastParams.ApparentTemperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.Weather));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var forecast = ctx.Current.Forecast;
        if (ForecastQueries.ValueAt(forecast, ctx.Now) is not { Temperature: { } temperature } current)
        {
            return null;
        }

        var hourly = forecast.Points
            .Where(p => p.ValidFrom > ctx.Now && p.Temperature is not null)
            .Select(ToForecastPoint)
            .ToList();

        return new WeatherResult(
            ctx.Location,
            temperature,
            current.ApparentTemperature ?? temperature,
            current.Humidity ?? 0,
            current.WindSpeed ?? 0,
            current.WindDirection ?? 0,
            current.Pressure ?? 0,
            MapCondition(current.WeatherCode),
            hourly);
    }

    private static WeatherForecastPoint ToForecastPoint(ForecastPoint p)
    {
        var temperature = p.Temperature ?? 0;
        return new(
            p.ValidFrom,
            temperature,
            p.ApparentTemperature ?? temperature,
            p.Humidity ?? 0,
            p.WindSpeed ?? 0,
            p.WindDirection ?? 0,
            p.Pressure ?? 0,
            MapCondition(p.WeatherCode),
            p.PrecipitationProbability,
            p.Precipitation,
            p.CloudCover);
    }

    internal static string MapCondition(double? weatherCode) => weatherCode is { } value ? (int)value switch
    {
        0 => "clear",
        1 => "mostly-clear",
        2 => "partly-cloudy",
        3 => "overcast",
        45 or 48 => "fog",
        >= 51 and <= 55 => "drizzle",
        56 or 57 => "freezing-drizzle",
        >= 61 and <= 65 => "rain",
        66 or 67 => "freezing-rain",
        >= 71 and <= 77 => "snow",
        >= 80 and <= 82 => "showers",
        85 or 86 => "snow-showers",
        >= 95 and <= 99 => "thunderstorm",
        _ => "unknown",
    } : "unknown";
}
