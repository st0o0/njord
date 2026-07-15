using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Grpc;
using Njord.Grpc.V1;

using DomainAlert = Njord.Domain.Analysis.Alert;
using DomainAlertType = Njord.Domain.Analysis.AlertType;
using DomainAlertSeverity = Njord.Domain.Analysis.AlertSeverity;
using DomainHorizonDerived = Njord.Domain.Analysis.HorizonDerived;
using DomainScalarDerived = Njord.Domain.Analysis.ScalarDerived;
using DomainParameterTrend = Njord.Domain.Analysis.ParameterTrend;
using DomainHorizonConsensus = Njord.Domain.Analysis.HorizonConsensus;
using DomainParameterConsensus = Njord.Domain.Analysis.ParameterConsensus;

namespace Njord.Tests.Grpc;

public sealed class EnrichmentProtoMapperSpec
{
    [Fact(Timeout = 5000)]
    public void MapAlerts_should_map_alert_type_and_severity_enums()
    {
        var result = new AlertResult("lucerne",
        [
            new DomainAlert(DomainAlertType.Frost, DomainAlertSeverity.Orange, 0.85,
                new Dictionary<string, object?>()),
            new DomainAlert(DomainAlertType.Storm, DomainAlertSeverity.Red, 0.92,
                new Dictionary<string, object?>()),
        ]);

        var update = EnrichmentProtoMapper.MapAlerts(result);

        Assert.Equal(2, update.Alerts.Count);

        Assert.Equal(Njord.Grpc.V1.AlertType.Frost, update.Alerts[0].Type);
        Assert.Equal(Njord.Grpc.V1.AlertSeverity.Orange, update.Alerts[0].Severity);
        Assert.Equal(0.85, update.Alerts[0].Confidence);

        Assert.Equal(Njord.Grpc.V1.AlertType.Storm, update.Alerts[1].Type);
        Assert.Equal(Njord.Grpc.V1.AlertSeverity.Red, update.Alerts[1].Severity);
        Assert.Equal(0.92, update.Alerts[1].Confidence);
    }

    [Fact(Timeout = 5000)]
    public void MapIndices_should_map_score_values_and_optional_fields()
    {
        var result = new IndexResult(
            Location: "lucerne",
            Laundry: 80, Outdoor: 70, Running: 65, Cycling: 75,
            Bbq: 90, Irrigation: 30, Hdd: 5.2, Cdd: 1.8,
            Solar: 85, Ventilation: 60,
            FrostProtection: (HoursUntilFrost: 8, Confidence: 0.7),
            Vpd: (Category: "optimal", Vpd: 1.2));

        var update = EnrichmentProtoMapper.MapIndices(result);

        Assert.Equal(80, update.Laundry);
        Assert.Equal(70, update.Outdoor);
        Assert.Equal(65, update.Running);
        Assert.Equal(75, update.Cycling);
        Assert.Equal(90, update.Bbq);
        Assert.Equal(30, update.Irrigation);
        Assert.Equal(85, update.Solar);
        Assert.Equal(60, update.Ventilation);
        Assert.Equal(5.2, update.Hdd);
        Assert.Equal(1.8, update.Cdd);
        Assert.Equal(8, update.FrostHours);
        Assert.Equal(0.7, update.FrostConfidence);
        Assert.Equal(1.2, update.VpdKpa);
        Assert.Equal("optimal", update.VpdCategory);
    }

