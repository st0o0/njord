using Njord.Core;
using Njord.Grpc.Mapping;

using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Results.Forecasts;
using Njord.Messages.Results.Scores;
using Njord.Messages.Results.Trends;
using Njord.Tests.Shared;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainConfidenceLevel = Njord.Messages.Alerts.ConfidenceLevel;

namespace Njord.Grpc.Tests.Mapping;

public sealed class EntityStateUpdateMappingTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly DateTimeOffset _updatedAt = TestTimeConstants.EnrichmentEpoch;

    private sealed record UnknownResult(Location Location) : FeatureResult(Location);

    [Fact]
    public void Alert_dispatches()
    {
        var result = new FrostAlertResult(_vienna, DomainAlertSeverity.Yellow, 0.8, -2, 0, null, null, null, DomainConfidenceLevel.High, -3, null);

        var update = result.ToEntityStateUpdate("entity-1", _updatedAt);

        Assert.Multiple(
            () => Assert.Equal("entity-1", update.EntityId),
            () => Assert.Equal(_updatedAt, update.UpdatedAt.ToDateTimeOffset()),
            () => Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Alert, update.ValueCase));
    }

    [Fact]
    public void Score_dispatches()
    {
        var result = new BbqScoreResult(_vienna, 80, _updatedAt, 3);

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Score, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Condition_dispatches()
    {
        var result = new VentilationWindowResult(_vienna, _updatedAt, _updatedAt.AddHours(1));

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Condition, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Trend_dispatches()
    {
        var result = new ConfidenceTrendResult(_vienna, ConfidenceTrendDirection.Converging, 0.5, 1.0, 0.5);

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Trend, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Forecast_dispatches()
    {
        var result = new ForecastModelComparisonResult(_vienna, []);

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Forecast, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Weather_dispatches()
    {
        var result = new WeatherResult(_vienna, 18, 17, 60, 2, 270, 1015, "clear", []);

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Weather, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Observation_dispatches()
    {
        var result = new ObservationResult(_vienna, "TestObservation", 21.5);

        Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Observation, result.ToEntityStateUpdate("e", _updatedAt).ValueCase);
    }

    [Fact]
    public void Unknown_result_throws()
    {
        var result = new UnknownResult(_vienna);

        Assert.Throws<ArgumentOutOfRangeException>(() => result.ToEntityStateUpdate("e", _updatedAt));
    }
}
