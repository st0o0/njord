using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class WeightedTargetSpec
{
    private static readonly LocationOptions Loc = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };
    private static readonly WeatherModel Model = new("icon_d2");
    private static readonly CycleId Cycle = new(new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero));

    [Fact(Timeout = 5000)]
    public void Properties_are_accessible()
    {
        var target = new WeightedTarget(Loc, Model, 1, Cycle);

        Assert.Equal(Loc, target.Location);
        Assert.Equal(Model, target.Model);
        Assert.Equal(1, target.Weight);
        Assert.Equal(Cycle, target.Cycle);
    }

    [Fact(Timeout = 5000)]
    public void Compute_weight_is_one_for_small_requests()
    {
        var weight = WeightedTarget.ComputeWeight(9, 4);

        Assert.Equal(1, weight);
    }

    [Fact(Timeout = 5000)]
    public void Compute_weight_increases_with_many_variables()
    {
        var weight = WeightedTarget.ComputeWeight(25, 4);

        Assert.True(weight > 1);
    }
}