    [Fact(Timeout = 5000)]
    public void MapTrends_should_map_parameter_trends_and_nullable_timing()
    {
        var trends = new Dictionary<string, DomainParameterTrend?>
        {
            ["temperature_2m"] = new DomainParameterTrend("rising", 1.5),
            ["wind_speed_10m"] = null,
        };
        var result = new TrendResult(
            Location: "lucerne",
            ParameterTrends: trends,
            WeatherChange: new WeatherChangeResult("Clear", "Rain", "Clear -> Rain"),
            PrecipTiming: (StartsInHours: 3, EndsInHours: 8),
            ExtremaTiming: (MaxInHours: 6, MinInHours: 18),
            Stability: (Label: "stable", Ratio: 0.9),
            Decay: (DecayRate: 0.15, ReliableHours: 24));

        var update = EnrichmentProtoMapper.MapTrends(result);

        Assert.Single(update.ParameterTrends);
        Assert.Equal("temperature_2m", update.ParameterTrends[0].Parameter);
        Assert.Equal("rising", update.ParameterTrends[0].Direction);
        Assert.Equal(1.5, update.ParameterTrends[0].Delta);
        Assert.Equal("Clear -> Rain", update.WeatherChangeDescription);
        Assert.Equal(3, update.PrecipStartsInHours);
        Assert.Equal(8, update.PrecipEndsInHours);
        Assert.Equal(6, update.TempMaxInHours);
        Assert.Equal(18, update.TempMinInHours);
        Assert.Equal("stable", update.StabilityLabel);
        Assert.Equal(0.9, update.StabilityRatio);
        Assert.Equal(0.15, update.DecayRate);
        Assert.Equal(24, update.ReliableHours);
    }

    [Fact(Timeout = 5000)]
    public void MapEnergy_should_map_all_fields_including_cop_optimal_list()
    {
        var result = new EnergyResult(
            Location: "lucerne",
            HeatingDemand: 75,
            CopEstimate: 3.2,
            CopOptimal: [(HoursFromNow: 2, Cop: 3.8), (HoursFromNow: 5, Cop: 4.1)],
            Shading: 60,
            BatteryStrategy: "charge",
            NightCooling: 45);

        var update = EnrichmentProtoMapper.MapEnergy(result);

        Assert.Equal(75, update.HeatingDemand);
        Assert.Equal(3.2, update.CopEstimate);
        Assert.Equal(60, update.Shading);
        Assert.Equal("charge", update.BatteryStrategy);
        Assert.Equal(45, update.NightCooling);
        Assert.Equal(2, update.CopOptimal.Count);
        Assert.Equal(2, update.CopOptimal[0].HoursFromNow);
        Assert.Equal(3.8, update.CopOptimal[0].Cop);
        Assert.Equal(5, update.CopOptimal[1].HoursFromNow);
        Assert.Equal(4.1, update.CopOptimal[1].Cop);
    }

    [Fact(Timeout = 5000)]
    public void MapDerived_should_map_horizon_entries_and_scalars()
    {
        var byHorizon = new Dictionary<string, DomainHorizonDerived>
        {
            ["h3"] = new DomainHorizonDerived(Beaufort: 4, WindChill: -2.5, DewPointComfort: "comfortable", WmoDescription: "Partly cloudy"),
        };
        var scalars = new DomainScalarDerived(DiurnalAmplitude: 12.3, SunshinePct: 65.0, Inversion: true);
        var result = new DerivedResult("lucerne", byHorizon, scalars);

        var update = EnrichmentProtoMapper.MapDerived(result);

        Assert.Single(update.ByHorizon);
        Assert.Equal("h3", update.ByHorizon[0].Horizon);
        Assert.Equal(4, update.ByHorizon[0].Beaufort);
        Assert.Equal(-2.5, update.ByHorizon[0].WindChill);
        Assert.Equal("comfortable", update.ByHorizon[0].DewpointComfort);
        Assert.Equal("Partly cloudy", update.ByHorizon[0].WmoDescription);
        Assert.Equal(12.3, update.Scalars.DiurnalAmplitude);
        Assert.Equal(65.0, update.Scalars.SunshinePct);
        Assert.True(update.Scalars.Inversion);
    }

