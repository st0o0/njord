namespace Njord.Model.Tests;

public sealed class ForecastValueSpec
{
    [Fact]
    public void Numeric_value_has_number_and_no_text()
    {
        var value = new ForecastValue(22.5, null);

        Assert.Multiple(
            () => Assert.Equal(22.5, value.Number),
            () => Assert.Null(value.Text),
            () => Assert.True(value.HasValue));
    }

    [Fact]
    public void Text_value_has_text_and_no_number()
    {
        var value = new ForecastValue(null, "2026-07-14T06:00:00Z");

        Assert.Multiple(
            () => Assert.Null(value.Number),
            () => Assert.Equal("2026-07-14T06:00:00Z", value.Text),
            () => Assert.True(value.HasValue));
    }

    [Fact]
    public void Absent_value_has_neither_number_nor_text()
    {
        var value = new ForecastValue(null, null);

        Assert.Multiple(
            () => Assert.Null(value.Number),
            () => Assert.Null(value.Text),
            () => Assert.False(value.HasValue));
    }

    [Fact]
    public void Default_value_is_absent()
    {
        var value = default(ForecastValue);

        Assert.False(value.HasValue);
    }

    [Fact]
    public void Implicit_conversion_from_double()
    {
        ForecastValue value = 18.3;

        Assert.Multiple(
            () => Assert.Equal(18.3, value.Number),
            () => Assert.Null(value.Text));
    }

    [Fact]
    public void Implicit_conversion_from_nullable_double_with_value()
    {
        double? source = 42.0;
        ForecastValue value = source;

        Assert.Equal(42.0, value.Number);
    }

    [Fact]
    public void Implicit_conversion_from_nullable_double_null()
    {
        double? source = null;
        ForecastValue value = source;

        Assert.False(value.HasValue);
    }

    [Fact]
    public void Implicit_conversion_from_string()
    {
        ForecastValue value = "2026-07-14T20:00:00Z";

        Assert.Multiple(
            () => Assert.Null(value.Number),
            () => Assert.Equal("2026-07-14T20:00:00Z", value.Text));
    }

    [Fact]
    public void Implicit_conversion_from_null_string()
    {
        ForecastValue value = (string?)null;

        Assert.False(value.HasValue);
    }

}
