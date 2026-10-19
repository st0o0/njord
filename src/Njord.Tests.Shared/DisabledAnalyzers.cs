using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Consensus;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Derived.Configuration;
using Njord.Indices;
using Njord.Model;
using Njord.Trends.Configuration;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;

namespace Njord.Tests.Shared;

public static class DisabledAnalyzers
{
    private static readonly ResolvedParameterSet EmptyParameters =
        ParameterSelection.Resolve(["Weather"], [], []);

    public static AlertAnalyzer Alert(TimeProvider? time = null) =>
        FeatureServices.Alerts(time ?? TestTimeouts.AtEpoch(), enabled: false);

    public static DerivedAnalyzer Derived() =>
        new(Options.Create(AppSettingsDefaults.Options<DerivedOptions>(o => o.Enabled = false)));

    public static TrendAnalyzer Trend() =>
        new(Options.Create(AppSettingsDefaults.Options<TrendOptions>(o => o.Enabled = false)));

    public static IndexAnalyzer Index(TimeProvider? time = null) =>
        FeatureServices.Indices(time ?? TestTimeouts.AtEpoch(), enabled: false);

    public static ConsensusBuilder Consensus(TimeProvider? time = null) =>
        new(EmptyParameters, time ?? TestTimeouts.AtEpoch());
}
