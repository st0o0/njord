using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
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
        new(new DerivedResultComputer(EmptyParameters),
            Options.Create(new DerivedOptions { Enabled = false }));

    public static TrendAnalyzer Trend() =>
        new(new TrendComputer(), Options.Create(new TrendOptions { Enabled = false }));

    public static IndexAnalyzer Index() =>
        new(new IndexComputer(EmptyParameters, TimeProvider.System),
            Options.Create(new IndexOptions { Enabled = false }));

    public static ConsensusBuilder Consensus() =>
        new(EmptyParameters, TimeProvider.System);
}
