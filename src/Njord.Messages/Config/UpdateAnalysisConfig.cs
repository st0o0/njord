using Njord.Consensus.Settings;

namespace Njord.Messages.Config;

// A null section or value leaves the current setting unchanged. A reset section loses its override first, so the
// base configuration applies again, and its update (if any) is applied on top. A dry run validates only.
public sealed record UpdateAnalysisConfig(
    ConsensusConfigUpdate? Consensus,
    AlertConfigUpdate? Alerts,
    bool? DerivedEnabled,
    bool? TrendsEnabled,
    IndexConfigUpdate? Indices,
    IReadOnlyList<EnrichmentSection>? Reset = null,
    bool DryRun = false);

public sealed record ConsensusConfigUpdate(bool? Enabled, ConsensusMethod? Method, double? TrimPercent);

public sealed record AlertConfigUpdate(
    bool? Enabled,
    IReadOnlyList<double>? FrostThresholds,
    IReadOnlyList<double>? HeatThresholds,
    IReadOnlyList<double>? StormGustThresholds,
    double? HeavyRainHourlyThreshold,
    double? HeavyRainDailyThreshold,
    double? PressureDropThreshold,
    double? CapeThreshold,
    double? ThunderstormPrecipThreshold,
    double? ThunderstormGustThreshold,
    double? PressureDropSevereThreshold,
    int? FogPersistentHours,
    double? IceThreshold,
    IReadOnlyList<double>? WindChillThresholds,
    IReadOnlyList<double>? VisibilityThresholds,
    IReadOnlyList<double>? TropicalNightThresholds,
    IReadOnlyList<double>? HumidityThresholds);

public sealed record IndexConfigUpdate(
    bool? Enabled,
    double? IndoorTemp,
    double? IdealOutdoorTemp,
    double? HeatSensitivity,
    double? HumiditySensitivity,
    double? WindSensitivity,
    double? RainSensitivity,
    double? RunningIdealTempLow,
    double? RunningIdealTempHigh,
    double? BbqMinTemp,
    double? BbqIdealWindLow,
    double? BbqIdealWindHigh);

public abstract record UpdateAnalysisConfigResponse;
public sealed record UpdateAnalysisConfigCompleted(EffectiveConfig Config, IReadOnlyList<string> RestartRequired)
    : UpdateAnalysisConfigResponse;
public sealed record UpdateAnalysisConfigValidated(EffectiveConfig Config, IReadOnlyList<string> RestartRequired)
    : UpdateAnalysisConfigResponse;
public sealed record UpdateAnalysisConfigRejected(IReadOnlyList<ConfigIssue> Issues, bool DryRun = false)
    : UpdateAnalysisConfigResponse
{
    public string Reason => string.Join("; ", Issues.Select(i => i.Message));
}
public sealed record UpdateAnalysisConfigFailed(Exception Cause) : UpdateAnalysisConfigResponse;
