using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class ResolvedParameterSetLookupSpec
{
    private static readonly ResolvedParameterSet Params =
        ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact(Timeout = 5000)]
    public void Get_returns_non_null_for_included_parameter()
    {
        var result = Params.Get(ParameterRegistry.Temperature2m);

        Assert.NotNull(result);
        Assert.Equal("temperature_2m", result!.ApiName);
    }

    [Fact(Timeout = 5000)]
    public void Get_returns_null_for_excluded_parameter()
    {
        var paramsWithExclusion = ParameterRegistry.Resolve(["Weather"], [], ["temperature_2m"]);

        Assert.Null(paramsWithExclusion.Get(ParameterRegistry.Temperature2m));
    }

    [Fact(Timeout = 5000)]
    public void Contains_returns_true_for_included_parameter()
    {
        Assert.True(Params.Contains(ParameterRegistry.WindSpeed10m));
    }

    [Fact(Timeout = 5000)]
    public void Contains_returns_false_for_excluded_parameter()
    {
        var paramsWithExclusion = ParameterRegistry.Resolve(["Weather"], [], ["wind_speed_10m"]);

        Assert.False(paramsWithExclusion.Contains(ParameterRegistry.WindSpeed10m));
    }
}
