using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Budget.Configuration;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetGaugeRegistrationSpec
{
    [Fact]
    public void The_meter_name_constant_matches_the_budget_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.Budget", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.Meter.Name));
    }

    [Fact]
    public void The_container_registers_the_gauge_registration_as_a_hosted_service()
    {
        var services = new ServiceCollection();

        new BudgetSetupContainer().SetupServices(services, new ConfigurationBuilder().Build());

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(BudgetGaugeRegistration));
    }
}