    [Fact(Timeout = 5000)]
    public void MapHistory_should_map_per_model_metrics_and_anomaly()
    {
        var model = new WeatherModel("icon_d2");
        var result = new HistoryResult(
            Location: "lucerne",
            Mae7d: new Dictionary<WeatherModel, double?> { [model] = 1.2 },
            Mae30d: new Dictionary<WeatherModel, double?> { [model] = 1.5 },
            Weights: new Dictionary<WeatherModel, double> { [model] = 0.8 },
            Drift: new Dictionary<WeatherModel, double?> { [model] = 0.3 },
            SeasonalBest: model,
            Anomaly: (IsAnomaly: true, DeviationSigma: 2.1),
            WeightedTemperature: 18.5);

        var update = EnrichmentProtoMapper.MapHistory(result);

        Assert.Single(update.Models);
        Assert.Equal("icon_d2", update.Models[0].Model);
        Assert.Equal(1.2, update.Models[0].Mae7D);
        Assert.Equal(1.5, update.Models[0].Mae30D);
        Assert.Equal(0.8, update.Models[0].Weight);
        Assert.Equal(0.3, update.Models[0].Drift);
        Assert.Equal("icon_d2", update.SeasonalBest);
        Assert.True(update.Anomaly);
        Assert.Equal(2.1, update.AnomalyDeviation);
        Assert.Equal(18.5, update.WeightedTemperature);
    }

    [Fact(Timeout = 5000)]
    public void MapConsensus_should_map_parameter_and_horizon_nesting()
    {
        var param = new ParameterDef("temperature_2m", "C", "temperature", "temperature_2m",
            ParameterGroup.Weather, ParameterGranularity.Hourly);

        var horizonConsensus = new DomainHorizonConsensus(
            Median: 20.5,
            TrimmedMean: 20.3,
            Spread: 2.1,
            Iqr: 1.5,
            Agreement: 0.85,
            Outlier: (Model: new WeatherModel("gfs_seamless"), Deviation: 3.2),
            ConfidenceInterval: (Lower: 18.0, Upper: 23.0),
            AvailableModels: [new WeatherModel("icon_d2"), new WeatherModel("ecmwf_ifs025")]);

        var paramConsensus = new DomainParameterConsensus(
            param,
            new Dictionary<string, DomainHorizonConsensus> { ["h3"] = horizonConsensus });

        var result = new ConsensusResult([paramConsensus]);

        var update = EnrichmentProtoMapper.MapConsensus(result);

        Assert.Single(update.Parameters);
        Assert.Equal("temperature_2m", update.Parameters[0].Parameter);
        Assert.Equal("C", update.Parameters[0].Unit);
        Assert.Single(update.Parameters[0].ByHorizon);

        var h = update.Parameters[0].ByHorizon[0];
        Assert.Equal("h3", h.Horizon);
        Assert.Equal(20.5, h.Median);
        Assert.Equal(20.3, h.TrimmedMean);
        Assert.Equal(2.1, h.Spread);
        Assert.Equal(1.5, h.Iqr);
        Assert.Equal(0.85, h.Agreement);
        Assert.Equal(2, h.AvailableModels);
    }

    [Fact(Timeout = 5000)]
    public void MapToEvent_should_wrap_alert_result_in_enrichment_event()
    {
        var alertResult = new AlertResult("lucerne",
        [
            new DomainAlert(DomainAlertType.Heat, DomainAlertSeverity.Yellow, 0.75,
                new Dictionary<string, object?>()),
        ]);
        var updatedAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var evt = EnrichmentProtoMapper.MapToEvent("lucerne", "alerts", alertResult, updatedAt);

        Assert.NotNull(evt);
        Assert.Equal("lucerne", evt.Location);
        Assert.Equal("alerts", evt.TypeName);
        Assert.Equal(updatedAt.ToUnixTimeSeconds(), evt.UpdatedAt);
        Assert.NotNull(evt.Alerts);
        Assert.Single(evt.Alerts.Alerts);
    }

    [Fact(Timeout = 5000)]
    public void MapToEvent_should_return_null_for_unknown_type_name()
    {
        var result = new AlertResult("lucerne", []);
        var evt = EnrichmentProtoMapper.MapToEvent("lucerne", "unknown", result, DateTimeOffset.UtcNow);

        Assert.Null(evt);
    }
}
