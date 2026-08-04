using Newtonsoft.Json;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Persistence;

using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class EnrichmentSnapshotDtoSerializationSpec
{
    [Fact(Timeout = 5000)]
    public Task EnrichmentSnapshot_dto_produces_stable_wire_format()
    {
        var state = new Dictionary<string, object>
        {
            ["lucerne|alerts"] = new AlertResult("lucerne", []),
            ["lucerne|indices"] = new IndexResult("lucerne", 80, 90, 70, 85, 95, 60, 88, 75, null, null),
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void EnrichmentSnapshot_dto_round_trips_to_domain()
    {
        var state = new Dictionary<string, object>
        {
            ["lucerne|alerts"] = new AlertResult("lucerne", []),
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnrichmentSnapshotDto>(json)!;
        var result = EnrichmentSnapshotMapping.ToDomain(deserialized);

        Assert.Single(result);
        Assert.IsType<AlertResult>(result["lucerne|alerts"]);
    }

    [Fact(Timeout = 5000)]
    public void HistoryResult_round_trips_through_enrichment_snapshot_dto()
    {
        var historyResult = new HistoryResult(
            "lucerne",
            Mae7d: new Dictionary<WeatherModel, double?> { [new("icon_d2")] = 1.5 },
            Mae30d: new Dictionary<WeatherModel, double?> { [new("icon_d2")] = 2.0 },
            Weights: new Dictionary<WeatherModel, double> { [new("icon_d2")] = 0.8 },
            Drift: new Dictionary<WeatherModel, double?> { [new("icon_d2")] = -0.3 },
            SeasonalBest: new WeatherModel("icon_d2"),
            Anomaly: (true, 2.1),
            WeightedTemperature: 18.5);

        var state = new Dictionary<string, object>
        {
            ["lucerne|history"] = historyResult,
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnrichmentSnapshotDto>(json)!;
        var result = EnrichmentSnapshotMapping.ToDomain(deserialized);

        Assert.Single(result);
        var recovered = Assert.IsType<HistoryResult>(result["lucerne|history"]);
        Assert.Equal("lucerne", recovered.Location);
        Assert.Equal(18.5, recovered.WeightedTemperature);
    }

    [Fact(Timeout = 5000)]
    public void ConsensusResult_round_trips_through_enrichment_snapshot_dto()
    {
        var param = new ParameterDef("temperature_2m", "C", "temperature", "temperature_2m",
            ParameterGroup.Weather, ParameterGranularity.Hourly);
        var horizon = new HorizonConsensus(
            Median: 20.5, TrimmedMean: 20.3, Spread: 2.1, Iqr: 1.5, Agreement: 0.85,
            Outlier: null, ConfidenceInterval: null,
            AvailableModels: [new WeatherModel("icon_d2")]);
        var hourlyParam = new ParameterConsensus(param,
            new Dictionary<string, HorizonConsensus> { ["h3"] = horizon });

        var dailyParamDef = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var dailyParam = new ParameterConsensus(dailyParamDef,
            new Dictionary<string, HorizonConsensus> { ["d0"] = horizon });

        var consensusResult = new ConsensusResult([hourlyParam], [dailyParam]);

        var state = new Dictionary<string, object>
        {
            ["lucerne|consensus"] = consensusResult,
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnrichmentSnapshotDto>(json)!;
        var result = EnrichmentSnapshotMapping.ToDomain(deserialized);

        Assert.Single(result);
        var recovered = Assert.IsType<ConsensusResult>(result["lucerne|consensus"]);
        Assert.Single(recovered.Parameters);
        Assert.Single(recovered.DailyParameters);
        Assert.Equal("temperature_2m", recovered.Parameters[0].Parameter.ApiName);
    }
}
