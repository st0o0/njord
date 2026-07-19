using Newtonsoft.Json;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Persistence;

using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class EnrichmentResultSerializationSpec
{
    private static Dictionary<string, object> BuildState() => new()
    {
        ["lucerne|alerts"] = new AlertResult(
            "lucerne",
            [
                new Alert(
                    AlertType.Frost,
                    AlertSeverity.Yellow,
                    0.85,
                    new Dictionary<string, object?> { ["note"] = "overnight" },
                    TriggerValue: -1.5,
                    Threshold: 0.0,
                    PeakValue: -2.3,
                    HoursUntil: 6,
                    DurationHours: 4),
            ]),
        ["lucerne|indices"] = new IndexResult(
            "lucerne", 80, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75,
            new FrostProtectionInfo(6, 0.9),
            new VpdInfo("moderate", 0.8),
            new ScoreEnvelope(70, 90, 0.75)),
        ["lucerne|trends"] = new TrendResult(
            "lucerne",
            new Dictionary<string, ParameterTrend?>
            {
                ["temperature_2m"] = new ParameterTrend("rising", 1.2),
            },
            new WeatherChangeResult("cloudy", "clear", "clearing"),
            new PrecipTimingInfo(2, 5),
            new ExtremaTimingInfo(4, 10),
            new StabilityInfo("stable", 0.95),
            new DecayInfo(0.1, 24)),
        ["lucerne|derived"] = new DerivedResult(
            "lucerne",
            new Dictionary<string, HorizonDerived>
            {
                ["h3"] = new HorizonDerived(4, -1.2, "comfortable", "clear sky"),
            },
            new ScalarDerived(8.5, 65.0, false)),
        ["lucerne|energy"] = new EnergyResult(
            "lucerne", 42, 3.2,
            [new CopOptimalEntry(3, 3.6), new CopOptimalEntry(7, 3.9)],
            30, "charge", 1,
            HeatingDemandMax: 50,
            CopEstimateMin: 2.8,
            CopOptimalConservative: [3, 7]),
        ["lucerne|consensus"] = new ConsensusResult([], []),
    };

    [Fact(Timeout = 5000)]
    public Task All_enrichment_results_produce_stable_wire_format()
    {
        var state = BuildState();
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void All_enrichment_results_round_trip_through_nested_json()
    {
        var state = BuildState();
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnrichmentSnapshotDto>(json)!;
        var result = EnrichmentSnapshotMapping.ToDomain(deserialized);

        Assert.Equal(6, result.Count);

        var alerts = Assert.IsType<AlertResult>(result["lucerne|alerts"]);
        Assert.Equal("lucerne", alerts.Location);
        var alert = Assert.Single(alerts.Alerts);
        Assert.Equal(AlertType.Frost, alert.Type);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(0.85, alert.Confidence);
        Assert.Equal(-1.5, alert.TriggerValue);
        Assert.Equal(-2.3, alert.PeakValue);
        Assert.Equal(6, alert.HoursUntil);
        Assert.Equal(4, alert.DurationHours);

        var indices = Assert.IsType<IndexResult>(result["lucerne|indices"]);
        Assert.Equal(80, indices.Laundry);
        Assert.NotNull(indices.FrostProtection);
        Assert.Equal(6, indices.FrostProtection!.HoursUntilFrost);
        Assert.NotNull(indices.Vpd);
        Assert.Equal("moderate", indices.Vpd!.Category);
        Assert.NotNull(indices.LaundryEnvelope);
        Assert.Equal(70, indices.LaundryEnvelope!.Min);

        var trends = Assert.IsType<TrendResult>(result["lucerne|trends"]);
        Assert.Equal("rising", trends.ParameterTrends["temperature_2m"]!.Direction);
        Assert.NotNull(trends.WeatherChange);
        Assert.Equal(2, trends.PrecipTiming.StartsInHours);
        Assert.Equal(4, trends.ExtremaTiming.MaxInHours);
        Assert.Equal("stable", trends.Stability!.Label);
        Assert.Equal(0.1, trends.Decay!.DecayRate);

        var derived = Assert.IsType<DerivedResult>(result["lucerne|derived"]);
        Assert.Equal(4, derived.ByHorizon["h3"].Beaufort);
        Assert.Equal("comfortable", derived.ByHorizon["h3"].DewPointComfort);
        Assert.Equal(8.5, derived.Scalars.DiurnalAmplitude);

        var energy = Assert.IsType<EnergyResult>(result["lucerne|energy"]);
        Assert.Equal(42, energy.HeatingDemand);
        Assert.Equal(2, energy.CopOptimal.Count);
        Assert.Equal(50, energy.HeatingDemandMax);
        Assert.Equal(2.8, energy.CopEstimateMin);
        Assert.Equal([3, 7], energy.CopOptimalConservative);

        var consensus = Assert.IsType<ConsensusResult>(result["lucerne|consensus"]);
        Assert.Empty(consensus.Parameters);
        Assert.Empty(consensus.DailyParameters);
    }
}
