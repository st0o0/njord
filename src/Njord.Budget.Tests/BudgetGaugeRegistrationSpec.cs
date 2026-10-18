using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Budget.Configuration;
using Njord.Budget.Health;
using Njord.Core.Configuration.Polling;
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

    [Fact(Timeout = 15_000)]
    public async Task Gauges_follow_the_usage_and_the_configured_budget()
    {
        var polling = new TestOptionsMonitor<PollingOptions>(AppSettingsDefaults.Options<PollingOptions>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestConfigView.Create(pollingMonitor: polling));
        new BudgetSetupContainer().SetupServices(services, new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var hosted = Assert.Single(provider.GetServices<IHostedService>().OfType<BudgetGaugeRegistration>());
        await hosted.StartAsync(TestContext.Current.CancellationToken);

        var measurements = new Dictionary<string, long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => measurements[instrument.Name] = value);
        listener.Start();

        listener.RecordObservableInstruments();
        var initial = new Dictionary<string, long>(measurements);

        provider.GetRequiredService<BudgetUsageState>().Update(5, 50);
        polling.Update(AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(250_000, 60)));
        listener.RecordObservableInstruments();

        Assert.Multiple(
            () => Assert.Equal(0, initial["njord_budget_used_daily"]),
            () => Assert.Equal(0, initial["njord_budget_used_monthly"]),
            () => Assert.Equal(10_000, initial["njord_budget_limit_daily"]),
            () => Assert.Equal(300_000, initial["njord_budget_limit_monthly"]),
            () => Assert.Equal(5, measurements["njord_budget_used_daily"]),
            () => Assert.Equal(50, measurements["njord_budget_used_monthly"]),
            () => Assert.Equal(10_000, measurements["njord_budget_limit_daily"]),
            () => Assert.Equal(250_000, measurements["njord_budget_limit_monthly"]));
    }
}
