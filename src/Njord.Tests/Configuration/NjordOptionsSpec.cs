using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class NjordOptionsSpec
{
    [Fact(Timeout = 5000)]
    public void Without_an_override_the_budget_is_the_open_meteo_free_tier()
    {
        var options = new NjordOptions();

        Assert.Equal(300_000, options.EffectiveBudget.RequestsPerMonth);
        Assert.Equal(600, options.EffectiveBudget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void An_explicit_override_supersedes_the_default()
    {
        var overrideBudget = new RequestBudget(50_000, 60);

        var options = new NjordOptions { BudgetOverride = overrideBudget };

        Assert.Equal(overrideBudget, options.EffectiveBudget);
    }

    [Fact(Timeout = 5000)]
    public void Mqtt_defaults_are_broker_friendly()
    {
        var options = new NjordOptions();

        Assert.Equal(string.Empty, options.Mqtt.Host);
        Assert.Equal(1883, options.Mqtt.Port);
        Assert.Equal("homeassistant", options.Mqtt.DiscoveryPrefix);
        Assert.Equal("njord", options.Mqtt.BaseTopic);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_default_to_the_six_step_ladder()
    {
        Assert.Equal([3, 6, 12, 24, 48, 72], new NjordOptions().Horizons);
    }
}
