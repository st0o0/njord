using Njord.Model;
namespace Njord.Forecast.Tests;

public sealed class DailyForecastPointSpec
{
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    [Fact]
    public void Get_returns_numeric_value_for_numeric_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue> { [TempMax] = 28.5 });

        Assert.Equal(28.5, point.Get(TempMax).Number);
    }

    [Fact]
    public void Get_returns_text_value_for_time_string_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue> { [Sunrise] = new ForecastValue(null, "05:31") });

        Assert.Equal("05:31", point.Get(Sunrise).Text);
    }

    [Fact]
    public void Get_returns_default_for_missing_parameter()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue>());

        var value = point.Get(TempMax);
        Assert.Multiple(
            () => Assert.Null(value.Number),
            () => Assert.Null(value.Text),
            () => Assert.False(value.HasValue));
    }

    [Fact]
    public void HasAnyValue_true_when_numeric_value_present()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue> { [TempMax] = 28.5 });

        Assert.True(point.HasAnyValue);
    }

    [Fact]
    public void HasAnyValue_true_when_text_value_present()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue> { [Sunrise] = new ForecastValue(null, "05:31") });

        Assert.True(point.HasAnyValue);
    }

    [Fact]
    public void HasAnyValue_false_when_empty()
    {
        var point = new DailyForecastPoint(
            new DateOnly(2026, 7, 14),
            new Dictionary<ParameterDef, ForecastValue>());

        Assert.False(point.HasAnyValue);
    }
}
