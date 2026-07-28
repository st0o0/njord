using Newtonsoft.Json;
using Njord.Persistence;
using Njord.Pipeline;
using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class SchedulerDtoSerializationSpec
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(Timeout = 5000)]
    public Task DataChanged_dto_round_trips_through_json()
    {
        var domain = new SchedulerActor.DataChanged("lucerne", "icon_d2", 42, TestTime);
        var dto = SchedulerDtoMapping.ToDto(domain);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void DataChanged_dto_deserializes_back_to_domain()
    {
        var original = new SchedulerActor.DataChanged("lucerne", "icon_d2", 42, TestTime);
        var dto = SchedulerDtoMapping.ToDto(original);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<DataChangedDto>(json)!;
        var result = SchedulerDtoMapping.ToDomain(deserialized);

        Assert.Equal(original.Location, result.Location);
        Assert.Equal(original.ModelId, result.ModelId);
        Assert.Equal(original.Hash, result.Hash);
        Assert.Equal(original.Utc, result.Utc);
    }

    [Fact(Timeout = 5000)]
    public void DataChanged_dto_ignores_unknown_fields()
    {
        var json = """{"v":1,"loc":"lucerne","model":"icon_d2","hash":42,"utc":638899272000000000,"future_field":"hello"}""";
        var dto = JsonConvert.DeserializeObject<DataChangedDto>(json)!;
        var result = SchedulerDtoMapping.ToDomain(dto);

        Assert.Equal("lucerne", result.Location);
    }

    [Fact(Timeout = 5000)]
    public void Scheduler_snapshot_dto_round_trips_full_state()
    {
        var states = new Dictionary<string, ModelPollState>
        {
            ["lucerne|icon_d2"] = new ModelPollState(
                LastHash: 42,
                LastChangeUtc: TestTime,
                PrevChangeUtc: TestTime.AddHours(-1),
                NextPollUtc: TestTime.AddHours(1),
                MissCount: 2,
                Phase: PollPhase.Steady,
                Cycle: TimeSpan.FromHours(1)),
            ["zurich|gfs_seamless"] = ModelPollState.Initial(TestTime),
        };

        var dto = SchedulerDtoMapping.ToSnapshot(states);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<SchedulerSnapshotDto>(json)!;
        var result = SchedulerDtoMapping.FromSnapshot(deserialized);

        Assert.Equal(2, result.Count);

        var steady = result["lucerne|icon_d2"];
        Assert.Equal(42, steady.LastHash);
        Assert.Equal(TestTime, steady.LastChangeUtc);
        Assert.Equal(TestTime.AddHours(-1), steady.PrevChangeUtc);
        Assert.Equal(TestTime.AddHours(1), steady.NextPollUtc);
        Assert.Equal(2, steady.MissCount);
        Assert.Equal(PollPhase.Steady, steady.Phase);
        Assert.Equal(TimeSpan.FromHours(1), steady.Cycle);

        var discovery = result["zurich|gfs_seamless"];
        Assert.Null(discovery.LastHash);
        Assert.Equal(PollPhase.Discovery, discovery.Phase);
        Assert.Null(discovery.Cycle);
    }

    [Fact(Timeout = 5000)]
    public void Scheduler_snapshot_dto_round_trips_empty_state()
    {
        var states = new Dictionary<string, ModelPollState>();

        var dto = SchedulerDtoMapping.ToSnapshot(states);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<SchedulerSnapshotDto>(json)!;
        var result = SchedulerDtoMapping.FromSnapshot(deserialized);

        Assert.Empty(result);
    }
}
