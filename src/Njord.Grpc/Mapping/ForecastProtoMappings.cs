using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Forecast;
using Njord.Model;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Grpc.Mapping;

internal static class ForecastProtoMappings
{
    private static readonly HashSet<string> HourlyFixedFields =
    [
        "temperature_2m", "apparent_temperature", "precipitation", "relative_humidity_2m",
        "wind_speed_10m", "wind_gusts_10m", "wind_direction_10m", "cloud_cover",
        "weather_code", "is_day", "rain", "pressure_msl"
    ];

    private static readonly HashSet<string> DailyFixedFields =
    [
        "temperature_2m_max", "temperature_2m_min", "precipitation_sum",
        "wind_speed_10m_max", "wind_gusts_10m_max", "sunrise", "sunset", "weather_code"
    ];

    public static GetForecastResponse ToProto(this ForecastResult result, DateTimeOffset now)
    {
        var forecast = result.Forecast;
        var response = new GetForecastResponse
        {
            Location = forecast.Location,
            Model = forecast.Model.Id,
            UpdatedAt = now.ToTimestamp(),
        };
        AddPointsTo(forecast, response.Hourly, response.Daily);
        return response;
    }

    public static ForecastUpdate ToProto(this PipelineEvent.ForecastReceived update, DateTimeOffset now)
    {
        var proto = new ForecastUpdate
        {
            Location = update.Location.Name,
            Model = update.Model.Id,
            UpdatedAt = now.ToTimestamp(),
        };
        AddPointsTo(update.Forecast, proto.Hourly, proto.Daily);
        return proto;
    }

    private static void AddPointsTo(
        WeatherForecast forecast,
        Google.Protobuf.Collections.RepeatedField<HourlyForecast> hourlyTarget,
        Google.Protobuf.Collections.RepeatedField<DailyForecast> dailyTarget)
    {
        hourlyTarget.AddRange(forecast.Hourly.Points.Select(p => p.ToProto()));
        dailyTarget.AddRange(forecast.Daily.Points.Select(p => p.ToProto()));
    }

    public static HourlyForecast ToProto(this ForecastPoint point)
    {
        var hourly = new HourlyForecast { ValidAt = point.ValidAt.ToTimestamp() };
        SetOptional(point, ParameterRegistry.Temperature2m, v => hourly.Temperature = v);
        SetOptional(point, ParameterRegistry.ApparentTemperature, v => hourly.ApparentTemperature = v);
        SetOptional(point, ParameterRegistry.Precipitation, v => hourly.Precipitation = v);
        SetOptional(point, ParameterRegistry.RelativeHumidity2m, v => hourly.Humidity = v);
        SetOptional(point, ParameterRegistry.WindSpeed10m, v => hourly.WindSpeed = v);
        SetOptional(point, ParameterRegistry.WindGusts10m, v => hourly.WindGusts = v);
        var windDir = point.Get(ParameterRegistry.WindDirection10m).Number;
        if (windDir.HasValue)
        {
            hourly.WindBearing = windDir.Value;
        }

        SetOptional(point, ParameterRegistry.CloudCover, v => hourly.CloudCover = v);
        var weatherCode = point.Get(ParameterRegistry.WeatherCode).Number;
        if (weatherCode.HasValue)
        {
            hourly.WeatherCode = (int)weatherCode.Value;
        }

        SetOptional(point, ParameterRegistry.IsDay, v => hourly.IsDay = v > 0);
        var rain = point.Get(ParameterRegistry.Rain).Number;
        if (rain.HasValue)
        {
            hourly.Rain = rain.Value;
        }

        SetOptional(point, ParameterRegistry.PressureMsl, v => hourly.PressureMsl = v);

        foreach (var (param, value) in point.Values)
        {
            if (!value.Number.HasValue || HourlyFixedFields.Contains(param.ApiName))
            {
                continue;
            }

            hourly.Extra.Add(new ParameterValue { Name = param.ApiName, Numeric = value.Number.Value });
        }

        return hourly;
    }

    public static DailyForecast ToProto(this DailyForecastPoint point)
    {
        var daily = new DailyForecast { Date = point.Date.ToString("O") };
        var tempMax = point.Get(ParameterRegistry.Temperature2mMax).Number;
        if (tempMax.HasValue)
        {
            daily.TemperatureMax = tempMax.Value;
        }

        var tempMin = point.Get(ParameterRegistry.Temperature2mMin).Number;
        if (tempMin.HasValue)
        {
            daily.TemperatureMin = tempMin.Value;
        }

        var precipSum = point.Get(ParameterRegistry.PrecipitationSum).Number;
        if (precipSum.HasValue)
        {
            daily.PrecipitationSum = precipSum.Value;
        }

        var windMax = point.Get(ParameterRegistry.WindSpeed10mMax).Number;
        if (windMax.HasValue)
        {
            daily.WindSpeedMax = windMax.Value;
        }

        var gustMax = point.Get(ParameterRegistry.WindGusts10mMax).Number;
        if (gustMax.HasValue)
        {
            daily.WindGustsMax = gustMax.Value;
        }

        daily.Sunrise = point.Get(ParameterRegistry.Sunrise).Text ?? "";
        daily.Sunset = point.Get(ParameterRegistry.Sunset).Text ?? "";
        var wc = point.Get(ParameterRegistry.DailyWeatherCode).Number;
        if (wc.HasValue)
        {
            daily.WeatherCode = (int)wc.Value;
        }

        foreach (var (param, value) in point.Values)
        {
            if (DailyFixedFields.Contains(param.ApiName))
            {
                continue;
            }

            if (value.Number.HasValue)
            {
                daily.Extra.Add(new ParameterValue { Name = param.ApiName, Numeric = value.Number.Value });
            }
            else if (value.Text is not null)
            {
                daily.Extra.Add(new ParameterValue { Name = param.ApiName, Text = value.Text });
            }
        }

        return daily;
    }

    private static void SetOptional(ForecastPoint point, ParameterDef param, Action<double> setter)
    {
        var value = point.Get(param).Number;
        if (value.HasValue)
        {
            setter(value.Value);
        }
    }
}
