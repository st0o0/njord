using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Messages.Config;

// The persisted configuration as one immutable contract tree, built by the configuration manager.
public sealed record ConfigSnapshot(
    ForecastScope Scope,
    PollingSection Polling,
    BudgetProjection BudgetProjection,
    AnalysisSections Analysis);

public sealed record ForecastScope(
    IReadOnlyList<LocationEntry> Locations,
    IReadOnlyList<string> Models,
    IReadOnlyList<int> Horizons,
    int ForecastDays,
    ParameterSelection Parameters);

// Models are the location's own models; ResolvedModels are the default models plus the location's own.
public sealed record LocationEntry(
    LocationId Id,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> ResolvedModels);

public sealed record ParameterSelection(
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Extra,
    IReadOnlyList<string> Exclude);

public sealed record PollingSection(
    TimeSpan PollInterval,
    TimeSpan DiscoveryInterval,
    BudgetLimits? BudgetOverride,
    BudgetLimits EffectiveBudget,
    int DailyLimit);

public sealed record BudgetLimits(int RequestsPerMonth, int RequestsPerMinute);

public sealed record BudgetProjection(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings);

public sealed record AnalysisSections(
    ConsensusSection Consensus,
    AlertSection Alerts,
    DerivedSection Derived,
    TrendSection Trends,
    IndexSection Indices);

public sealed record ConsensusSection(bool Enabled, ConsensusMethod Method, double TrimPercent, int MinQuorum);

public sealed record AlertSection(
    bool Enabled,
    IReadOnlyList<double> FrostThresholds,
    IReadOnlyList<double> HeatThresholds,
    IReadOnlyList<double> StormGustThresholds,
    double HeavyRainHourlyThreshold,
    double HeavyRainDailyThreshold,
    double PressureDropThreshold,
    double PressureDropSevereThreshold,
    int FogPersistentHours,
    double CapeThreshold,
    double ThunderstormPrecipThreshold,
    double ThunderstormGustThreshold,
    double IceThreshold,
    IReadOnlyList<double> WindChillThresholds,
    IReadOnlyList<double> VisibilityThresholds,
    IReadOnlyList<double> TropicalNightThresholds,
    IReadOnlyList<double> HumidityThresholds);

public sealed record DerivedSection(bool Enabled);

public sealed record TrendSection(bool Enabled);

public sealed record IndexSection(
    bool Enabled,
    IndexPreferenceValues Preferences,
    IReadOnlyDictionary<string, IndexPreferenceValues> ScoreOverrides,
    IReadOnlyList<LocationIndexPreferences> LocationOverrides);

public sealed record IndexPreferenceValues(
    double? IdealOutdoorTemp,
    double? RunningIdealTempLow,
    double? RunningIdealTempHigh,
    double? BbqMinTemp,
    double? BbqIdealWindLow,
    double? BbqIdealWindHigh,
    double? IndoorTemp,
    double? HeatSensitivity,
    double? HumiditySensitivity,
    double? WindSensitivity,
    double? RainSensitivity);

public sealed record LocationIndexPreferences(
    string Location,
    IndexPreferenceValues Preferences,
    IReadOnlyDictionary<string, IndexPreferenceValues> ScoreOverrides);
