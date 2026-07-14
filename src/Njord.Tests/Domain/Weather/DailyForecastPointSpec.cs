using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class DailyForecastPointSpec
{
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    [Fact(Timeout = 5000)]
    public void GetNumeric_returns_value_for_numeric_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, double?> { [TempMax] = 28.5 },
            new Dictionary<ParameterDef, string?>());

        Assert.Equal(28.5, point.GetNumeric(TempMax));
    }

    [Fact(Timeout = 5000)]
    public void GetMeta_returns_value_for_time_string_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, double?>(),
            new Dictionary<ParameterDef, string?> { [Sunrise] = "05:31" });

        Assert.Equal("05:31", point.GetMeta(Sunrise));
    }

    [Fact(Timeout = 5000)]
    public void GetNumeric_returns_null_for_missing_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, double?>(),
            new Dictionary<ParameterDef, string?>());

        Assert.Null(point.GetNumeric(TempMax));
    }

    [Fact(Timeout = 5000)]
    public void GetMeta_returns_null_for_missing_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, double?>(),
            new Dictionary<ParameterDef, string?>());

        Assert.Null(point.GetMeta(Sunrise));
    }
}
