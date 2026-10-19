using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;
using Njord.Messages.Forecast;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewaySpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeOpenMeteoClient _client = new();
    // A burst large enough that the frozen fake clock never exhausts the budget.
    private readonly SwitchableBudgetProvider _budget = new() { MaxBurst = 1_000_000 };
    private Akka.TestKit.TestProbe _attached = null!;
    private Akka.TestKit.TestProbe _forecasts = null!;
    private Akka.TestKit.TestProbe _apiCalls = null!;

    protected override bool UseTestPersistence => false;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            _attached = CreateTestProbe();
            _forecasts = CreateTestProbe();
            _apiCalls = CreateTestProbe();
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new ForwardingActor(_apiCalls.Ref)), "budget"));
            registry.Register<IForecastPipeline>(system.ActorOf(
                Props.Create(() => new FakePipeline(_attached.Ref, _forecasts.Ref)), "pipeline"));
            var options = Options.Create(AppSettingsDefaults.Options<NjordOptions>(o =>
            {
                o.Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }];
            }));
            var parameters = ParameterSelection.Resolve(["Weather"], [], []);
            registry.Register<IForecastGateway>(system.ActorOf(
                Props.Create(() => new OpenMeteoGateway(_client, _time, options, parameters, _budget, FetchTimeouts.Default)),
                "gateway"));
        });
    }

    private IActorRef Gateway => GetActor<IForecastGateway>();

    private static FetchDomain Fetch(string location = "lucerne", DomainKind domain = DomainKind.Weather) =>
        new(new Location(location, 0, 0), domain);

    private async Task AttachedAsync() =>
        await _attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_throwing_fetch_is_answered_with_a_transport_failure()
    {
        await AttachedAsync();
        _client.Throws = true;
        Gateway.Tell(Fetch());

        var failed = await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", failed.Location.Name),
            () => Assert.Equal(DomainKind.Weather, failed.Domain),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason),
            () => Assert.Contains(nameof(InvalidOperationException), failed.Detail));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failure_result_from_the_client_is_answered_with_its_reason()
    {
        await AttachedAsync();
        _client.EnsembleFails = true;
        Gateway.Tell(Fetch());

        var failed = await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason),
            () => Assert.Equal("simulated ensemble failure", failed.Detail));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_next_fetch_completes_after_a_throwing_fetch()
    {
        await AttachedAsync();
        _client.Throws = true;
        Gateway.Tell(Fetch());
        await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        _client.Throws = false;
        Gateway.Tell(Fetch());

        var completed = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(DomainKind.Weather, completed.Domain);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_successful_domain_fetch_reaches_the_pipeline_input_and_the_requester()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch());

        var completed = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        var received = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, completed.Domain),
            () => Assert.Equal(received.Data.GetHashCode(), completed.Hash),
            () => Assert.Equal("lucerne", received.Location.Name),
            () => Assert.Equal(47.05, received.Location.Latitude),
            () => Assert.Equal(DomainKind.Weather, received.Domain),
            () => Assert.IsType<DomainForecast<WeatherPoint>>(received.Data));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_exhausted_budget_rejects_the_request_instead_of_fetching()
    {
        await AttachedAsync();
        // The bucket holds exactly one request's weight and refills at one cost-unit per minute.
        var weight = OpenMeteoGateway.ComputeWeight(ParameterSelection.Resolve(["Weather"], [], []).HourlyCount, AppSettingsDefaults.Options<NjordOptions>().ForecastDays);
        _budget.CostPerMinute = 1;
        _budget.MaxBurst = weight;
        _time.Advance(TimeSpan.FromSeconds(6));

        Gateway.Tell(Fetch());
        await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Gateway.Tell(Fetch());

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", rejected.Location.Name),
            () => Assert.Equal(DomainKind.Weather, rejected.Domain),
            () => Assert.Equal(FetchRejectionReason.Budget, rejected.Reason),
            () => Assert.Equal(1, _client.Calls));
        var recorded = await _apiCalls.ExpectMsgAsync<RecordApiCall>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(weight, recorded.Weight);
        await _apiCalls.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_domain_without_a_fetch_path_is_rejected_without_a_fetch()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch(domain: DomainKind.Flood));

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, rejected.Domain),
            () => Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason),
            () => Assert.Equal(0, _client.Calls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_in_another_spelling_is_answered_in_that_spelling_and_reaches_the_pipeline_under_the_configured_name()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch("LUCERNE"));
        var completed = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var received = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("LUCERNE", completed.Location.Name),
            () => Assert.Equal("lucerne", received.Location.Name));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Two_requests_for_the_same_target_in_flight_are_both_answered()
    {
        await AttachedAsync();
        _client.Hold();
        Gateway.Tell(Fetch());
        Gateway.Tell(Fetch());
        await AwaitConditionAsync(() => Task.FromResult(_client.Calls == 2), TestTimeouts.AwaitAssertMax,
            cancellationToken: TestContext.Current.CancellationToken);
        _client.Release();

        var first = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        var second = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, first.Domain),
            () => Assert.Equal(DomainKind.Weather, second.Domain));
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unknown_location_fails_fast_without_a_fetch()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch("atlantis"));

        var failed = await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("atlantis", failed.Location.Name),
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failed.Reason),
            () => Assert.Equal(0, _client.Calls));
    }

    private sealed class SwitchableBudgetProvider : IBudgetProvider
    {
        public volatile int MaxBurst = 16;

        public volatile int CostPerMinute = 480;

        public BudgetRate GetCurrentRate() => new(CostPerMinute, MaxBurst);
    }

    private sealed class ForwardingActor : ReceiveActor
    {
        public ForwardingActor(IActorRef target)
        {
            ReceiveAny(target.Tell);
        }
    }

    // Vends a SinkRef per request whose forecasts go to the probe, then reports the attach.
    private sealed class FakePipeline : ReceiveActor
    {
        public FakePipeline(IActorRef attached, IActorRef forecasts)
        {
            var mat = Context.Materializer();
            ReceiveAsync<RequestForecastSink>(async msg =>
            {
                var requester = Sender;
                var sinkRef = await StreamRefs.SinkRef<DomainDataReceived>()
                    .To(Sink.ActorRef<DomainDataReceived>(forecasts, "completed", _ => "failed"))
                    .Run(mat);
                requester.Tell(new RequestForecastSinkCompleted(msg.RequestId, sinkRef), Self);
                attached.Tell("attached");
            });
        }
    }
}
