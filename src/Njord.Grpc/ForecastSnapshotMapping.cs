using Njord.Domain.Weather;
using Njord.Persistence;

namespace Njord.Grpc;

public static class ForecastSnapshotMapping
{
    public static ForecastSnapshotDto ToDto(Dictionary<string, ModelForecast> state)
    {
        var dto = new ForecastSnapshotDto();
        foreach (var (key, forecast) in state)
        {
            dto.Forecasts[key] = ToDto(forecast);
        }

        return dto;
    }

    public static Dictionary<string, ModelForecast> ToDomain(ForecastSnapshotDto dto)
    {
        var state = new Dictionary<string, ModelForecast>();
        foreach (var (key, forecastDto) in dto.Forecasts)
        {
            var domain = ToDomain(forecastDto);
            if (domain is not null)
            {
                state[key] = domain;
            }
        }
        return state;
    }

    private static ModelForecastDto ToDto(ModelForecast forecast) => new()
    {
        ModelId = forecast.Model.Id,
        Location = forecast.Location,
        CycleUtcTicks = forecast.Cycle.Timestamp.UtcTicks,
        Hourly = forecast.Hourly.Points.Select(ToDto).ToArray(),
        Daily = forecast.Daily.Points.Select(ToDto).ToArray(),
    };

    private static ForecastPointDto ToDto(ForecastPoint point)
    {
        var dto = new ForecastPointDto { ValidAtUtcTicks = point.ValidAt.UtcTicks };
        foreach (var (param, value) in point.Values)
        {
            dto.Values[param.ApiName] = value;
        }

        return dto;
    }

    private static DailyForecastPointDto ToDto(DailyForecastPoint point)
    {
        var dto = new DailyForecastPointDto { Date = point.Date.ToString("O") };
        foreach (var (param, value) in point.NumericValues)
        {
            dto.NumericValues[param.ApiName] = value;
        }

        foreach (var (param, value) in point.MetaValues)
        {
            dto.MetaValues[param.ApiName] = value;
        }

        return dto;
    }

    private static ModelForecast? ToDomain(ModelForecastDto dto)
    {
        var hourlyPoints = new List<ForecastPoint>(dto.Hourly.Length);
        foreach (var pointDto in dto.Hourly)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (apiName, value) in pointDto.Values)
            {
                var param = ParameterRegistry.GetByApiName(apiName);
                if (param is not null)
                {
                    values[param] = value;
                }
            }
            hourlyPoints.Add(new ForecastPoint(
                new DateTimeOffset(pointDto.ValidAtUtcTicks, TimeSpan.Zero), values));
        }

        var dailyPoints = new List<DailyForecastPoint>(dto.Daily.Length);
        foreach (var pointDto in dto.Daily)
        {
            var numeric = new Dictionary<ParameterDef, double?>();
            foreach (var (apiName, value) in pointDto.NumericValues)
            {
                var param = ParameterRegistry.GetByApiName(apiName);
                if (param is not null)
                {
                    numeric[param] = value;
                }
            }
            var meta = new Dictionary<ParameterDef, string?>();
            foreach (var (apiName, value) in pointDto.MetaValues)
            {
                var param = ParameterRegistry.GetByApiName(apiName);
                if (param is not null)
                {
                    meta[param] = value;
                }
            }
            dailyPoints.Add(new DailyForecastPoint(
                DateOnly.ParseExact(pointDto.Date, "O"), numeric, meta));
        }

        return new ModelForecast(
            new WeatherModel(dto.ModelId),
            dto.Location,
            new CycleId(new DateTimeOffset(dto.CycleUtcTicks, TimeSpan.Zero)),
            new ForecastSeries(hourlyPoints),
            new DailyForecastSeries(dailyPoints));
    }
}
