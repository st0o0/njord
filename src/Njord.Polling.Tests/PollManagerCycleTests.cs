using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// The data-change counter comes from the workers' reports.
public sealed class PollManagerCycleTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);
    private readonly string _location = $"cycle{Guid.NewGuid():N}";
    private readonly ConcurrentQueue<(string Instrument, double Value)> _measurements = new();
    private readonly IOptions<NjordOptions> _njordOptions;
    private readonly IOptions<PollingOptions> _pollingOptions =
        Options.Create(AppSettingsDefaults.Options<PollingOptions>(o => o.DiscoveryInterval = TimeSpan.FromHours(1)));
    private IActorRef _manager = null!;

    public PollManagerCycleTests() =>
        _njordOptions = Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = _location, Latitude = 47.05, Longitude = 8.31 }],
        });

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((system, registry) =>
        {
            var region = CreateTestProbe("region");
            region.IgnoreMessages(_ => true);
            registry.Register<IPollRegion>(region.Ref);
            _manager = system.ActorOf(Props.Create(() => new PollManager(_njordOptions, _pollingOptions, _time)), "manager");
        });
    }

    private MeterListener ListenToPollingMeter()
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Njord.Polling")
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

    private void Report(PollWorkerOutcome outcome)
    {
        var target = new PollTarget(new Location(_location, 0, 0));
        _manager.Tell(new PollWorkerReport(target, outcome,
            new PollStateEntry(target.Location, PollPhase.Discovery, _time.GetUtcNow(), null, 0, null)));
    }

    private double[] Values(string instrument) =>
        [.. _measurements.Where(m => m.Instrument == instrument).Select(m => m.Value)];

    [Fact(Timeout = 30_000)]
    public async Task Only_changed_reports_count_as_data_changes()
    {
        using var listener = ListenToPollingMeter();
        Report(PollWorkerOutcome.Planned);
        Report(PollWorkerOutcome.Changed);
        Report(PollWorkerOutcome.Unchanged);
        Report(PollWorkerOutcome.Failed);
        Report(PollWorkerOutcome.Changed);

        await AwaitAssertAsync(
            () => Assert.Equal([1.0, 1.0], Values("njord_pipeline_data_changed_total")),
            TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
    }
}
