using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class PollingOptionsSpec
{
    [Fact]
    public void Poll_interval_defaults_to_sixty_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(60), AppSettingsDefaults.Options<PollingOptions>().PollInterval);
    }

    [Fact]
    public void Without_an_override_the_budget_is_the_open_meteo_free_tier()
    {
        var budget = BudgetCalculator.GetEffectiveBudget(AppSettingsDefaults.Options<PollingOptions>());

        Assert.Equal(300_000, budget.RequestsPerMonth);
        Assert.Equal(600, budget.RequestsPerMinute);
    }

    [Fact]
    public void An_explicit_override_supersedes_the_default()
    {
        var overrideBudget = new RequestBudget(50_000, 60);

        Assert.Equal(overrideBudget, BudgetCalculator.GetEffectiveBudget(AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = overrideBudget)));
    }

    [Fact]
    public void The_poll_interval_environment_variable_still_binds()
    {
        var prefix = $"NJORD_SPEC_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable($"{prefix}Njord__PollInterval", "00:30:00");
        Environment.SetEnvironmentVariable($"{prefix}Njord__DiscoveryInterval", "00:10:00");
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
            Environment.SetEnvironmentVariable($"{prefix}Njord__PollInterval", null);
            Environment.SetEnvironmentVariable($"{prefix}Njord__DiscoveryInterval", null);
        }
    }
}
