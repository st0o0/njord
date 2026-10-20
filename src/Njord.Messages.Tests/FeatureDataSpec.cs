using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Tests;

public sealed class FeatureDataSpec
{
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);

    [Fact]
    public void Should_format_time_as_utc_iso_text()
    {
        var value = new DateTimeOffset(2026, 7, 1, 8, 30, 5, TimeSpan.FromHours(2));

        Assert.Equal("2026-07-01T06:30:05Z", FeatureData.Time(value));
    }

    [Fact]
    public void Should_format_numbers_with_the_invariant_culture()
    {
        Assert.Multiple(
            () => Assert.Equal("1.5", FeatureData.Number(1.5)),
            () => Assert.Equal("42", FeatureData.Number(42)));
    }

    [Fact]
    public void Should_format_flags_as_lowercase_text()
    {
        Assert.Multiple(
            () => Assert.Equal("true", FeatureData.Flag(true)),
            () => Assert.Equal("false", FeatureData.Flag(false)));
    }

    [Fact]
    public void Should_build_a_dictionary_from_entries()
    {
        var data = FeatureData.Of(("a", "1"), ("b", "2"));

        Assert.Multiple(
            () => Assert.Equal(2, data.Count),
            () => Assert.Equal("1", data["a"]),
            () => Assert.Equal("2", data["b"]));
    }

    [Fact]
    public void Should_tag_each_result_with_its_output_kind()
    {
        var at = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var data = FeatureData.Of();
        FeatureResult[] results =
        [
            new ScoreResult(Lucerne, "laundry", 80, at, 3),
            new ConditionResult(Lucerne, "commute", "good", data),
            new TrendResult(Lucerne, "confidencetrend", "improving", 0.5, data),
            new ForecastResult(Lucerne, "seasonaloutlook", ImmutableArray<ForecastEntry>.Empty),
        ];

        Assert.Equal(
            [OutputKind.Score, OutputKind.Condition, OutputKind.Trend, OutputKind.Forecast],
            results.Select(result => result.Output));
    }
}
