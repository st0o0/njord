using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Njord.Forecast;
using Njord.Forecast.Forecast;
using Njord.OpenMeteo.Ensemble;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Forecast;

internal static class ForecastResponseMappingExtensions
{
    private const string TimeKey = "time";
    private const string MemberPrefix = "member";

    private static readonly FrozenSet<string> KnownNames =
        ApiVariableNames.For<ForecastApiVariable>().Values
            .Concat(ApiVariableNames.For<EnsembleApiVariable>().Values)
            .ToFrozenSet();

    private static readonly string[] BaseNames = [.. KnownNames.OrderByDescending(n => n.Length)];

    public static ImmutableDictionary<string, DomainForecast<ForecastPoint>> ToModelForecasts(
        this ForecastResponse response, DateTimeOffset modelRun, string singleModelId)
    {
        if (response.Hourly is not { } hourly || hourly.Count == 0)
        {
            return ImmutableDictionary<string, DomainForecast<ForecastPoint>>.Empty;
        }

        var modelData = SplitByModel(hourly);

        if (modelData.IsEmpty)
        {
            return ImmutableDictionary<string, DomainForecast<ForecastPoint>>.Empty;
        }

        var time = hourly.TryGetValue(TimeKey, out var t) ? JsonNode.Parse(t.GetRawText()) : new JsonArray();

        var forecasts = ImmutableDictionary.CreateBuilder<string, DomainForecast<ForecastPoint>>();
        foreach (var (model, series) in modelData)
        {
            series[TimeKey] = time?.DeepClone();
            var data = series.Deserialize(OpenMeteoJsonContext.Default.ForecastHourlyData);
            if (data?.ToForecast(modelRun)?.TrimTrailing((ForecastPoint p, ForecastParams f) => p.HasFields(f)) is { } forecast)
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

    private static DomainForecast<ForecastPoint>? ToForecast(this ForecastHourlyData h, DateTimeOffset modelRun)
    {
        if (h.Time.Length == 0)
        {
            return null;
        }

        var points = new ForecastPoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new ForecastPoint(
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
                UvIndexClearSky: At(h.UvIndexClearSky, i),
                Showers: At(h.Showers, i),
                SoilMoisture0To1cm: At(h.SoilMoisture0To1cm, i),
                SoilMoisture1To3cm: At(h.SoilMoisture1To3cm, i),
                Evapotranspiration: At(h.Evapotranspiration, i),
                VapourPressureDeficit: At(h.VapourPressureDeficit, i),
                BoundaryLayerHeight: At(h.BoundaryLayerHeight, i),
                CloudCoverLow: At(h.CloudCoverLow, i),
                CloudCoverMid: At(h.CloudCoverMid, i),
                CloudCoverHigh: At(h.CloudCoverHigh, i),
                ConvectiveInhibition: At(h.ConvectiveInhibition, i),
                SurfacePressure: At(h.SurfacePressure, i),
                WindSpeed80m: At(h.WindSpeed80m, i),
                WindDirection80m: At(h.WindDirection80m, i),
                WindSpeed120m: At(h.WindSpeed120m, i),
                WindDirection120m: At(h.WindDirection120m, i),
                Temperature80m: At(h.Temperature80m, i),
                Temperature120m: At(h.Temperature120m, i),
                LiftedIndex: At(h.LiftedIndex, i));
        }

        return new DomainForecast<ForecastPoint>(
            DomainKind.Forecast, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
