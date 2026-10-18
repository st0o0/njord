namespace Njord.Messages.Config;

// Null or empty values leave the current setting unchanged.
public sealed record UpdateSettings(
    int? ForecastDays,
    IReadOnlyList<int> Horizons,
    IReadOnlyList<string> DefaultModels,
    ParameterScope? Parameters,
    TimeSpan? PollInterval);

public abstract record UpdateSettingsResponse;
public sealed record UpdateSettingsCompleted(EffectiveConfig Config, IReadOnlyList<string> RestartRequired)
    : UpdateSettingsResponse;
public sealed record UpdateSettingsRejected(string Reason) : UpdateSettingsResponse;
public sealed record UpdateSettingsFailed(Exception Cause) : UpdateSettingsResponse;
