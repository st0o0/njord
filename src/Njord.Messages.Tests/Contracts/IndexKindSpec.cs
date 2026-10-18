using Njord.Consensus;
using Njord.Messages.Indices;

namespace Njord.Messages.Tests.Contracts;

public sealed class IndexKindSpec
{
    [Theory]
    [InlineData(IndexKind.Laundry, "laundry")]
    [InlineData(IndexKind.Outdoor, "outdoor")]
    [InlineData(IndexKind.Running, "running")]
    [InlineData(IndexKind.Cycling, "cycling")]
    [InlineData(IndexKind.Bbq, "bbq")]
    [InlineData(IndexKind.Irrigation, "irrigation")]
    [InlineData(IndexKind.Solar, "solar")]
    [InlineData(IndexKind.NightVentilation, "night_ventilation")]
    public void Index_kind_has_snake_case_wire_name(IndexKind kind, string expected) =>
        Assert.Equal(expected, kind.ToWireName());

    [Theory]
    [InlineData("Running", IndexKind.Running)]
    [InlineData("running", IndexKind.Running)]
    [InlineData("RUNNING", IndexKind.Running)]
    [InlineData("NightVentilation", IndexKind.NightVentilation)]
    [InlineData("nightventilation", IndexKind.NightVentilation)]
    [InlineData("bbq", IndexKind.Bbq)]
    public void Config_key_parses_case_insensitively(string key, IndexKind expected)
    {
        Assert.True(IndexKinds.TryParse(key, out var kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("Swimming")]
    [InlineData("")]
    [InlineData("3")]
    [InlineData(null)]
    public void Unknown_config_key_does_not_parse(string? key) =>
        Assert.False(IndexKinds.TryParse(key, out _));

    [Fact]
    public void All_lists_every_index_kind_once()
    {
        Assert.Equal(8, IndexKinds.All.Count);
        Assert.Equal(IndexKinds.All.Count, IndexKinds.All.Distinct().Count());
    }
}
