using Njord.Core.Flood;

namespace Njord.Core.Tests.Flood;

public sealed class FloodPointTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new FloodPoint(At, Discharge: 1, DischargeMean: 2);

        Assert.True(point.HasFields(FloodParams.Discharge | FloodParams.DischargeMean));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new FloodPoint(At, Discharge: 1);

        Assert.False(point.HasFields(FloodParams.Discharge | FloodParams.DischargeMean));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new FloodPoint(At).HasFields(FloodParams.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new FloodPoint(At, Discharge: 1);

        Assert.True(point.HasFields(FloodParams.Discharge));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new FloodPoint(At);

        Assert.All(
            Enum.GetValues<FloodParams>().Where(p => p != FloodParams.None),
            flag => Assert.False(point.HasFields(flag)));
    }

    [Fact]
    public void Should_require_ensemble_members_for_the_ensemble_flag()
    {
        Assert.Multiple(
            () => Assert.False(new FloodPoint(At).HasFields(FloodParams.Ensemble)),
            () => Assert.True(new FloodPoint(At, EnsembleMembers: [1.0, 2.0]).HasFields(FloodParams.Ensemble)));
    }
}
