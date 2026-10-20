using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Features.Alerts.Frost;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class FeatureEngineSpec
{
    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed record Seen(WeatherInput Current, IReadOnlyList<WeatherInput> History);

    private sealed record ProbeResult(Location Location) : FeatureResult(Location);

    private sealed class RecordingRule(DomainRequirements requirements, int pollDepth = 0, bool enabled = true)
        : FeatureRule<WeatherInput>
    {
        public List<Seen> Calls { get; } = [];

        public override FeatureDescriptor Descriptor { get; } =
            new(OutputKind.Alert, FeatureCategory.Weather, true, requirements, PollDepth: pollDepth);

        public override bool Enabled => enabled;

        protected override WeatherInput? Project(LocationForecast forecast) =>
            forecast.Weather is { } w ? new(w) : null;

        protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
        {
            Calls.Add(new Seen(ctx.Current, [.. ctx.History]));
            return new ProbeResult(ctx.Location);
        }
    }

    private static DomainRequirements Temperature => new(Weather: WeatherParams.Temperature);

    private static FeatureEngine Engine(params IFeatureRule[] rules) =>
        new(rules, new FakeTimeProvider(Start));

    private static double? TemperatureOf(WeatherInput input) => input.Weather.Points[0].Temperature;

    private static DomainDataReceived Weather(Location location, double temperature) =>
        DomainData.Received(location, DomainData.Weather(Start, 4, (_, p) => p with { Temperature = temperature }));

    [Fact]
    public void The_domain_event_comes_first_followed_by_the_feature_result()
    {
        var engine = Engine(new RecordingRule(Temperature));

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Equal(2, events.Count);
        Assert.Multiple(
            () => Assert.IsType<PipelineEvent.WeatherDataReceived>(events[0]),
            () => Assert.IsType<ProbeResult>(events[1]));
    }

    [Fact]
    public void A_rule_without_poll_depth_sees_only_the_current_forecast()
    {
        var rule = new RecordingRule(Temperature);
        var engine = Engine(rule);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Lucerne, 4.0));

        var last = rule.Calls[^1];
        Assert.Multiple(
            () => Assert.Equal(2, rule.Calls.Count),
            () => Assert.Equal(4.0, TemperatureOf(last.Current)),
            () => Assert.Single(last.History));
    }

    [Fact]
    public void A_rule_with_poll_depth_receives_the_previous_poll_as_history()
    {
        var rule = new RecordingRule(Temperature, pollDepth: 1);
        var engine = Engine(rule);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Lucerne, 4.0));

        var last = rule.Calls[^1];
        Assert.Equal(2, last.History.Count);
        Assert.Multiple(
            () => Assert.Equal(4.0, TemperatureOf(last.Current)),
            () => Assert.Equal(4.0, TemperatureOf(last.History[0])),
            () => Assert.Equal(10.0, TemperatureOf(last.History[1])));
    }

    [Fact]
    public void An_update_of_another_domain_keeps_the_weather_history()
    {
        var rule = new RecordingRule(Temperature, pollDepth: 1);
        var engine = Engine(rule);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(DomainData.Received(DomainData.Lucerne, DomainData.Flood(Start)));
        engine.Process(Weather(DomainData.Lucerne, 1.0));

        var last = rule.Calls[^1];
        Assert.Equal(1.0, TemperatureOf(last.Current));
        Assert.Equal(2, last.History.Count);
    }

    [Fact]
    public void Locations_are_tracked_separately_and_case_insensitively()
    {
        var rule = new RecordingRule(Temperature, pollDepth: 1);
        var engine = Engine(rule);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Bern, 2.0));
        engine.Process(Weather(DomainData.Lucerne with { Name = "LUCERNE" }, 5.0));

        Assert.Multiple(
            () => Assert.Single(rule.Calls[1].History),
            () => Assert.Equal(2, rule.Calls[2].History.Count),
            () => Assert.Equal(10.0, TemperatureOf(rule.Calls[2].History[1])));
    }

    [Fact]
    public void Only_rules_requiring_the_changed_domain_are_invoked()
    {
        var weather = new RecordingRule(Temperature);
        var flood = new RecordingRule(new DomainRequirements(Flood: FloodParams.Discharge));
        var engine = Engine(weather, flood);

        engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Multiple(
            () => Assert.Single(weather.Calls),
            () => Assert.Empty(flood.Calls));
    }

    [Fact]
    public void A_rule_whose_optional_field_is_missing_still_runs()
    {
        var rule = new RecordingRule(new DomainRequirements(
            Weather: WeatherParams.Temperature, OptionalWeather: WeatherParams.ShortwaveRadiation));
        var engine = Engine(rule);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Multiple(
            () => Assert.Single(rule.Calls),
            () => Assert.Equal(2, events.Count));
    }

    [Fact]
    public void A_rule_whose_required_field_is_missing_is_skipped_and_emits_nothing()
    {
        var rule = new RecordingRule(new DomainRequirements(
            Weather: WeatherParams.Temperature | WeatherParams.ShortwaveRadiation));
        var engine = Engine(rule);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Multiple(
            () => Assert.Empty(rule.Calls),
            () => Assert.IsType<PipelineEvent.WeatherDataReceived>(Assert.Single(events)));
    }

    [Fact]
    public void Other_rules_still_run_when_one_is_skipped()
    {
        var skipped = new RecordingRule(new DomainRequirements(Weather: WeatherParams.ShortwaveRadiation));
        var running = new RecordingRule(Temperature);
        var engine = Engine(skipped, running);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Multiple(
            () => Assert.Empty(skipped.Calls),
            () => Assert.Single(running.Calls),
            () => Assert.Equal(2, events.Count));
    }

    [Fact]
    public void A_rule_requiring_a_domain_that_was_not_fetched_waits_for_it()
    {
        var rule = new RecordingRule(new DomainRequirements(
            Weather: WeatherParams.Temperature, Flood: FloodParams.Discharge));
        var engine = Engine(rule);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        Assert.Empty(rule.Calls);

        engine.Process(DomainData.Received(DomainData.Lucerne, DomainData.Flood(Start)));
        Assert.Single(rule.Calls);
    }

    [Fact]
    public void A_disabled_rule_is_never_invoked()
    {
        var rule = new RecordingRule(Temperature, enabled: false);
        var engine = Engine(rule);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Multiple(
            () => Assert.Empty(rule.Calls),
            () => Assert.Single(events));
    }

    [Fact]
    public void The_requirements_are_the_union_of_the_enabled_rules()
    {
        var engine = Engine(
            new RecordingRule(new DomainRequirements(Weather: WeatherParams.Temperature)),
            new RecordingRule(new DomainRequirements(Weather: WeatherParams.WindSpeed)),
            new RecordingRule(new DomainRequirements(Weather: WeatherParams.ShortwaveRadiation), enabled: false));

        Assert.Equal(WeatherParams.Temperature | WeatherParams.WindSpeed, engine.Requirements.Weather);
    }

    [Fact]
    public void A_real_frost_feature_raises_its_alert_after_the_domain_event()
    {
        var engine = Engine(new FrostAlert(Options.Create(new FrostConfig())));

        var events = engine.Process(
            DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start, 48, (_, p) => p with { Temperature = -6.0 })));

        Assert.Equal(2, events.Count);
        Assert.IsType<PipelineEvent.WeatherDataReceived>(events[0]);
        var frost = Assert.IsType<FrostAlertResult>(events[1]);
        Assert.Equal(AlertSeverity.Orange, frost.Severity);
    }

    [Fact]
    public void Data_that_does_not_match_its_domain_is_rejected()
    {
        var engine = Engine();
        var mismatched = new DomainDataReceived(DomainData.Lucerne, DomainKind.Weather, DomainData.Flood(Start));

        var ex = Assert.Throws<ArgumentException>(() => engine.Process(mismatched));

        Assert.Equal("received", ex.ParamName);
    }
}
