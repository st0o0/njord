using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Grpc;

public sealed class ForecastSnapshotDto
{
    public Dictionary<string, ModelForecastDto> Forecasts { get; set; } = new();
}

public sealed class ModelForecastDto
{
    public string ModelId { get; set; } = "";
    public string Location { get; set; } = "";
    public long CycleUtcTicks { get; set; }
    public ForecastPointDto[] Hourly { get; set; } = [];
    public DailyForecastPointDto[] Daily { get; set; } = [];
}

public sealed class ForecastPointDto
{
    public long ValidAtUtcTicks { get; set; }
    public Dictionary<string, double?> Values { get; set; } = new();
}

public sealed class DailyForecastPointDto
{
    public string Date { get; set; } = "";
    public Dictionary<string, double?> NumericValues { get; set; } = new();
    public Dictionary<string, string?> MetaValues { get; set; } = new();
}

public sealed class EnrichmentSnapshotDto
{
    public Dictionary<string, EnrichmentEntryDto> Enrichments { get; set; } = new();
}

public sealed class EnrichmentEntryDto
{
    public string TypeName { get; set; } = "";
    public string JsonPayload { get; set; } = "";
}

public static class SnapshotMapping
{
    private static readonly Dictionary<string, Type> EnrichmentTypes = new()
    {
        ["AlertResult"] = typeof(Domain.Analysis.AlertResult),
        ["IndexResult"] = typeof(Domain.Analysis.IndexResult),
        ["TrendResult"] = typeof(Domain.Analysis.TrendResult),
        ["DerivedResult"] = typeof(Domain.Analysis.DerivedResult),
        ["EnergyResult"] = typeof(Domain.Analysis.EnergyResult),
        ["ConsensusResult"] = typeof(Domain.Analysis.ConsensusResult),
    };

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Include,
    };

    public static ForecastSnapshotDto ToDto(Dictionary<string, ModelForecast> state)
    {
        var dto = new ForecastSnapshotDto();
        foreach (var (key, forecast) in state)
            dto.Forecasts[key] = ToDto(forecast);
        return dto;
    }

    public static Dictionary<string, ModelForecast> ToDomain(ForecastSnapshotDto dto)
    {
        var state = new Dictionary<string, ModelForecast>();
        foreach (var (key, forecastDto) in dto.Forecasts)
        {
            var domain = ToDomain(forecastDto);
            if (domain is not null)
                state[key] = domain;
        }
        return state;
    }

    private static ModelForecastDto ToDto(ModelForecast forecast)
    {
        return new ModelForecastDto
        {
            ModelId = forecast.Model.Id,
            Location = forecast.Location,
            CycleUtcTicks = forecast.Cycle.Timestamp.UtcTicks,
            Hourly = forecast.Hourly.Points.Select(ToDto).ToArray(),
            Daily = forecast.Daily.Points.Select(ToDto).ToArray(),
        };
    }

    private static ForecastPointDto ToDto(ForecastPoint point)
    {
        var dto = new ForecastPointDto
        {
            ValidAtUtcTicks = point.ValidAt.UtcTicks,
        };
        foreach (var (param, value) in point.Values)
            dto.Values[param.ApiName] = value;
        return dto;
    }

    private static DailyForecastPointDto ToDto(DailyForecastPoint point)
    {
        var dto = new DailyForecastPointDto
        {
            Date = point.Date.ToString("O"),
        };
        foreach (var (param, value) in point.NumericValues)
            dto.NumericValues[param.ApiName] = value;
        foreach (var (param, value) in point.MetaValues)
            dto.MetaValues[param.ApiName] = value;
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
                    values[param] = value;
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
                    numeric[param] = value;
            }

            var meta = new Dictionary<ParameterDef, string?>();
            foreach (var (apiName, value) in pointDto.MetaValues)
            {
                var param = ParameterRegistry.GetByApiName(apiName);
                if (param is not null)
                    meta[param] = value;
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

    public static EnrichmentSnapshotDto ToDto(Dictionary<string, object> state)
    {
        var dto = new EnrichmentSnapshotDto();
        foreach (var (key, value) in state)
        {
            var typeName = value.GetType().Name;
            dto.Enrichments[key] = new EnrichmentEntryDto
            {
                TypeName = typeName,
                JsonPayload = JsonConvert.SerializeObject(value, JsonSettings),
            };
        }
        return dto;
    }

    public static Dictionary<string, object> ToDomain(EnrichmentSnapshotDto dto)
    {
        var state = new Dictionary<string, object>();
        foreach (var (key, entry) in dto.Enrichments)
        {
            if (!EnrichmentTypes.TryGetValue(entry.TypeName, out var type))
                continue;

            var value = JsonConvert.DeserializeObject(entry.JsonPayload, type, JsonSettings);
            if (value is not null)
                state[key] = value;
        }
        return state;
    }
}
