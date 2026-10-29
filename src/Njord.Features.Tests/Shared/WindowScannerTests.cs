using Njord.Features.Shared;
using Njord.Messages.Results;

namespace Njord.Features.Tests.Shared;

public sealed class WindowScannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 6, 0, 0, TimeSpan.Zero);

    private static List<HourlyScore> Hours(params double[] scores) =>
        [.. scores.Select((score, i) => new HourlyScore(Start.AddHours(i), score))];

    [Fact]
    public void Empty_input_returns_no_windows()
    {
        Assert.Empty(WindowScanner.Scan([]));
    }

    [Fact]
    public void All_hours_below_threshold_return_no_windows()
    {
        Assert.Empty(WindowScanner.Scan(Hours(10, 20, 30, 49)));
    }

    [Fact]
    public void Single_run_returns_one_window_with_minimum_score()
    {
        var windows = WindowScanner.Scan(Hours(10, 60, 80, 70, 10));

        var window = Assert.Single(windows);
        Assert.Multiple(
            () => Assert.Equal(Start.AddHours(1), window.Start),
            () => Assert.Equal(Start.AddHours(4), window.End),
            () => Assert.Equal(60, window.Score),
            () => Assert.Null(window.Limiting));
    }

    [Fact]
    public void Multiple_runs_are_ranked_by_score()
    {
        var windows = WindowScanner.Scan(Hours(60, 60, 0, 90, 90, 0, 75, 75));

        Assert.Equal([90, 75, 60], windows.Select(w => w.Score));
    }

    [Fact]
    public void Max_windows_trims_the_lowest_ranked()
    {
        var windows = WindowScanner.Scan(Hours(60, 60, 0, 90, 90, 0, 75, 75), maxWindows: 2);

        Assert.Equal([90, 75], windows.Select(w => w.Score));
    }

    [Fact]
    public void Runs_shorter_than_min_hours_are_dropped()
    {
        var windows = WindowScanner.Scan(Hours(80, 0, 70, 70, 70, 0), minHours: 3);

        var window = Assert.Single(windows);
        Assert.Equal(Start.AddHours(2), window.Start);
    }

    [Fact]
    public void Window_reports_the_limiting_factor_of_the_boundary_hour()
    {
        var scores = new List<HourlyScore>
        {
            new(Start, 80),
            new(Start.AddHours(1), 80),
            new(Start.AddHours(2), 10, LimitingKind.Rain, 3.5),
        };

        var window = Assert.Single(WindowScanner.Scan(scores));

        Assert.NotNull(window.Limiting);
        Assert.Multiple(
            () => Assert.Equal(LimitingKind.Rain, window.Limiting.Kind),
            () => Assert.Equal(Start.AddHours(2), window.Limiting.From),
            () => Assert.Equal(3.5, window.Limiting.Value));
    }
}
