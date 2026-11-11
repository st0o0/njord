using Njord.Core.Domain;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Results;

namespace Njord.Grpc.Tests;

public sealed class DefaultResultFactoryTests
{
    private static FeatureDescriptor Descriptor(OutputKind output, string name = "Test") =>
        new(output, FeatureCategory.Forecast, name, new DomainRequirements());

    [Fact]
    public void Alert_creates_cleared_alert_result()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Alert), "Vienna");

        var alert = Assert.IsType<ClearedAlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0, alert.Confidence),
            () => Assert.Equal(0, alert.TriggerValue),
            () => Assert.Equal(0, alert.Threshold),
            () => Assert.Null(alert.PeakValue),
            () => Assert.Null(alert.HoursUntil),
            () => Assert.Null(alert.DurationHours),
            () => Assert.Equal(ConfidenceLevel.Low, alert.Level));
    }

    [Fact]
    public void Score_creates_default_score_result()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Score), "Vienna");

        var score = Assert.IsType<DefaultScoreResult>(result);
        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(DateTimeOffset.MinValue, score.BestStart),
            () => Assert.Equal(0, score.BestHours));
    }

    [Fact]
    public void Condition_creates_default_condition_result()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Condition), "Vienna");

        var condition = Assert.IsType<DefaultConditionResult>(result);
        Assert.Equal("Vienna", condition.Location.Name);
    }

    [Fact]
    public void Default_condition_result_maps_to_inactive()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Condition), "Vienna");
        var condition = Assert.IsType<DefaultConditionResult>(result);

        Assert.False(condition.ToConditionValue().Active);
    }

    [Fact]
    public void Observation_creates_result_with_feature_name_and_zero_value()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Observation, "Pm25"), "Vienna");

        var observation = Assert.IsType<ObservationResult>(result);
        Assert.Multiple(
            () => Assert.Equal("Pm25", observation.FeatureName),
            () => Assert.Equal(0, observation.Value));
    }

    [Theory]
    [InlineData(OutputKind.Trend)]
    [InlineData(OutputKind.Forecast)]
    [InlineData(OutputKind.Weather)]
    public void Non_defaultable_output_kinds_return_null(OutputKind kind) =>
        Assert.Null(DefaultResultFactory.Create(Descriptor(kind), "Vienna"));

    [Fact]
    public void Created_result_location_has_zero_coordinates()
    {
        var result = DefaultResultFactory.Create(Descriptor(OutputKind.Alert), "Vienna");

        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal("Vienna", result.Location.Name),
            () => Assert.Equal(0, result.Location.Latitude),
            () => Assert.Equal(0, result.Location.Longitude));
    }
}
