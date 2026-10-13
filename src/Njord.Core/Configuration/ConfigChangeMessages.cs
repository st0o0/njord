using Njord.Compute.Settings;

namespace Njord.Core.Configuration;

public sealed record AnalysisSettingsChanged(AnalysisSettings Settings);

public sealed record PollingChanged(TimeSpan PollInterval, TimeSpan DiscoveryInterval);
