using Newtonsoft.Json;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Persistence;
using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class ForecastHistoryDtoSerializationSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static ForecastRecord MakeRecord()
    {
        var modelValues = new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>
        {
            [new WeatherModel("icon_d2")] = new Dictionary<string, double?> { ["temperature_2m"] = 22.5 },
            [new WeatherModel("gfs_seamless")] = new Dictionary<string, double?> { ["temperature_2m"] = 23.1 },
        };
        var consensus = new Dictionary<string, double?> { ["temperature_2m"] = 22.8 };
        return new ForecastRecord(T0, "lucerne", modelValues, consensus);
    }

    [Fact(Timeout = 5000)]
    public Task ForecastRecord_dto_round_trips_through_json()
    {
        var dto = ForecastHistoryDtoMapping.ToDto(MakeRecord());
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void ForecastRecord_dto_deserializes_back_to_domain()
    {
        var original = MakeRecord();
        var dto = ForecastHistoryDtoMapping.ToDto(original);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<ForecastRecordDto>(json)!;
        var result = ForecastHistoryDtoMapping.ToDomain(deserialized);

        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Location, result.Location);
        Assert.Equal(22.5, result.ModelValues[new WeatherModel("icon_d2")]["temperature_2m"]);
        Assert.Equal(22.8, result.ConsensusValues["temperature_2m"]);
    }

    [Fact(Timeout = 5000)]
    public Task ForecastHistorySnapshot_dto_round_trips_through_json()
    {
        var history = new ForecastHistory(retentionDays: 7);
        history.Add(MakeRecord());
        var dto = ForecastHistoryDtoMapping.ToDto(history);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void ForecastRecord_dto_ignores_unknown_fields()
    {
        var json = """{"v":1,"ts":638899272000000000,"loc":"lucerne","models":{},"consensus":{},"new_thing":true}""";
        var dto = JsonConvert.DeserializeObject<ForecastRecordDto>(json)!;
        var result = ForecastHistoryDtoMapping.ToDomain(dto);

        Assert.Equal("lucerne", result.Location);
    }
}
