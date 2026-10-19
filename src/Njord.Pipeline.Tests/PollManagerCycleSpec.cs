using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// Poll-cycle summaries and the data-change counter come from the workers' reports.
public sealed class PollManagerCycleSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly string _location = $"cycle{Guid.NewGuid():N}";
    private readonly ConcurrentQueue<(string Instrument, double Value)> _measurements = new();
    private IActorRef _manager = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = _location, Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "ecmwf_ifs025", "gfs_seamless"],
        }));
        services.AddSingleton(TestConfigView.Create(polling: AppSettingsDefaults.Options<PollingOptions>(o => o.DiscoveryInterval = TimeSpan.FromHours(1))));
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            var region = CreateTestProbe("region");
            region.IgnoreMessages(_ => true);
            registry.Register<IPollRegion>(region.Ref);
            _manager = system.ActorOf(DependencyResolver.For(system).Props<PollManager>(), "manager");
        });

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
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "location" && Equals(tag.Value, _location))
            {
                _measurements.Enqueue((instrument.Name, value));
            }
        }
    }

    private void Report(string model, PollWorkerOutcome outcome)
    {
        var target = new PollTarget(new LocationId(_location), model);
        _manager.Tell(new PollWorkerReport(target, outcome,
            new PollStateEntry(target.Location, model, PollPhase.Discovery, _time.GetUtcNow(), null, 0, null)));
    }

    private double[] Values(string instrument) =>
        [.. _measurements.Where(m => m.Instrument == instrument).Select(m => m.Value)];

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_cycle_with_one_failed_model_completes_and_records_the_failure()
    {
        using var listener = ListenToPipelineMeter();
        Report("icon_d2", PollWorkerOutcome.Changed);
        Report("ecmwf_ifs025", PollWorkerOutcome.Changed);
        Report("gfs_seamless", PollWorkerOutcome.Failed);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal([3.0], Values("njord_poll_cycle_models"));
            Assert.Equal([1.0], Values("njord_poll_cycle_failed_models"));
            Assert.Single(Values("njord_poll_cycle_duration_seconds"));
            Assert.Equal([1.0, 1.0], Values("njord_pipeline_data_changed_total"));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Consecutive_rounds_with_a_failure_complete_as_separate_cycles()
    {
        using var listener = ListenToPipelineMeter();
        Report("icon_d2", PollWorkerOutcome.Changed);
        Report("ecmwf_ifs025", PollWorkerOutcome.Unchanged);
        Report("gfs_seamless", PollWorkerOutcome.Failed);
        Report("icon_d2", PollWorkerOutcome.Unchanged);
        Report("ecmwf_ifs025", PollWorkerOutcome.Unchanged);
        Report("gfs_seamless", PollWorkerOutcome.Changed);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal([3.0, 3.0], Values("njord_poll_cycle_models"));
            Assert.Equal([1.0, 0.0], Values("njord_poll_cycle_failed_models"));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Planned_reports_count_as_no_outcome()
    {
        using var listener = ListenToPipelineMeter();
        Report("icon_d2", PollWorkerOutcome.Planned);
        Report("ecmwf_ifs025", PollWorkerOutcome.Planned);
        Report("gfs_seamless", PollWorkerOutcome.Planned);
        Report("icon_d2", PollWorkerOutcome.Changed);
        Report("ecmwf_ifs025", PollWorkerOutcome.Changed);

        await _manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        Assert.Empty(Values("njord_poll_cycle_models"));

        Report("gfs_seamless", PollWorkerOutcome.Unchanged);
        await AwaitAssertAsync(() => Assert.Equal([3.0], Values("njord_poll_cycle_models")),
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }
}
