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
}
