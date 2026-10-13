using Njord.Compute.Settings;

namespace Njord.Messages.Config;

// A null section or value leaves the current setting unchanged.
public sealed record UpdateAnalysisConfig(
    ConsensusUpdate? Consensus,
    AlertUpdate? Alerts,
    bool? DerivedEnabled,
    bool? TrendsEnabled,
    IndexUpdate? Indices);

public sealed record ConsensusUpdate(bool? Enabled, ConsensusMethod? Method, double? TrimPercent);

public sealed record AlertUpdate(
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

public sealed record IndexUpdate(
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
public sealed record UpdateAnalysisConfigCompleted(ConfigSnapshot Config, IReadOnlyList<string> RestartRequired)
    : UpdateAnalysisConfigResponse;
public sealed record UpdateAnalysisConfigRejected(string Reason) : UpdateAnalysisConfigResponse;
public sealed record UpdateAnalysisConfigFailed(Exception Cause) : UpdateAnalysisConfigResponse;
