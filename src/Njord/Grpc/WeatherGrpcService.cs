using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Grpc.V2;
using GrpcStatus = Grpc.Core.Status;
using ActorSystem = Akka.Actor.ActorSystem;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    IOptions<NjordOptions> options,
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    TimeProvider timeProvider) : V2.WeatherService.WeatherServiceBase
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
            response.Locations.Add(new LocationInfo
            {
                Name = loc.Name,
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
                Models = { models },
            });

            foreach (var modelId in models)
            {
                if (!seenModels.Add(modelId))
                {
                    continue;
                }

                var coverage = ModelCoverageRegistry.Get(modelId);
                var info = new ModelInfo { Id = modelId };
                if (coverage is not null)
                {
                    info.DisplayName = coverage.DisplayName ?? modelId;
                    info.Provider = coverage.Provider ?? "";
                    info.Region = coverage.Region;
                    info.CoverageTier = coverage.Tier switch
                    {
                        Configuration.CoverageTier.Global => V2.CoverageTier.Global,
                        Configuration.CoverageTier.Continental => V2.CoverageTier.Continental,
                        Configuration.CoverageTier.Regional => V2.CoverageTier.Regional,
                        _ => V2.CoverageTier.Unspecified,
                    };
                    if (coverage.MaxForecastHours.HasValue)
                    {
                        info.MaxForecastHours = coverage.MaxForecastHours.Value;
                    }

                    if (coverage.ResolutionKm.HasValue)
                    {
                        info.ResolutionKm = coverage.ResolutionKm.Value;
                    }

                    if (coverage.Description is not null)
                    {
                        info.Description = coverage.Description;
                    }
                }
                response.Models.Add(info);
            }
        }

        return Task.FromResult(response);
    }

    public override async Task<GetForecastResponse> GetForecast(GetForecastRequest request, ServerCallContext context)
    {
        var location = FindLocation(request.Location);
        ValidateModel(location, request.Model);

        var actor = actorRegistry.Get<ForecastSnapshotActor>();
        var result = await actor.Ask<ForecastResponse>(
            new Grpc.GetForecast(request.Location, request.Model), AskTimeout);

        if (result.Forecast is null)
        {
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No forecast data available yet for '{request.Location}/{request.Model}'"));
        }

        return MapForecastResponse(result.Forecast, timeProvider.GetUtcNow());
    }

    public override async Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        FindLocation(request.Location);

        var actor = actorRegistry.Get<EnrichmentSnapshotActor>();
        var result = await actor.Ask<AllEnrichmentsResponse>(
            new GetAllEnrichments(request.Location), AskTimeout);

        var response = new GetEnrichmentsResponse { Location = request.Location };

        foreach (var (typeName, resultObj) in result.Results)
        {
            var updatedAt = resultObj is Domain.Analysis.ConsensusResult cr && cr.ComputedAt is { } computedAt
                ? computedAt
                : timeProvider.GetUtcNow();
            var evt = EnrichmentProtoMapper.MapToEvent(
                request.Location, typeName, resultObj, updatedAt);
            if (evt is null)
            {
                continue;
            }

            switch (evt.PayloadCase)
            {
                case EnrichmentEvent.PayloadOneofCase.Alerts: response.Alerts = evt.Alerts; break;
                case EnrichmentEvent.PayloadOneofCase.Indices: response.Indices = evt.Indices; break;
                case EnrichmentEvent.PayloadOneofCase.Trends: response.Trends = evt.Trends; break;
                case EnrichmentEvent.PayloadOneofCase.Derived: response.Derived = evt.Derived; break;
                case EnrichmentEvent.PayloadOneofCase.History: response.History = evt.History; break;
                case EnrichmentEvent.PayloadOneofCase.Consensus:
                    response.Consensus = evt.Consensus;
                    response.ConsensusUpdatedAt = evt.UpdatedAt;
                    break;
            }
        }

        return response;
    }

    public override async Task StreamForecasts(
        StreamForecastsRequest request,
        IServerStreamWriter<ForecastUpdate> responseStream,
        ServerCallContext context)
    {
        var egressActor = actorRegistry.Get<EgressActor>();
        var sourceResponse = await egressActor.Ask<EgressSourceResponse>(
            new RequestEgressSource(), context.CancellationToken);

        var mat = actorSystem.Materializer();

        await sourceResponse.SourceRef.Source
            .Collect(e => e is EgressEvent.PerModelUpdate, e => (EgressEvent.PerModelUpdate)e)
            .Where(u => string.IsNullOrEmpty(request.Location) ||
                        string.Equals(u.Location, request.Location, StringComparison.OrdinalIgnoreCase))
            .Log("grpc-stream-forecast", u => $"{u.Location}/{u.Model.Id}")
            .SelectAsync(1, async update =>
            {
                var proto = MapForecastUpdate(update, timeProvider.GetUtcNow());
                await responseStream.WriteAsync(proto);
                return proto;
            })
            .RunWith(Sink.Ignore<ForecastUpdate>(), mat)
            .WaitAsync(context.CancellationToken);
    }

    public override async Task StreamEnrichments(
        StreamEnrichmentsRequest request,
        IServerStreamWriter<EnrichmentEvent> responseStream,
        ServerCallContext context)
    {
        var egressActor = actorRegistry.Get<EgressActor>();
        var sourceResponse = await egressActor.Ask<EgressSourceResponse>(
            new RequestEgressSource(), context.CancellationToken);

        var mat = actorSystem.Materializer();

        await sourceResponse.SourceRef.Source
            .Collect(e => e is EgressEvent.EnrichmentUpdate, e => (EgressEvent.EnrichmentUpdate)e)
            .Where(u => string.IsNullOrEmpty(request.Location) ||
                        string.Equals(u.Location, request.Location, StringComparison.OrdinalIgnoreCase))
            .Log("grpc-stream-enrichment", u => $"{u.Location}/{u.TypeName}")
            .SelectAsync(1, async update =>
            {
                var updatedAt = update.UpdatedAt ?? timeProvider.GetUtcNow();
                var evt = EnrichmentProtoMapper.MapToEvent(
                    update.Location, update.TypeName, update.Result, updatedAt);
                if (evt is not null)
                {
                    await responseStream.WriteAsync(evt);
                }

                return evt;
            })
            .RunWith(Sink.Ignore<EnrichmentEvent?>(), mat)
            .WaitAsync(context.CancellationToken);
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
        Google.Protobuf.Collections.RepeatedField<V2.HourlyForecast> hourlyTarget,
        Google.Protobuf.Collections.RepeatedField<V2.DailyForecast> dailyTarget)
    {
        foreach (var point in forecast.Hourly.Points)
        {
            var hourly = new V2.HourlyForecast { ValidAt = Timestamp.FromDateTimeOffset(point.ValidAt) };
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

                hourly.Extra.Add(new V2.ParameterValue { Name = param.ApiName, Numeric = value.Value });
            }

            hourlyTarget.Add(hourly);
        }

        foreach (var point in forecast.Daily.Points)
        {
            var daily = new V2.DailyForecast { Date = point.Date.ToString("O") };
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

                daily.Extra.Add(new V2.ParameterValue { Name = param.ApiName, Numeric = value.Value });
            }

            foreach (var (param, value) in point.MetaValues)
            {
                if (value is null || DailyFixedFields.Contains(param.ApiName))
                {
                    continue;
                }

                daily.Extra.Add(new V2.ParameterValue { Name = param.ApiName, Text = value });
            }

            dailyTarget.Add(daily);
        }
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
