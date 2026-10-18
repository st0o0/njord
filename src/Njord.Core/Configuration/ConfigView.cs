using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;

namespace Njord.Core.Configuration;

// The persisted forecast scope and polling configuration as they are right now, read per call by request
// handlers outside actors. It never subscribes to changes; the ConfigManager is the only change subscriber.
public sealed class ConfigView(
    IOptionsMonitor<NjordOptions> scope,
    IOptionsMonitor<PollingOptions> polling)
{
    public NjordOptions Scope => scope.CurrentValue;
    public PollingOptions Polling => polling.CurrentValue;
}
