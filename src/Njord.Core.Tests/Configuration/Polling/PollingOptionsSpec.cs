using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class PollingOptionsSpec
{
    [Fact]
    public void The_poll_interval_environment_variable_still_binds()
    {
        var prefix = $"NJORD_SPEC_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable($"{prefix}Njord__Polling__PollInterval", "00:30:00");
        Environment.SetEnvironmentVariable($"{prefix}Njord__Polling__DiscoveryInterval", "00:10:00");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration);
            services.AddOptions<PollingOptions>().BindConfiguration(PollingOptions.SectionName);

            using var provider = services.BuildServiceProvider();
            var polling = provider.GetRequiredService<IOptions<PollingOptions>>().Value;

            Assert.Equal(TimeSpan.FromMinutes(30), polling.PollInterval);
            Assert.Equal(TimeSpan.FromMinutes(10), polling.DiscoveryInterval);
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}Njord__Polling__PollInterval", null);
            Environment.SetEnvironmentVariable($"{prefix}Njord__Polling__DiscoveryInterval", null);
        }
    }
}
