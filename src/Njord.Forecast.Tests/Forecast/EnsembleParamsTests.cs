using Njord.Forecast.Forecast;

namespace Njord.Forecast.Tests.Forecast;

public sealed class EnsembleParamsTests
{
    [Fact]
    public void Flags_combine_correctly()
    {
        var combined = EnsembleParams.TemperatureSpread | EnsembleParams.PrecipitationSpread;

        Assert.Multiple(
            () => Assert.True(combined.HasFlag(EnsembleParams.TemperatureSpread)),
            () => Assert.True(combined.HasFlag(EnsembleParams.PrecipitationSpread)),
            () => Assert.False(combined.HasFlag(EnsembleParams.WindSpeedSpread)),
            () => Assert.False(combined.HasFlag(EnsembleParams.Models)));
    }

    [Fact]
    public void None_has_no_flags()
    {
        Assert.False(EnsembleParams.None.HasFlag(EnsembleParams.TemperatureSpread));
    }

    [Fact]
    public void All_flags_are_distinct_powers_of_two()
    {
        var values = Enum.GetValues<EnsembleParams>()
            .Where(p => p != EnsembleParams.None)
            .Select(p => (long)p)
            .ToList();

        Assert.All(values, v => Assert.True((v & (v - 1)) == 0));
        Assert.Equal(values.Count, values.Distinct().Count());
    }
}
