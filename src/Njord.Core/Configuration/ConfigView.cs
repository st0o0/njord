using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;

namespace Njord.Core.Configuration;

// The forecast scope and polling configuration fixed at startup, read by request handlers outside actors.
public sealed class ConfigView(
    IOptions<NjordOptions> scope,
    IOptions<PollingOptions> polling)
{
    public NjordOptions Scope => scope.Value;
    public PollingOptions Polling => polling.Value;
}
