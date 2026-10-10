using Akka.Actor;
using Akka.Hosting;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Egress;
using Njord.Messages.Snapshots;
using Njord.Weather;
using CoverageTier = Njord.Core.Configuration.CoverageTier;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    IOptions<NjordOptions> options,
    ActorRegistry actorRegistry,
    TimeProvider timeProvider) : WeatherService.WeatherServiceBase
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);
    private readonly NjordOptions _options = options.Value;

    public override Task<GetCatalogResponse> GetCatalog(GetCatalogRequest request, ServerCallContext context)
    {
        var response = new GetCatalogResponse();
        var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var loc in _options.Locations)
        {
            var models = _options.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase).ToList();
            response.Locations.Add(CatalogMapper.ToLocationInfo(loc, models));

            foreach (var modelId in models)
            {
                if (seenModels.Add(modelId))
                {
                    response.Models.Add(CatalogMapper.ToModelInfo(modelId));
                }
            }
        }

        return Task.FromResult(response);
    }

    public override async Task<GetForecastResponse> GetForecast(GetForecastRequest request, ServerCallContext context)
    {
        var location = FindLocation(request.Location);
        ValidateModel(location, request.Model);

        var region = await actorRegistry.GetAsync<IForecastSnapshotRegion>();
        var result = await region.Ask<QueryForecastResponse>(
            new QueryForecast(request.Location, request.Model), AskTimeout);

        return result switch
        {
            QueryForecastCompleted found => MapForecastResponse(found.Forecast, timeProvider.GetUtcNow()),
            _ => throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No forecast data available yet for '{request.Location}/{request.Model}'")),
        };
    }

    public override Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        // TODO: restructure-enrichment-pipeline — query GrpcConsumerActor in-memory state
        throw new RpcException(new GrpcStatus(StatusCode.Unimplemented,
            "Enrichment queries are being restructured"));
    }

    public override Task StreamForecasts(
        StreamForecastsRequest request,
        IServerStreamWriter<ForecastUpdate> responseStream,
        ServerCallContext context)
    {
        // TODO: restructure-enrichment-pipeline — request SourceRef from GrpcConsumerActor
        throw new RpcException(new GrpcStatus(StatusCode.Unimplemented,
            "Forecast streaming is being restructured"));
    }

    public override Task StreamEnrichments(
        StreamEnrichmentsRequest request,
        IServerStreamWriter<EnrichmentEvent> responseStream,
        ServerCallContext context)
    {
        // TODO: restructure-enrichment-pipeline — request SourceRef from GrpcConsumerActor
        throw new RpcException(new GrpcStatus(StatusCode.Unimplemented,
            "Enrichment streaming is being restructured"));
    }

    private LocationOptions FindLocation(string name)
    {
        return _options.Locations.FirstOrDefault(l =>
            string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new RpcException(new GrpcStatus(StatusCode.NotFound, $"Location '{name}' not found"));
    }

    private void ValidateModel(LocationOptions location, string modelId)
    {
        var models = _options.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase).ToList();
        if (!models.Contains(modelId, StringComparer.OrdinalIgnoreCase))
        {
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"Model '{modelId}' not configured for '{location.Name}'"));
        }
    }

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

    private static GetForecastResponse MapForecastResponse(ModelForecast forecast, DateTimeOffset now)
    {
        var response = new GetForecastResponse
        {
            Location = forecast.Location,
            Model = forecast.Model.Id,
            UpdatedAt = Timestamp.FromDateTimeOffset(now),
        };
        MapForecastPoints(forecast, response.Hourly, response.Daily);
        return response;
    }

    private static ForecastUpdate MapForecastUpdate(EgressEvent.PerModelUpdate update, DateTimeOffset now)
    {
        var proto = new ForecastUpdate
        {
            Location = update.Location,
            Model = update.Model.Id,
            UpdatedAt = Timestamp.FromDateTimeOffset(now),
        };
        MapForecastPoints(update.Forecast, proto.Hourly, proto.Daily);
        return proto;
    }

    private static void MapForecastPoints(
        ModelForecast forecast,
        Google.Protobuf.Collections.RepeatedField<HourlyForecast> hourlyTarget,
        Google.Protobuf.Collections.RepeatedField<DailyForecast> dailyTarget)
    {
        hourlyTarget.AddRange(forecast.Hourly.Points.Select(ToHourlyForecast));
        dailyTarget.AddRange(forecast.Daily.Points.Select(ToDailyForecast));
    }

    internal static HourlyForecast ToHourlyForecast(ForecastPoint point)
    {
        var hourly = new HourlyForecast { ValidAt = Timestamp.FromDateTimeOffset(point.ValidAt) };
        SetOptional(point, ParameterRegistry.Temperature2m, v => hourly.Temperature = v);
        SetOptional(point, ParameterRegistry.ApparentTemperature, v => hourly.ApparentTemperature = v);
        SetOptional(point, ParameterRegistry.Precipitation, v => hourly.Precipitation = v);
        SetOptional(point, ParameterRegistry.RelativeHumidity2m, v => hourly.Humidity = v);
        SetOptional(point, ParameterRegistry.WindSpeed10m, v => hourly.WindSpeed = v);
        SetOptional(point, ParameterRegistry.WindGusts10m, v => hourly.WindGusts = v);
        var windDir = point.Get(ParameterRegistry.GetByApiName("wind_direction_10m")!);
        if (windDir.HasValue)
        {
            hourly.WindBearing = windDir.Value;
        }

        SetOptional(point, ParameterRegistry.CloudCover, v => hourly.CloudCover = v);
        var weatherCode = point.Get(ParameterRegistry.WeatherCode);
        if (weatherCode.HasValue)
        {
            hourly.WeatherCode = (int)weatherCode.Value;
        }

        SetOptional(point, ParameterRegistry.IsDay, v => hourly.IsDay = v > 0);
        var rain = point.Get(ParameterRegistry.GetByApiName("rain")!);
        if (rain.HasValue)
        {
            hourly.Rain = rain.Value;
        }

        SetOptional(point, ParameterRegistry.PressureMsl, v => hourly.PressureMsl = v);

        foreach (var (param, value) in point.Values)
        {
            if (value is null || HourlyFixedFields.Contains(param.ApiName))
            {
                continue;
            }

            hourly.Extra.Add(new ParameterValue { Name = param.ApiName, Numeric = value.Value });
        }

        return hourly;
    }

    internal static DailyForecast ToDailyForecast(DailyForecastPoint point)
    {
        var daily = new DailyForecast { Date = point.Date.ToString("O") };
        var tempMax = point.GetNumeric(ParameterRegistry.GetByApiName("temperature_2m_max")!);
        if (tempMax.HasValue)
        {
            daily.TemperatureMax = tempMax.Value;
        }

        var tempMin = point.GetNumeric(ParameterRegistry.GetByApiName("temperature_2m_min")!);
        if (tempMin.HasValue)
        {
            daily.TemperatureMin = tempMin.Value;
        }

        var precipSum = point.GetNumeric(ParameterRegistry.GetByApiName("precipitation_sum")!);
        if (precipSum.HasValue)
        {
            daily.PrecipitationSum = precipSum.Value;
        }

        var windMax = point.GetNumeric(ParameterRegistry.GetByApiName("wind_speed_10m_max")!);
        if (windMax.HasValue)
        {
            daily.WindSpeedMax = windMax.Value;
        }

        var gustMax = point.GetNumeric(ParameterRegistry.GetByApiName("wind_gusts_10m_max")!);
        if (gustMax.HasValue)
        {
            daily.WindGustsMax = gustMax.Value;
        }

        daily.Sunrise = point.GetMeta(ParameterRegistry.GetByApiName("sunrise")!) ?? "";
        daily.Sunset = point.GetMeta(ParameterRegistry.GetByApiName("sunset")!) ?? "";
        var wc = point.GetNumeric(ParameterRegistry.GetByApiName("weather_code")!);
        if (wc.HasValue)
        {
            daily.WeatherCode = (int)wc.Value;
        }

        foreach (var (param, value) in point.NumericValues)
        {
            if (value is null || DailyFixedFields.Contains(param.ApiName))
            {
                continue;
            }

            daily.Extra.Add(new ParameterValue { Name = param.ApiName, Numeric = value.Value });
        }

        foreach (var (param, value) in point.MetaValues)
        {
            if (value is null || DailyFixedFields.Contains(param.ApiName))
            {
                continue;
            }

            daily.Extra.Add(new ParameterValue { Name = param.ApiName, Text = value });
        }

        return daily;
    }

    private static void SetOptional(ForecastPoint point, ParameterDef param, Action<double> setter)
    {
        var value = point.Get(param);
        if (value.HasValue)
        {
            setter(value.Value);
        }
    }
}
