using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Egress;
using Njord.Grpc.V2;
using Njord.Messages.Snapshots;
using Njord.Weather;
using ActorSystem = Akka.Actor.ActorSystem;
using CoverageTier = Njord.Core.Configuration.CoverageTier;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    IOptions<NjordOptions> options,
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
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

        var consumer = await actorRegistry.GetAsync<IGrpcConsumerActor>();
        var result = await consumer.Ask<QueryForecastResponse>(
            new QueryForecast(request.Location, request.Model), AskTimeout);

        return result switch
        {
            QueryForecastCompleted found => MapForecastResponse(found.Forecast, timeProvider.GetUtcNow()),
            _ => throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No forecast data available yet for '{request.Location}/{request.Model}'")),
        };
    }

    public override async Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        FindLocation(request.Location);

        var consumer = await actorRegistry.GetAsync<IGrpcConsumerActor>();
        var result = await consumer.Ask<GrpcConsumerActor.LatestResults>(
            new GrpcConsumerActor.QueryLatestResults(request.Location), AskTimeout);

        var response = new GetEnrichmentsResponse { Location = request.Location };

        if (result.LastAnalysis is { } analysis)
        {
            var evt = EnrichmentProtoMapper.MapToEvent(analysis, timeProvider.GetUtcNow());
            if (evt is not null)
            {
                switch (evt.PayloadCase)
                {
                    case EnrichmentEvent.PayloadOneofCase.Alerts: response.Alerts = evt.Alerts; break;
                    case EnrichmentEvent.PayloadOneofCase.Indices: response.Indices = evt.Indices; break;
                    case EnrichmentEvent.PayloadOneofCase.Trends: response.Trends = evt.Trends; break;
                    case EnrichmentEvent.PayloadOneofCase.Derived: response.Derived = evt.Derived; break;
                    case EnrichmentEvent.PayloadOneofCase.Consensus:
                        response.Consensus = evt.Consensus;
                        response.ConsensusUpdatedAt = evt.UpdatedAt;
                        break;
                }
            }
        }

        return response;
    }

    public override async Task StreamForecasts(
        StreamForecastsRequest request,
        IServerStreamWriter<ForecastUpdate> responseStream,
        ServerCallContext context)
    {
        var sourceRef = await RequestGrpcStreamSourceAsync(context.CancellationToken);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-forecast-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(() => killSwitch.Shutdown());

        await sourceRef.Source
            .Collect(e => e is PipelineEvent.ForecastReceived, e => (PipelineEvent.ForecastReceived)e)
            .Where(u => string.IsNullOrEmpty(request.Location) ||
                        string.Equals(u.Location, request.Location, StringComparison.OrdinalIgnoreCase))
            .Via(killSwitch.Flow<PipelineEvent.ForecastReceived>())
            .SelectAsync(1, async update =>
            {
                var proto = MapForecastUpdate(update, timeProvider.GetUtcNow());
                await responseStream.WriteAsync(proto, context.CancellationToken);
                return proto;
            })
            .RunWith(Sink.Ignore<ForecastUpdate>(), mat);
    }

    public override async Task StreamEnrichments(
        StreamEnrichmentsRequest request,
        IServerStreamWriter<EnrichmentEvent> responseStream,
        ServerCallContext context)
    {
        var sourceRef = await RequestGrpcStreamSourceAsync(context.CancellationToken);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-enrichment-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(() => killSwitch.Shutdown());

        await sourceRef.Source
            .Where(e => e is PipelineEvent.ConsensusComputed or PipelineEvent.AlertComputed
                or PipelineEvent.DerivedComputed or PipelineEvent.TrendComputed
                or PipelineEvent.IndexComputed)
            .Via(killSwitch.Flow<PipelineEvent>())
            .SelectAsync(1, async update =>
            {
                var evt = EnrichmentProtoMapper.MapToEvent(update, timeProvider.GetUtcNow());
                if (evt is not null)
                    await responseStream.WriteAsync(evt, context.CancellationToken);
                return evt;
            })
            .RunWith(Sink.Ignore<EnrichmentEvent?>(), mat);
    }

    private async Task<ISourceRef<PipelineEvent>> RequestGrpcStreamSourceAsync(CancellationToken cancellationToken)
    {
        var consumer = await actorRegistry.GetAsync<IGrpcConsumerActor>();
        var reply = await consumer.Ask<object>(
            new GrpcConsumerActor.RequestGrpcStreamSource(0), cancellationToken);

        return reply switch
        {
            GrpcConsumerActor.GrpcStreamSourceResponse response => response.SourceRef,
            GrpcConsumerActor.GrpcStreamSourceFailed => throw new RpcException(
                new GrpcStatus(StatusCode.Unavailable, "Pipeline stream is currently unavailable")),
            _ => throw new RpcException(
                new GrpcStatus(StatusCode.Internal, $"Unexpected reply: {reply.GetType().Name}")),
        };
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

    private static GetForecastResponse MapForecastResponse(Forecast forecast, DateTimeOffset now)
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

    private static ForecastUpdate MapForecastUpdate(PipelineEvent.ForecastReceived update, DateTimeOffset now)
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
        Forecast forecast,
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
