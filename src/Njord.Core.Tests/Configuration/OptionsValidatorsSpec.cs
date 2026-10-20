using Njord.Core.Configuration.Health;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.Persistence;

namespace Njord.Core.Tests.Configuration;

public sealed class OptionsValidatorsSpec
{
    [Fact]
    public void Should_reject_a_poll_overdue_factor_of_one_or_less()
    {
        var result = new HealthOptionsValidator().Validate(null, new HealthOptions { PollOverdueFactor = 1 });

        Assert.Contains("PollOverdueFactor must be greater than 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_stalled_factor_that_does_not_exceed_the_overdue_factor()
    {
        var options = new HealthOptions { PollOverdueFactor = 2, PollStalledFactor = 2 };

        var result = new HealthOptionsValidator().Validate(null, options);

        Assert.Contains("PollStalledFactor must be greater than PollOverdueFactor", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Should_reject_a_budget_degraded_percentage_on_the_boundary(double percent)
    {
        var result = new HealthOptionsValidator().Validate(null, new HealthOptions { BudgetDegradedBelowPercent = percent });

        Assert.Contains("BudgetDegradedBelowPercent", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_enabled_mqtt_without_a_host()
    {
        var result = new MqttOptionsValidator().Validate(null, new MqttOptions { Enabled = true });

        Assert.Equal("The MQTT broker host is missing: set Njord:Mqtt:Host.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_accept_disabled_mqtt_without_a_host()
    {
        Assert.True(new MqttOptionsValidator().Validate(null, new MqttOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void Should_reject_an_mqtt_port_outside_the_valid_range(int port)
    {
        var result = new MqttOptionsValidator().Validate(null, new MqttOptions { Port = port });

        Assert.Contains($"got {port}", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_postgres_persistence_without_a_connection_string()
    {
        var options = new PersistenceOptions { Provider = PersistenceProvider.PostgreSql };

        var result = new PersistenceOptionsValidator().Validate(null, options);

        Assert.Contains("requires a connection string", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_default_the_persistence_data_path()
    {
        Assert.Equal("data/njord.db", new PersistenceOptions().DataPath);
    }

    [Fact]
    public void Should_reject_an_unwritable_sqlite_data_directory()
    {
        var blocker = Path.GetTempFileName();
        try
        {
            var options = new PersistenceOptions { DataPath = Path.Combine(blocker, "njord.db") };

            var result = new PersistenceOptionsValidator().Validate(null, options);

            Assert.Contains("is not writable", ValidationAssert.SingleFailure(result));
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
