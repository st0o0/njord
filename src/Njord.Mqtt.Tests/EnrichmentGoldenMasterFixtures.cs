using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Core.Configuration;
using Njord.Domain.Options;
using Njord.Domain.Weather;
using Njord.Mqtt.Presentation;

namespace Njord.Mqtt.Tests;

internal static class EnrichmentGoldenMasterFixtures
{
    public const string Location = "lucerne";
    public const string BaseTopic = "njord";
    public const string Version = "1.0.0-golden";

    public static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly FakeTimeProvider Time = new(T0);

    public static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    public static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    public static readonly ParameterDef DewPoint = ParameterRegistry.GetByApiName("dew_point_2m")!;
    public static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;
    public static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    public static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    public static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    public static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    public static readonly ParameterDef SurfacePressure = ParameterRegistry.GetByApiName("surface_pressure")!;
    public static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    public static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;

    public static readonly ResolvedParameterSet Parameters =
        ParameterRegistry.Resolve(["Weather", "Solar"], [], []);

    public static NjordOptions Options() => new()
    {
        Locations = [new LocationOptions { Name = Location, Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2", "ecmwf_ifs025"],
        Horizons = [3, 6, 12, 24, 48, 72],
        ForecastDays = 4,
        PollInterval = TimeSpan.FromMinutes(60),
        Mqtt = new MqttOptions { BaseTopic = BaseTopic },
        Enrichment = new EnrichmentOptions
        {
            Alerts = new AlertOptions { Enabled = true },
            Derived = new DerivedOptions { Enabled = true },
            Trends = new TrendOptions { Enabled = true },
            Indices = new IndexOptions { Enabled = true },
            History = new HistoryOptions { Enabled = true },
        },
    };

    public static DiscoveryContext Context(NjordOptions options) =>
        new(options.Mqtt, options.PollInterval, Version);

    public static IReadOnlyList<IEnrichmentPresenter> Presenters(NjordOptions options, ResolvedParameterSet? parameters = null)
    {
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        return
        [
            new ConsensusPresenter(wrapped, parameters ?? Parameters),
            new AlertPresenter(wrapped),
            new DerivedPresenter(wrapped),
            new TrendPresenter(wrapped),
            new IndexPresenter(wrapped),
            new HistoryPresenter(wrapped),
        ];
    }

    public static IEnrichmentPresenter Presenter(string typeName) =>
        Presenters(Options()).Single(p => p.TypeName == typeName);

    public static ModelForecast Forecast(WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var values = hourlyValues.ToDictionary(x => x.Param, x => (double?)x.Value);
        return new ModelForecast(
            model, Location, new CycleId(T0),
            new ForecastSeries(Enumerable.Range(0, 72)
                .Select(i => new ForecastPoint(T0.AddHours(i), values))),
            new DailyForecastSeries([]));
    }

    public static ConsensusSnapshot Consensus(ResolvedParameterSet parameters, params ModelForecast[] forecasts)
    {
        var snapshot = forecasts.Aggregate(ModelSnapshot.Empty, (s, f) => s.Update(f));
        return new ConsensusSnapshotFactory(parameters, Time).Create(snapshot, Location);
    }

    public static ConsensusSnapshot FullConsensus() => Consensus(
        Parameters,
        Forecast(new("icon_d2"),
            (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0), (WeatherCode, 0.0),
            (PressureMsl, 1020.0), (SurfacePressure, 1015.0), (SunshineDuration, 3600.0),
            (IsDay, 1.0), (Humidity, 55.0), (CloudCover, 30.0), (Precipitation, 0.0)),
        Forecast(new("ecmwf_ifs025"),
            (Temperature, 7.0), (WindSpeed, 9.5), (DewPoint, 4.0), (WeatherCode, 1.0),
            (PressureMsl, 1018.0), (SurfacePressure, 1013.0), (SunshineDuration, 3000.0),
            (IsDay, 1.0), (Humidity, 60.0), (CloudCover, 45.0), (Precipitation, 0.2)));

    public static ConsensusSnapshot PreviousConsensus() => Consensus(
        Parameters,
        Forecast(new("icon_d2"),
            (Temperature, 3.0), (WindSpeed, 4.0), (DewPoint, 2.0), (WeatherCode, 3.0),
            (PressureMsl, 1010.0), (SurfacePressure, 1005.0), (SunshineDuration, 1800.0),
            (IsDay, 1.0), (Humidity, 70.0), (CloudCover, 80.0), (Precipitation, 1.0)),
        Forecast(new("ecmwf_ifs025"),
            (Temperature, 4.0), (WindSpeed, 5.0), (DewPoint, 3.0), (WeatherCode, 3.0),
            (PressureMsl, 1011.0), (SurfacePressure, 1006.0), (SunshineDuration, 1500.0),
            (IsDay, 1.0), (Humidity, 72.0), (CloudCover, 85.0), (Precipitation, 1.2)));

    public static string Pretty(string json) =>
        JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public static string Render(IEnumerable<MqttMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            sb.Append("topic: ").Append(message.Topic).Append('\n');
            sb.Append("retain: ").Append(message.Retain).Append('\n');
            sb.Append(Pretty(message.Payload)).Append("\n\n");
        }

        return sb.ToString();
    }

    public static string RenderDiscovery(string deviceId, string payload) =>
        $"device_id: {deviceId}\n{Pretty(payload)}\n";
}
