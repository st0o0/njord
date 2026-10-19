using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;

namespace Njord.Tests.Shared;

public static class TestConfigView
{
    public static ConfigView Create(NjordOptions? scope = null, PollingOptions? polling = null) =>
        new(
            Options.Create(scope ?? AppSettingsDefaults.Options<NjordOptions>()),
            Options.Create(polling ?? AppSettingsDefaults.Options<PollingOptions>()));
}
