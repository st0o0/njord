using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Derived.Configuration;
using Njord.Indices;
using Njord.Indices.Configuration;
using Njord.Messages.Alerts;
using Njord.Trends.Configuration;
using Njord.Weather;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;

namespace Njord.Tests.Shared;

public static class DisabledAnalyzers
{
    private static readonly ResolvedParameterSet EmptyParameters =
        ParameterSelection.Resolve(["Weather"], [], []);

    public static AlertAnalyzer Alert() => FeatureServices.Alerts(TimeProvider.System, enabled: false);

    public static DerivedAnalyzer Derived() =>
        new(Options.Create(AppSettingsDefaults.Options<DerivedOptions>(o => o.Enabled = false)));

    public static TrendAnalyzer Trend() =>
        new(Options.Create(AppSettingsDefaults.Options<TrendOptions>(o => o.Enabled = false)));

    public static IndexAnalyzer Index() => FeatureServices.Indices(TimeProvider.System, enabled: false);

    public static ConsensusBuilder Consensus() =>
        new(EmptyParameters, TimeProvider.System);
}
