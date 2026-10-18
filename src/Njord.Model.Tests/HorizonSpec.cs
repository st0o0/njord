namespace Njord.Model.Tests;

public sealed class HorizonSpec
{
    [Fact]
    public void Hourly_horizon_has_h_wire_key()
    {
        Assert.Equal("h12", Horizon.Hours(12).ToWireKey());
    }

    [Fact]
    public void Daily_horizon_has_d_wire_key()
    {
        Assert.Equal("d1", Horizon.Days(1).ToWireKey());
    }

    [Fact]
    public void Horizons_with_same_kind_and_offset_are_equal()
    {
        Assert.Equal(Horizon.Hours(3), Horizon.Hours(3));
        Assert.True(Horizon.Days(2) == Horizon.Days(2));
    }

    [Fact]
    public void Hourly_and_daily_horizons_with_same_offset_are_distinct()
    {
        Assert.NotEqual(Horizon.Hours(1), Horizon.Days(1));
    }

    [Fact]
    public void Horizon_exposes_kind_and_offset()
    {
        var hourly = Horizon.Hours(6);
        var daily = Horizon.Days(2);

        Assert.Multiple(
            () => Assert.Equal(HorizonKind.Hourly, hourly.Kind),
            () => Assert.Equal(6, hourly.Offset),
            () => Assert.True(hourly.IsHourly),
            () => Assert.Equal(HorizonKind.Daily, daily.Kind),
            () => Assert.Equal(2, daily.Offset),
            () => Assert.False(daily.IsHourly));
    }

    [Fact]
    public void Negative_offset_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Horizon.Hours(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Horizon.Days(-1));
    }

    [Theory]
    [InlineData("h0")]
    [InlineData("h72")]
    [InlineData("d0")]
    [InlineData("d6")]
    public void Wire_key_round_trips_through_parse(string key)
    {
        Assert.True(Horizon.TryParseWireKey(key, out var horizon));
        Assert.Equal(key, horizon.ToWireKey());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("h")]
    [InlineData("x3")]
    [InlineData("h-1")]
    [InlineData("h3a")]
    [InlineData("H3")]
    public void Invalid_wire_key_does_not_parse(string? key)
    {
        Assert.False(Horizon.TryParseWireKey(key, out _));
    }

    [Fact]
    public void ToString_returns_wire_key()
    {
        Assert.Equal("h24", Horizon.Hours(24).ToString());
    }
}
