using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Derived;
using Njord.Indices;
using Njord.Weather;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;

namespace Njord.Tests.Shared;

public static class DisabledAnalyzers
{
    private static readonly ResolvedParameterSet EmptyParameters =
        ParameterRegistry.Resolve(["Weather"], [], []);

    public static AlertAnalyzer Alert() =>
        new(Options.Create(new AlertOptions { Enabled = false }), TimeProvider.System);

    public static DerivedAnalyzer Derived() =>
        new(new DerivedResultComputer(EmptyParameters), [], enabled: false);

    public static TrendAnalyzer Trend() =>
        new(new TrendComputer(), enabled: false);

    public static IndexAnalyzer Index() =>
        new(new IndexComputer(EmptyParameters, TimeProvider.System),
            new IndexOptions(), [], enabled: false);

    public static ConsensusSnapshotFactory Consensus() =>
        new(EmptyParameters, TimeProvider.System);
}
