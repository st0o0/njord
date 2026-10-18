using Njord.Compute.Consensus;
using Njord.Weather;

namespace Njord.Indices.Tests;

public sealed class FrostProtectionSpec
{
    private static HorizonConsensus Hc(double? median, double? agreement = null) =>
        new(median, null, null, null, agreement, null, null, []);

    private static LocationConsensus Consensus(int cutoffHour, params (int Hour, HorizonConsensus Hc)[] temperatures) =>
        new(new LocationId("lucerne"),
            new HourlyConsensus(
                [new ParameterConsensus(ParameterRegistry.Temperature2m, temperatures.ToDictionary(t => Horizon.Hours(t.Hour), t => t.Hc))],
                cutoffHour),
            new DailyConsensus([], 0),
            DateTimeOffset.UnixEpoch);

    [Fact]
    public void First_frost_hour_and_agreement_at_hour_three()
    {
        var consensus = Consensus(48, (0, Hc(4)), (3, Hc(2, agreement: 0.666)), (10, Hc(-1)), (12, Hc(-2)));

        var frost = FrostProtection.Compute(consensus);

        Assert.NotNull(frost);
        Assert.Equal(10, frost.HoursUntilFrost);
        Assert.Equal(0.67, frost.Confidence);
    }

    [Fact]
    public void No_frost_within_48_hours_yields_null()
    {
        var consensus = Consensus(72, (0, Hc(4)), (49, Hc(-5)));

        Assert.Null(FrostProtection.Compute(consensus));
    }

    [Fact]
    public void Missing_agreement_at_hour_three_counts_as_full_confidence()
    {
        var consensus = Consensus(24, (1, Hc(0)));

        var frost = FrostProtection.Compute(consensus);

        Assert.NotNull(frost);
        Assert.Equal(1, frost.HoursUntilFrost);
        Assert.Equal(1.0, frost.Confidence);
    }
}
