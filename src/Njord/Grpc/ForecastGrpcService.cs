using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Grpc.V1;
using GrpcStatus = Grpc.Core.Status;
using ActorSystem = Akka.Actor.ActorSystem;

namespace Njord.Grpc;

public sealed class ForecastGrpcService(
    IOptions<NjordOptions> options,
    ActorRegistry actorRegistry,
    ActorSystem actorSystem) : ForecastService.ForecastServiceBase
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);
    private readonly NjordOptions _options = options.Value;

    public override Task<GetLocationsResponse> GetLocations(GetLocationsRequest request, ServerCallContext context)
    {
        var response = new GetLocationsResponse();
        response.Locations.AddRange(_options.Locations.Select(l => l.Name));
        return Task.FromResult(response);
    }

    public override Task<GetModelsResponse> GetModels(GetModelsRequest request, ServerCallContext context)
    {
        var location = FindLocation(request.Location);
        var response = new GetModelsResponse { Location = location.Name };
        response.Models.AddRange(location.ResolveModels(_options.Models));
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
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No forecast data available yet for '{request.Location}/{request.Model}'"));

        return MapForecastResponse(result.Forecast);
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
            .SelectAsync(1, async update =>
            {
                var proto = MapForecastUpdate(update);
                await responseStream.WriteAsync(proto);
                return proto;
            })
            .RunWith(Sink.Ignore<ForecastUpdate>(), mat)
            .WaitAsync(context.CancellationToken);
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
            var evt = EnrichmentProtoMapper.MapToEvent(
                request.Location, typeName, resultObj, DateTimeOffset.UtcNow);
            if (evt is null) continue;

            switch (evt.PayloadCase)
            {
                case EnrichmentEvent.PayloadOneofCase.Alerts: response.Alerts = evt.Alerts; break;
                case EnrichmentEvent.PayloadOneofCase.Indices: response.Indices = evt.Indices; break;
                case EnrichmentEvent.PayloadOneofCase.Trends: response.Trends = evt.Trends; break;
                case EnrichmentEvent.PayloadOneofCase.Energy: response.Energy = evt.Energy; break;
                case EnrichmentEvent.PayloadOneofCase.Derived: response.Derived = evt.Derived; break;
                case EnrichmentEvent.PayloadOneofCase.History: response.History = evt.History; break;
                case EnrichmentEvent.PayloadOneofCase.Consensus: response.Consensus = evt.Consensus; break;
            }
        }

        return response;
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
            .SelectAsync(1, async update =>
            {
                var evt = EnrichmentProtoMapper.MapToEvent(
                    update.Location, update.TypeName, update.Result, DateTimeOffset.UtcNow);
                if (evt is not null)
                    await responseStream.WriteAsync(evt);
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
        var models = location.ResolveModels(_options.Models);
        if (!models.Contains(modelId, StringComparer.OrdinalIgnoreCase))
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"Model '{modelId}' not configured for '{location.Name}'"));
    }

    private static GetForecastResponse MapForecastResponse(ModelForecast forecast)
    {
        var response = new GetForecastResponse
        {
            Location = forecast.Location,
            Model = forecast.Model.Id,
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        MapForecastPoints(forecast, response.Hourly, response.Daily);
        return response;
    }

    private static ForecastUpdate MapForecastUpdate(EgressEvent.PerModelUpdate update)
    {
        var proto = new ForecastUpdate
        {
            Location = update.Location,
            Model = update.Model.Id,
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        MapForecastPoints(update.Forecast, proto.Hourly, proto.Daily);
        return proto;
    }

    private static void MapForecastPoints(
        ModelForecast forecast,
        Google.Protobuf.Collections.RepeatedField<HourlyForecast> hourlyTarget,
        Google.Protobuf.Collections.RepeatedField<DailyForecast> dailyTarget)
    {
        foreach (var point in forecast.Hourly.Points)
        {
            var hourly = new HourlyForecast { Timestamp = point.ValidAt.ToUnixTimeSeconds() };
            SetOptional(point, ParameterRegistry.Temperature2m, v => hourly.Temperature = v);
            SetOptional(point, ParameterRegistry.ApparentTemperature, v => hourly.ApparentTemperature = v);
            SetOptional(point, ParameterRegistry.Precipitation, v => hourly.Precipitation = v);
            SetOptional(point, ParameterRegistry.RelativeHumidity2m, v => hourly.Humidity = v);
            SetOptional(point, ParameterRegistry.WindSpeed10m, v => hourly.WindSpeed = v);
            SetOptional(point, ParameterRegistry.WindGusts10m, v => hourly.WindGusts = v);
            var windDir = point.Get(ParameterRegistry.GetByApiName("wind_direction_10m")!);
            if (windDir.HasValue) hourly.WindBearing = windDir.Value;
            SetOptional(point, ParameterRegistry.CloudCover, v => hourly.CloudCover = v);
            var weatherCode = point.Get(ParameterRegistry.WeatherCode);
            if (weatherCode.HasValue) hourly.WeatherCode = (int)weatherCode.Value;
            SetOptional(point, ParameterRegistry.IsDay, v => hourly.IsDay = v > 0);
            var rain = point.Get(ParameterRegistry.GetByApiName("rain")!);
            if (rain.HasValue) hourly.Rain = rain.Value;
            SetOptional(point, ParameterRegistry.PressureMsl, v => hourly.PressureMsl = v);
            hourlyTarget.Add(hourly);
        }

        foreach (var point in forecast.Daily.Points)
        {
            var daily = new DailyForecast { Date = point.Date.ToString("O") };
            var tempMax = point.GetNumeric(ParameterRegistry.GetByApiName("temperature_2m_max")!);
            if (tempMax.HasValue) daily.TemperatureMax = tempMax.Value;
            var tempMin = point.GetNumeric(ParameterRegistry.GetByApiName("temperature_2m_min")!);
            if (tempMin.HasValue) daily.TemperatureMin = tempMin.Value;
            var precipSum = point.GetNumeric(ParameterRegistry.GetByApiName("precipitation_sum")!);
            if (precipSum.HasValue) daily.PrecipitationSum = precipSum.Value;
            var windMax = point.GetNumeric(ParameterRegistry.GetByApiName("wind_speed_10m_max")!);
            if (windMax.HasValue) daily.WindSpeedMax = windMax.Value;
            var gustMax = point.GetNumeric(ParameterRegistry.GetByApiName("wind_gusts_10m_max")!);
            if (gustMax.HasValue) daily.WindGustsMax = gustMax.Value;
            daily.Sunrise = point.GetMeta(ParameterRegistry.GetByApiName("sunrise")!) ?? "";
            daily.Sunset = point.GetMeta(ParameterRegistry.GetByApiName("sunset")!) ?? "";
            var wc = point.GetNumeric(ParameterRegistry.GetByApiName("weather_code")!);
            if (wc.HasValue) daily.WeatherCode = (int)wc.Value;
            dailyTarget.Add(daily);
        }
    }

    private static void SetOptional(ForecastPoint point, ParameterDef param, Action<double> setter)
    {
        var value = point.Get(param);
        if (value.HasValue) setter(value.Value);
    }
}
