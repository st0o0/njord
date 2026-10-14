using Akka.Actor;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Consensus;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.OpenMeteo;
using Njord.Tests.Shared;
using Njord.Weather;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;

namespace Njord.Pipeline.Tests;

// The seam this change exists for: a restarting gateway must not rebuild the pipeline, so its analysis state
// and every consumer's SourceRef survive.
public sealed class GatewayRestartSpec : NjordActorSpec
{
    private const string Location = "lucerne";

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeOpenMeteoClient _client = new();
    private readonly FaultyOnceBudgetProvider _budget = new();

    protected override bool UseTestPersistence => false;

    private static NjordOptions Options => new()
    {
        Locations = [new LocationOptions { Name = Location, Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_gateway_restart_keeps_the_consumer_stream_and_the_previous_consensus_for_trends()
    {
        var ct = TestContext.Current.CancellationToken;
        ActorRegistry.Register<IBudgetTracker>(Sys.ActorOf(Props.Create(() => new SilentActor()), "budget"), overwrite: true);
        var pipeline = StartPipeline();
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var gateway = StartGateway();

        await FetchUntilCompletedAsync(gateway, ct);
        await consumer.ExpectMsgAsync<PipelineEvent.ForecastReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await consumer.ExpectMsgAsync<PipelineEvent.ConsensusComputed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        // The budget read fault fails the gateway's private stream; the gateway fails and is restarted.
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        var stopped = await gateway.Ask<FetchForecastResponse>(new FetchForecast(new LocationId(Location), "icon_d2"),
            TestTimeouts.RealTimeWaitMax, ct);
        Assert.Equal(PollFailureReason.Transport, Assert.IsType<FetchForecastFailed>(stopped).Reason);

        await FetchUntilCompletedAsync(gateway, ct);

        var next = await consumer.FishForMessageAsync(
            m => m is PipelineEvent.TrendComputed or ConsumerCompleted or ConsumerFailed,
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var trend = Assert.IsType<PipelineEvent.TrendComputed>(next);
        Assert.Equal(Location, trend.Result.Location.Name);
    }

    // Until the restarted gateway has re-attached it rejects with PipelineUnavailable; it is asked again then.
    private async Task FetchUntilCompletedAsync(IActorRef gateway, CancellationToken ct) =>
        await AwaitAssertAsync(async () =>
        {
            var reply = await gateway.Ask<FetchForecastResponse>(new FetchForecast(new LocationId(Location), "icon_d2"),
                TestTimeouts.RealTimeWaitMax, ct);
            Assert.IsType<FetchForecastCompleted>(reply);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(200), cancellationToken: ct);

    private IActorRef StartGateway()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        return Sys.ActorOf(Props.Create(() => new OpenMeteoGateway(_client, _time, options, parameters, _budget, FetchTimeouts.Default)),
            $"gateway-{Guid.NewGuid():N}");
    }

    private IActorRef StartPipeline()
    {
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var settings = AnalysisSettingsFactory.Create(Options, new ConsensusOptions(), new AlertOptions(), new IndexOptions(),
            new TrendOptions { Enabled = true }, new DerivedOptions());
        return Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            _time,
            new ConsensusBuilder(parameters, _time),
            Microsoft.Extensions.Options.Options.Create(new ConsensusOptions()),
            DisabledAnalyzers.Alert(),
            DisabledAnalyzers.Derived(),
            new TrendAnalyzer(Microsoft.Extensions.Options.Options.Create(new TrendOptions { Enabled = true })),
            DisabledAnalyzers.Index(),
            settings)), $"pipeline-{Guid.NewGuid():N}");
    }

    private sealed class FaultyOnceBudgetProvider : IBudgetProvider
    {
        public volatile bool ThrowOnNextRead;

        public BudgetRate GetCurrentRate()
        {
            if (ThrowOnNextRead)
            {
                ThrowOnNextRead = false;
                throw new InvalidOperationException("simulated budget read fault");
            }

            return new BudgetRate(480, 1_000_000);
        }
    }

    private sealed class SilentActor : ReceiveActor
    {
        public SilentActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
