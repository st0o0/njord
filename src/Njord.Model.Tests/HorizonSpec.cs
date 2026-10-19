namespace Njord.Model.Tests;

public sealed class HorizonSpec
{
    [Theory]
    [InlineData(HorizonKind.Hourly, 6, "h6")]
    [InlineData(HorizonKind.Daily, 2, "d2")]
    public void Should_render_wire_key_with_kind_prefix(HorizonKind kind, int offset, string expected)
    {
        var horizon = kind == HorizonKind.Hourly ? Horizon.Hours(offset) : Horizon.Days(offset);

        Assert.Equal(expected, horizon.ToWireKey());
    }

    [Fact]
    public void Should_roundtrip_wire_key()
    {
        Assert.True(Horizon.TryParseWireKey("h48", out var horizon));
        Assert.Equal(Horizon.Hours(48), horizon);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("h")]
    [InlineData("x5")]
    [InlineData("h-3")]
    [InlineData("hfoo")]
    public void Should_reject_malformed_wire_keys(string? key)
    {
        Assert.False(Horizon.TryParseWireKey(key, out _));
    }

    [Fact]
    public void Should_reject_negative_offsets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Horizon.Hours(-1));
    }
}
