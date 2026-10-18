using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc.Tests;

public sealed class OpsProtoMappingsSpec
{
    private static readonly DateTimeOffset NextPoll = new(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastChange = new(2026, 7, 15, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToModelStatus_should_map_all_fields_when_optionals_present()
    {
        var entry = new PollStateEntry(new LocationId("lucerne"), "icon_d2", PollPhase.Steady, NextPoll, LastChange, 2, 3600);

        var status = OpsProtoMappings.ToModelStatus(entry);

        Assert.Multiple(
            () => Assert.Equal("lucerne", status.Location),
            () => Assert.Equal("icon_d2", status.Model),
            () => Assert.Equal(Njord.Grpc.V2.PollPhase.Steady, status.Phase),
            () => Assert.Equal(Timestamp.FromDateTimeOffset(NextPoll), status.NextPoll),
            () => Assert.Equal(2, status.MissCount),
            () => Assert.Equal(Timestamp.FromDateTimeOffset(LastChange), status.LastChange),
            () => Assert.Equal(3600, status.CycleSeconds));
    }

    [Fact]
    public void ToModelStatus_should_leave_optionals_unset_when_absent()
    {
        var entry = new PollStateEntry(new LocationId("lucerne"), "icon_d2", PollPhase.Discovery, NextPoll, null, 0, null);

        var status = OpsProtoMappings.ToModelStatus(entry);

        Assert.Multiple(
            () => Assert.Equal(Njord.Grpc.V2.PollPhase.Discovery, status.Phase),
            () => Assert.Null(status.LastChange),
            () => Assert.Equal(0, status.CycleSeconds));
    }

    [Fact]
    public void ToTriggerTarget_should_map_all_fields_when_optionals_present()
    {
        var entry = new PollStateEntry(new LocationId("zurich"), "ecmwf_ifs025", PollPhase.Steady, NextPoll, LastChange, 1, 7200);

        var target = OpsProtoMappings.ToTriggerTarget(entry);

        Assert.Multiple(
            () => Assert.Equal("zurich", target.Location),
            () => Assert.Equal("ecmwf_ifs025", target.Model),
            () => Assert.Equal(Njord.Grpc.V2.PollPhase.Steady, target.Phase),
            () => Assert.Equal(Timestamp.FromDateTimeOffset(NextPoll), target.NextPoll),
            () => Assert.Equal(1, target.MissCount),
            () => Assert.Equal(Timestamp.FromDateTimeOffset(LastChange), target.LastChange),
            () => Assert.Equal(7200, target.CycleSeconds));
    }

    [Fact]
    public void ToTriggerTarget_should_leave_optionals_unset_when_absent()
    {
        var entry = new PollStateEntry(new LocationId("zurich"), "ecmwf_ifs025", PollPhase.Discovery, NextPoll, null, 0, null);

        var target = OpsProtoMappings.ToTriggerTarget(entry);

        Assert.Multiple(
            () => Assert.Equal(Njord.Grpc.V2.PollPhase.Discovery, target.Phase),
            () => Assert.Null(target.LastChange),
            () => Assert.Equal(0, target.CycleSeconds));
    }
}
