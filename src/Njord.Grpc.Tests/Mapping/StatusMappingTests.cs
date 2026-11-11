using Google.Protobuf.WellKnownTypes;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using ProtoPollPhase = Njord.Grpc.Proto.PollPhase;

namespace Njord.Grpc.Tests.Mapping;

public sealed class StatusMappingTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(PollPhase.Discovery, ProtoPollPhase.Discovery)]
    [InlineData(PollPhase.Steady, ProtoPollPhase.Steady)]
    public void Poll_phase_maps_to_proto(PollPhase domain, ProtoPollPhase expected) =>
        Assert.Equal(expected, domain.ToProto());

    [Fact]
    public void Unknown_poll_phase_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((PollPhase)99).ToProto());

    [Fact]
    public void Model_status_maps_all_fields_with_optionals_present()
    {
        var lastChange = _now.AddHours(-1);
        var entry = new PollStateEntry(_vienna, PollPhase.Steady, _now, lastChange, 3, 3600);

        var status = entry.ToModelStatus();

        Assert.Multiple(
            () => Assert.Equal("Vienna", status.Location),
            () => Assert.Equal(ProtoPollPhase.Steady, status.Phase),
            () => Assert.Equal(_now.ToTimestamp(), status.NextPoll),
            () => Assert.Equal(lastChange.ToTimestamp(), status.LastChange),
            () => Assert.Equal(3, status.MissCount),
            () => Assert.Equal(3600, status.CycleSeconds));
    }

    [Fact]
    public void Model_status_omits_nullable_fields_when_null()
    {
        var entry = new PollStateEntry(_vienna, PollPhase.Discovery, _now, null, 0, null);

        var status = entry.ToModelStatus();

        Assert.Multiple(
            () => Assert.Null(status.LastChange),
            () => Assert.False(status.HasCycleSeconds));
    }

    [Fact]
    public void Trigger_poll_completed_maps_count_and_targets()
    {
        var completed = new TriggerImmediatePollCompleted(2, ["Vienna", "Berlin"]);

        var proto = completed.ToProto();

        Assert.Multiple(
            () => Assert.Equal(2, proto.TriggeredCount),
            () => Assert.Equal(["Vienna", "Berlin"], proto.Targets));
    }

    [Fact]
    public void Budget_status_maps_all_fields_and_usage_percent()
    {
        var budget = new BudgetStatusResult(150_000, 5_000, 300_000, 10_000);

        var proto = budget.ToProto();

        Assert.Multiple(
            () => Assert.Equal(300_000, proto.MonthlyLimit),
            () => Assert.Equal(150_000, proto.MonthlyUsed),
            () => Assert.Equal(10_000, proto.DailyLimit),
            () => Assert.Equal(5_000, proto.DailyUsed),
            () => Assert.Equal(50.0, proto.UsagePercent));
    }

    [Fact]
    public void Budget_status_with_zero_monthly_limit_has_zero_usage_percent()
    {
        var budget = new BudgetStatusResult(100, 10, 0, 0);

        Assert.Equal(0.0, budget.ToProto().UsagePercent);
    }
}
