using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Weather;

internal static class WeatherResponseMappingExtensions
{
    private const string TimeKey = "time";
    private const string MemberPrefix = "member";

    private static readonly FrozenSet<string> KnownNames =
        ApiVariableNames.For<WeatherApiVariable>().Values.ToFrozenSet();

    private static readonly string[] BaseNames = [.. KnownNames.OrderByDescending(n => n.Length)];

    public static ImmutableDictionary<string, DomainForecast<WeatherPoint>> ToModelForecasts(
        this WeatherResponse response, DateTimeOffset modelRun, string singleModelId)
    {
        if (response.Hourly is not { } hourly || hourly.Count == 0)
        {
            return ImmutableDictionary<string, DomainForecast<WeatherPoint>>.Empty;
        }

        var modelData = SplitByModel(hourly);

        if (modelData.IsEmpty)
        {
            return ImmutableDictionary<string, DomainForecast<WeatherPoint>>.Empty;
        }

        var time = hourly.TryGetValue(TimeKey, out var t) ? JsonNode.Parse(t.GetRawText()) : new JsonArray();

        var forecasts = ImmutableDictionary.CreateBuilder<string, DomainForecast<WeatherPoint>>();
        foreach (var (model, series) in modelData)
        {
            series[TimeKey] = time?.DeepClone();
            var data = series.Deserialize(OpenMeteoJsonContext.Default.WeatherHourlyData);
            if (data?.ToForecast(modelRun)?.TrimTrailing((WeatherPoint p, WeatherParams f) => p.HasFields(f)) is { } forecast)
            {
                var key = model.Length == 0 ? singleModelId : model;
                forecasts[key] = forecast;
            }
        }

        return forecasts.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonObject> SplitByModel(Dictionary<string, JsonElement> hourly)
    {
        var plain = new JsonObject();
        var perModel = new Dictionary<string, JsonObject>();
        var modelOrder = new List<string>();

        foreach (var (key, value) in hourly)
        {
            if (key == TimeKey)
            {
                continue;
            }

            if (!TrySplit(key, out var baseName, out var model))
            {
                continue;
            }

            if (model is null)
            {
                plain[baseName] = JsonNode.Parse(value.GetRawText());
                continue;
            }

            if (!perModel.TryGetValue(model, out var group))
            {
                perModel[model] = group = [];
                modelOrder.Add(model);
            }

            group[baseName] = JsonNode.Parse(value.GetRawText());
        }

        if (perModel.Count > 0)
        {
            return modelOrder.ToImmutableDictionary(m => m, m => perModel[m]);
        }

        return plain.Count > 0
            ? ImmutableDictionary<string, JsonObject>.Empty.Add(string.Empty, plain)
            : ImmutableDictionary<string, JsonObject>.Empty;
    }

    private static bool TrySplit(string key, out string baseName, out string? model)
    {
        foreach (var name in BaseNames)
        {
            if (key == name)
            {
                (baseName, model) = (name, null);
                return true;
            }

            if (key.Length > name.Length + 1 && key.StartsWith(name, StringComparison.Ordinal) && key[name.Length] == '_')
            {
                var suffix = key[(name.Length + 1)..];
                if (suffix.StartsWith(MemberPrefix, StringComparison.Ordinal))
                {
                    break;
                }

                (baseName, model) = (name, suffix);
                return true;
            }
        }

        (baseName, model) = (string.Empty, null);
        return false;
    }

    private static DomainForecast<WeatherPoint>? ToForecast(this WeatherHourlyData h, DateTimeOffset modelRun)
    {
        if (h.Time.Length == 0)
        {
            return null;
        }

        var points = new WeatherPoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new WeatherPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
                Temperature: At(h.Temperature, i),
                ApparentTemperature: At(h.ApparentTemperature, i),
                WindSpeed: At(h.WindSpeed, i),
                Precipitation: At(h.Precipitation, i),
                PrecipitationProbability: At(h.PrecipitationProbability, i),
                CloudCover: At(h.CloudCover, i),
                Pressure: At(h.Pressure, i),
                WeatherCode: At(h.WeatherCode, i),
                Humidity: At(h.Humidity, i),
                DewPoint: At(h.DewPoint, i),
                UvIndex: At(h.UvIndex, i),
                EnsembleStdDev: At(h.EnsembleStdDev, i),
                WindGusts: At(h.WindGusts, i),
                Visibility: At(h.Visibility, i),
                Cape: At(h.Cape, i),
                Rain: At(h.Rain, i),
                Snowfall: At(h.Snowfall, i),
                SoilTemperature: At(h.SoilTemperature, i),
                FreezingLevelHeight: At(h.FreezingLevelHeight, i),
                IsDay: At(h.IsDay, i),
                ShortwaveRadiation: At(h.ShortwaveRadiation, i),
                DirectRadiation: At(h.DirectRadiation, i),
                DiffuseRadiation: At(h.DiffuseRadiation, i),
                DirectNormalIrradiance: At(h.DirectNormalIrradiance, i),
                SunshineDuration: At(h.SunshineDuration, i),
                WindDirection: At(h.WindDirection, i),
                GlobalTiltedIrradiance: At(h.GlobalTiltedIrradiance, i),
                SnowDepth: At(h.SnowDepth, i),
                WetBulbTemperature: At(h.WetBulbTemperature, i),
                SnowfallHeight: At(h.SnowfallHeight, i),
                PrecipitationSpread: At(h.PrecipitationSpread, i),
                WindSpeedSpread: At(h.WindSpeedSpread, i));
        }

        return new DomainForecast<WeatherPoint>(
            DomainKind.Weather, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
