using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Messages.Common;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerCycleSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly string _location = $"cycle{Guid.NewGuid():N}";
    private readonly ConcurrentQueue<(string Instrument, double Value)> _measurements = new();

    private MeterListener ListenToPipelineMeter()
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Njord.Pipeline")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "location" && Equals(tag.Value, _location))
                {
                    _measurements.Enqueue((instrument.Name, value));
                }
            }
        });
        listener.Start();
        return listener;
    }

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(FetchTimeouts.Default);
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = _location, Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "ecmwf_ifs025", "gfs_seamless"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(new PollingOptions { DiscoveryInterval = TimeSpan.FromHours(1) }));
        services.AddSingleton(new NjordHealthMonitor { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
                registry.Register<IForecastGateway>(system.ActorOf(Props.Create(() => new SilentGatewayActor()))))
            .WithActors((system, registry, resolver) =>
                registry.Register<IPollScheduler>(system.ActorOf(resolver.Props<PollScheduler>(), "scheduler")));
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    private void Completed(string model, int hash) =>
        Scheduler.Tell(new FetchForecastCompleted(new LocationId(_location), model, hash));

    private void Failed(string model) =>
        Scheduler.Tell(new FetchForecastFailed(new LocationId(_location), model, PollFailureReason.Transport, "simulated"));

    private double[] Values(string instrument) =>
        [.. _measurements.Where(m => m.Instrument == instrument).Select(m => m.Value)];

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_cycle_with_one_failed_model_completes_and_records_the_failure()
    {
        using var listener = ListenToPipelineMeter();
        Completed("icon_d2", 1);
        Completed("ecmwf_ifs025", 2);
        Failed("gfs_seamless");

        await AwaitAssertAsync(() =>
        {
            Assert.Equal([3.0], Values("njord_poll_cycle_models"));
            Assert.Equal([1.0], Values("njord_poll_cycle_failed_models"));
            Assert.Single(Values("njord_poll_cycle_duration_seconds"));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_rejected_model_counts_as_failed_in_its_cycle()
    {
        using var listener = ListenToPipelineMeter();
        Completed("icon_d2", 1);
        Completed("ecmwf_ifs025", 2);
        Scheduler.Tell(new FetchForecastRejected(new LocationId(_location), "gfs_seamless", FetchRejectionReason.QueueFull));

        await AwaitAssertAsync(() =>
        {
            Assert.Equal([3.0], Values("njord_poll_cycle_models"));
            Assert.Equal([1.0], Values("njord_poll_cycle_failed_models"));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Consecutive_rounds_with_a_failure_complete_as_separate_cycles()
    {
        using var listener = ListenToPipelineMeter();
        Completed("icon_d2", 1);
        Completed("ecmwf_ifs025", 2);
        Failed("gfs_seamless");
        Completed("icon_d2", 1);
        Completed("ecmwf_ifs025", 2);
        Completed("gfs_seamless", 3);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal([3.0, 3.0], Values("njord_poll_cycle_models"));
            Assert.Equal([1.0, 0.0], Values("njord_poll_cycle_failed_models"));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class SilentGatewayActor : ReceiveActor
    {
        public SilentGatewayActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
