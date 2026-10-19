using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Njord.Model;
using Njord.Mqtt.Payloads;
using Njord.Mqtt.Serialization;
using Njord.Mqtt.Topics;

namespace Njord.Mqtt.Mapping;

internal static class DomainPayloadMappings
{
    public const string StateSegment = "state";

    public static WeatherPayload ToPayload(this WeatherPoint point, DateTimeOffset modelRun)
        => new(point.ValidFrom, modelRun, point.Temperature, point.ApparentTemperature, point.WindSpeed,
            point.Precipitation, point.PrecipitationProbability, point.CloudCover, point.Pressure, point.WeatherCode,
            point.Humidity, point.DewPoint, point.UvIndex, point.EnsembleStdDev, point.WindGusts, point.Visibility,
            point.Cape, point.Rain, point.Snowfall, point.SoilTemperature, point.FreezingLevelHeight, point.IsDay);

    public static AirQualityPayload ToPayload(this AirQualityPoint point, DateTimeOffset modelRun)
        => new(point.ValidFrom, modelRun, point.Pm25, point.Pm10, point.Ozone,
            point.BirchPollen, point.GrassPollen, point.Alder, point.Mugwort);

    public static FloodPayload ToPayload(this FloodPoint point, DateTimeOffset modelRun)
        => new(point.ValidFrom, modelRun, point.Discharge, point.DischargeP10, point.DischargeP90, point.MedianHistorical);

    public static SolarPayload ToPayload(this SolarPoint point, DateTimeOffset modelRun)
        => new(point.ValidFrom, modelRun, point.DirectRadiation, point.DiffuseRadiation,
            point.ShortwaveRadiation, point.SunshineDuration);

    public static MarinePayload ToPayload(this MarinePoint point, DateTimeOffset modelRun)
        => new(point.ValidFrom, modelRun, point.WaveHeight, point.WavePeriod, point.WaveDirection, point.WaterTemperature);

    public static IReadOnlyList<MqttMessage> ToMessages(
        this DomainForecast<WeatherPoint> forecast, Location location, string baseTopic)
    {
        if (forecast.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var point = forecast.Points[0];
        return Build(baseTopic, location, DomainKind.Weather,
            point.ToPayload(forecast.ModelRun), MqttPayloadJsonContext.Default.WeatherPayload,
            [
                ("temperature", Num(point.Temperature)),
                ("apparent_temperature", Num(point.ApparentTemperature)),
                ("wind_speed", Num(point.WindSpeed)),
                ("precipitation", Num(point.Precipitation)),
                ("precipitation_probability", Num(point.PrecipitationProbability)),
                ("cloud_cover", Num(point.CloudCover)),
                ("pressure", Num(point.Pressure)),
                ("weather_code", Num(point.WeatherCode)),
                ("humidity", Num(point.Humidity)),
                ("dew_point", Num(point.DewPoint)),
                ("uv_index", Num(point.UvIndex)),
                ("ensemble_std_dev", Num(point.EnsembleStdDev)),
                ("wind_gusts", Num(point.WindGusts)),
                ("visibility", Num(point.Visibility)),
                ("cape", Num(point.Cape)),
                ("rain", Num(point.Rain)),
                ("snowfall", Num(point.Snowfall)),
                ("soil_temperature", Num(point.SoilTemperature)),
                ("freezing_level_height", Num(point.FreezingLevelHeight)),
                ("is_day", point.IsDay is { } isDay ? (isDay ? "true" : "false") : null),
            ]);
    }

    public static IReadOnlyList<MqttMessage> ToMessages(
        this DomainForecast<AirQualityPoint> forecast, Location location, string baseTopic)
    {
        if (forecast.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var point = forecast.Points[0];
        return Build(baseTopic, location, DomainKind.AirQuality,
            point.ToPayload(forecast.ModelRun), MqttPayloadJsonContext.Default.AirQualityPayload,
            [
                ("pm25", Num(point.Pm25)),
                ("pm10", Num(point.Pm10)),
                ("ozone", Num(point.Ozone)),
                ("birch_pollen", Num(point.BirchPollen)),
                ("grass_pollen", Num(point.GrassPollen)),
                ("alder", Num(point.Alder)),
                ("mugwort", Num(point.Mugwort)),
            ]);
    }

    public static IReadOnlyList<MqttMessage> ToMessages(
        this DomainForecast<FloodPoint> forecast, Location location, string baseTopic)
    {
        if (forecast.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var point = forecast.Points[0];
        return Build(baseTopic, location, DomainKind.Flood,
            point.ToPayload(forecast.ModelRun), MqttPayloadJsonContext.Default.FloodPayload,
            [
                ("discharge", Num(point.Discharge)),
                ("discharge_p10", Num(point.DischargeP10)),
                ("discharge_p90", Num(point.DischargeP90)),
                ("median_historical", Num(point.MedianHistorical)),
            ]);
    }

    public static IReadOnlyList<MqttMessage> ToMessages(
        this DomainForecast<SolarPoint> forecast, Location location, string baseTopic)
    {
        if (forecast.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var point = forecast.Points[0];
        return Build(baseTopic, location, DomainKind.Solar,
            point.ToPayload(forecast.ModelRun), MqttPayloadJsonContext.Default.SolarPayload,
            [
                ("direct_radiation", Num(point.DirectRadiation)),
                ("diffuse_radiation", Num(point.DiffuseRadiation)),
                ("shortwave_radiation", Num(point.ShortwaveRadiation)),
                ("sunshine_duration", Num(point.SunshineDuration)),
            ]);
    }

    public static IReadOnlyList<MqttMessage> ToMessages(
        this DomainForecast<MarinePoint> forecast, Location location, string baseTopic)
    {
        if (forecast.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var point = forecast.Points[0];
        return Build(baseTopic, location, DomainKind.Marine,
            point.ToPayload(forecast.ModelRun), MqttPayloadJsonContext.Default.MarinePayload,
            [
                ("wave_height", Num(point.WaveHeight)),
                ("wave_period", Num(point.WavePeriod)),
                ("wave_direction", Num(point.WaveDirection)),
                ("water_temperature", Num(point.WaterTemperature)),
            ]);
    }

    private static List<MqttMessage> Build<TPayload>(
        string baseTopic,
        Location location,
        DomainKind domain,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo,
        (string Parameter, string? Value)[] scalars)
    {
        var messages = new List<MqttMessage>(scalars.Length + 1)
        {
            new(TopicScheme.DomainTopic(baseTopic, location.Name, domain, StateSegment),
                JsonSerializer.Serialize(payload, typeInfo), true),
        };

        foreach (var (parameter, value) in scalars)
        {
            if (value is not null)
            {
                messages.Add(new MqttMessage(TopicScheme.DomainTopic(baseTopic, location.Name, domain, parameter), value, true));
            }
        }

        return messages;
    }

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Num(double? value) => value is { } number ? Num(number) : null;
}
