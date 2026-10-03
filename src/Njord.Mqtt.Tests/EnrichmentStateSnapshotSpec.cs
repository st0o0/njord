using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using static Njord.Mqtt.Tests.EnrichmentGoldenMasterFixtures;
using static VerifyXunit.Verifier;

namespace Njord.Mqtt.Tests;

public sealed class EnrichmentStateSnapshotSpec
{
    private static Task VerifyState(string typeName, object result) =>
        Verify(Render(Presenter(typeName).ToStateMessages(result, BaseTopic, Location)));

    [Fact]
    public Task Alerts_state_messages_match_golden_master()
    {
        var result = new AlertResult(Location,
        [
            new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75,
                new Dictionary<string, object?> { ["expected_low"] = -2.1 }),
            Alert.None(AlertType.Heat),
        ]);

        return VerifyState("alerts", result);
    }

    [Fact]
    public Task Derived_state_messages_match_golden_master()
    {
        var result = new DerivedResultComputer(Parameters).Compute(FullConsensus(), [3, 24]);

        return VerifyState("derived", result);
    }

    [Fact]
    public Task Trends_state_messages_match_golden_master()
    {
        var result = new TrendComputer().Compute(FullConsensus(), PreviousConsensus());

        return VerifyState("trends", result);
    }

    [Fact]
    public Task Indices_state_messages_match_golden_master()
    {
        var prefs = PreferenceResolver.Resolve(Options().Enrichment.Indices, [Location]);
        var result = new IndexComputer(Parameters, Time).Compute(FullConsensus(), prefs);

        return VerifyState("indices", result);
    }

    [Fact]
    public Task History_state_messages_match_golden_master()
    {
        var history = new ForecastHistory(30);
        for (var day = 1; day <= 10; day++)
        {
            history.Add(new ForecastRecord(
                T0.AddDays(-day), Location,
                new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>
                {
                    [new WeatherModel("icon_d2")] = new Dictionary<string, double?> { ["temperature_2m"] = 20.0 + day },
                    [new WeatherModel("ecmwf_ifs025")] = new Dictionary<string, double?> { ["temperature_2m"] = 21.0 + day },
                },
                new Dictionary<string, double?> { ["temperature_2m"] = 20.5 + day }));
        }

        var result = new HistoryComputer().Compute(
            history, ModelSnapshot.Empty, Location, Parameters, Time, Options().Enrichment.History);

        return VerifyState("history", result);
    }
}
