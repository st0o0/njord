namespace Njord.Messages.Config;

public sealed record QueryAnalysisSettings;

public abstract record AnalysisSettingsResponse;
public sealed record AnalysisSettingsResult(AnalysisSettings Settings) : AnalysisSettingsResponse;
