using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureRuleTests
{
    private static readonly Location TestLocation = new("Test", 48.0, 11.0);
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_skips_when_project_returns_null()
    {
        var rule = new SkippingRule();
        var ring = new RingBuffer<LocationForecast>(1);
        ring.Push(new LocationForecast(TestLocation));

        var result = rule.Execute(ring, DateTimeOffset.UtcNow);

        Assert.Null(result);
    }

    [Fact]
    public void Execute_skips_when_evaluate_returns_null()
    {
        var rule = new NullEvalRule();
        var ring = new RingBuffer<LocationForecast>(1);
        ring.Push(ForecastWithWeather());

        var result = rule.Execute(ring, DateTimeOffset.UtcNow);

        Assert.Null(result);
    }

    [Fact]
    public void Execute_returns_result_when_evaluate_produces_one()
    {
        var rule = new ProducingRule();
        var ring = new RingBuffer<LocationForecast>(1);
        ring.Push(ForecastWithWeather());

        var result = rule.Execute(ring, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.IsType<TestFeatureResult>(result);
    }

    [Fact]
    public void Execute_provides_history_up_to_poll_depth()
    {
        var rule = new HistoryCapturingRule(pollDepth: 2);
        var ring = new RingBuffer<LocationForecast>(10);
        ring.Push(ForecastWithWeather());
        ring.Push(ForecastWithWeather());
        ring.Push(ForecastWithWeather());
        ring.Push(ForecastWithWeather());

        rule.Execute(ring, DateTimeOffset.UtcNow);

        Assert.NotNull(rule.CapturedHistory);
        Assert.Equal(3, rule.CapturedHistory!.Count);
    }

    [Fact]
    public void Execute_provides_partial_history_when_fewer_polls_available()
    {
        var rule = new HistoryCapturingRule(pollDepth: 8);
        var ring = new RingBuffer<LocationForecast>(10);
        ring.Push(ForecastWithWeather());
        ring.Push(ForecastWithWeather());

        rule.Execute(ring, DateTimeOffset.UtcNow);

        Assert.NotNull(rule.CapturedHistory);
        Assert.Equal(2, rule.CapturedHistory!.Count);
    }

    private static LocationForecast ForecastWithWeather()
    {
        var point = new ForecastPoint(Now, Temperature: 20.0);
        var forecast = new DomainForecast<ForecastPoint>(DomainKind.Forecast, Now, TimeResolution.Hourly, [point]);
        return new LocationForecast(TestLocation, Forecast: forecast);
    }

    private sealed class SkippingRule : FeatureRule<ForecastInput>
    {
        public override FeatureDescriptor Descriptor => new(OutputKind.Alert, FeatureCategory.Forecast, "Test", true, default);
        public override bool Enabled => true;
        protected override ForecastInput? Project(LocationForecast f) => null;
        protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) => throw new InvalidOperationException("Should not be called");
    }

    private sealed class NullEvalRule : FeatureRule<ForecastInput>
    {
        public override FeatureDescriptor Descriptor => new(OutputKind.Alert, FeatureCategory.Forecast, "Test", true, default);
        public override bool Enabled => true;
        protected override ForecastInput? Project(LocationForecast f) => f.Forecast is { } w ? new(w) : null;
        protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) => null;
    }

    private sealed class ProducingRule : FeatureRule<ForecastInput>
    {
        public override FeatureDescriptor Descriptor => new(OutputKind.Alert, FeatureCategory.Forecast, "Test", true, default);
        public override bool Enabled => true;
        protected override ForecastInput? Project(LocationForecast f) => f.Forecast is { } w ? new(w) : null;
        protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) => new TestFeatureResult(TestLocation);
    }

    private sealed class HistoryCapturingRule(int pollDepth) : FeatureRule<ForecastInput>
    {
        public IReadOnlyList<ForecastInput>? CapturedHistory { get; private set; }
        public override FeatureDescriptor Descriptor => new(OutputKind.Trend, FeatureCategory.Insight, "Test", true, default, PollDepth: pollDepth);
        public override bool Enabled => true;
        protected override ForecastInput? Project(LocationForecast f) => f.Forecast is { } w ? new(w) : null;
        protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
        {
            CapturedHistory = ctx.History;
            return null;
        }
    }

    private sealed record TestFeatureResult(Location Location) : FeatureResult(Location);
}
