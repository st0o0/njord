using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;
using Njord.Messages.Forecast;
using Njord.Tests.Shared;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewaySpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeOpenMeteoClient _client = new() { ThrowingModels = { "broken" } };
    // A burst large enough that the frozen fake clock never parks a request in the throttle.
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
                o.Models = ["icon_d2", "broken"];
            }));
            var parameters = ParameterSelection.Resolve(["Weather"], [], []);
            registry.Register<IForecastGateway>(system.ActorOf(
                Props.Create(() => new OpenMeteoGateway(_client, _time, options, parameters, _budget, FetchTimeouts.Default)),
                "gateway"));
        });
    }

    private IActorRef Gateway => GetActor<IForecastGateway>();

    private static FetchForecast Fetch(string model) => new(new LocationId("lucerne"), model);

    private async Task AttachedAsync() =>
        await _attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_throwing_fetch_is_answered_with_a_transport_failure()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch("broken"));

        var failed = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", failed.Location.Name),
            () => Assert.Equal("broken", failed.ModelId),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason),
            () => Assert.Contains(nameof(InvalidOperationException), failed.Detail));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_next_fetch_completes_after_a_throwing_fetch()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch("broken"));
        await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        Gateway.Tell(Fetch("icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", completed.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_successful_fetch_reaches_the_pipeline_input_and_the_requester()
    {
        await AttachedAsync();
        Gateway.Tell(Fetch("icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        var forecast = await _forecasts.ExpectMsgAsync<WeatherForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("icon_d2", completed.ModelId),
            () => Assert.Equal(ForecastDataHash.Compute(forecast, _time), completed.Hash),
            () => Assert.Equal("icon_d2", forecast.Model.Id));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_fetch_stream_answers_its_open_request_and_the_restarted_gateway_fetches_again()
    {
        await AttachedAsync();
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));

        Gateway.Tell(Fetch("icon_d2"));

        var failed = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("icon_d2", failed.ModelId),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason),
            () => Assert.Contains("stream stopped", failed.Detail));

        await AttachedAsync();
        Gateway.Tell(Fetch("icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", completed.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Requests_arriving_while_the_fetch_stream_fails_are_each_answered_exactly_once()
    {
        const int requests = 6;
        await AttachedAsync();
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));

        for (var i = 0; i < requests; i++)
        {
            Gateway.Tell(Fetch($"m{i:D2}"));
        }

        var answered = await ReceiveRepliesAsync(requests);
        Assert.Equal(requests, answered.Select(r => r.ModelId).Distinct().Count());
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_burst_beyond_the_fetch_queue_is_answered_exactly_once_with_queue_full_for_the_overflow()
    {
        const int requests = 100;
        await AttachedAsync();
        _client.Hold();

        for (var i = 0; i < requests; i++)
        {
            Gateway.Tell(Fetch($"m{i:D3}"));
        }

        // Offers to the full queue are refused at once, while the held fetches cannot answer yet.
        var rejected = new List<FetchForecastRejected>();
        while (await ReceiveOneAsync(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken) is { } early)
        {
            rejected.Add(Assert.IsType<FetchForecastRejected>(early));
        }

        Assert.NotEmpty(rejected);
        _client.Release();

        var rest = await ReceiveRepliesAsync(requests - rejected.Count);
        var answered = rest.Concat(rejected.Select(r => (ModelId: r.ModelId, Reply: (FetchForecastResponse)r))).ToList();
        var completed = answered.Count(r => r.Reply is FetchForecastCompleted);
        Assert.Multiple(
            () => Assert.Equal(requests, answered.Select(r => r.ModelId).Distinct().Count()),
            () => Assert.All(rejected, r => Assert.Equal(FetchRejectionReason.QueueFull, r.Reason)),
            () => Assert.DoesNotContain(answered, r => r.Reply is FetchForecastFailed),
            () => Assert.Equal(completed, _client.Calls));
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_exhausted_budget_rejects_the_request_instead_of_holding_it()
    {
        await AttachedAsync();
        // The bucket holds exactly one request's weight and refills at one cost-unit per minute.
        var weight = OpenMeteoGateway.ComputeWeight(ParameterSelection.Resolve(["Weather"], [], []).HourlyCount, AppSettingsDefaults.Options<NjordOptions>().ForecastDays);
        _budget.CostPerMinute = 1;
        _budget.MaxBurst = weight;
        _time.Advance(TimeSpan.FromSeconds(6));

        Gateway.Tell(Fetch("icon_d2"));
        await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Gateway.Tell(Fetch("icon_d2"));

        var rejected = await ExpectMsgAsync<FetchForecastRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("icon_d2", rejected.ModelId),
            () => Assert.Equal(FetchRejectionReason.Budget, rejected.Reason),
            () => Assert.Equal(1, _client.Calls));
        var recorded = await _apiCalls.ExpectMsgAsync<RecordApiCall>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(weight, recorded.Weight);
        await _apiCalls.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_that_waited_past_its_admission_deadline_is_rejected_as_expired_without_a_fetch()
    {
        await AttachedAsync();
        _client.Hold();
        Gateway.Tell(Fetch("m00"));
        Gateway.Tell(Fetch("m01"));
        Gateway.Tell(Fetch("m02"));
        await AwaitConditionAsync(() => Task.FromResult(_client.Calls == 2), TestTimeouts.AwaitAssertMax,
            cancellationToken: TestContext.Current.CancellationToken);

        // Two fetches hold both slots, the third waits in the queue while its admission window passes.
        _time.Advance(FetchTimeouts.Default.Admission + TimeSpan.FromSeconds(1));
        _client.Release();

        var answered = await ReceiveRepliesAsync(3);
        var expired = Assert.IsType<FetchForecastRejected>(Assert.Single(answered, r => r.ModelId == "m02").Reply);
        Assert.Multiple(
            () => Assert.Equal(FetchRejectionReason.Expired, expired.Reason),
            () => Assert.Equal(2, answered.Count(r => r.Reply is FetchForecastCompleted)),
            () => Assert.Equal(2, _client.Calls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_whose_location_differs_in_case_is_answered_in_the_requested_spelling()
    {
        await AttachedAsync();
        Gateway.Tell(new FetchForecast(new LocationId("LUCERNE"), "icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("LUCERNE", completed.Location.Name),
            () => Assert.Equal("icon_d2", completed.ModelId));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_requested_in_another_spelling_reaches_the_pipeline_under_the_configured_names()
    {
        await AttachedAsync();
        Gateway.Tell(new FetchForecast(new LocationId("Lucerne"), "ICON_D2"));
        await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var forecast = await _forecasts.ExpectMsgAsync<WeatherForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", forecast.Location),
            () => Assert.Equal("icon_d2", forecast.Model.Id));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Two_requests_for_the_same_target_in_flight_are_both_answered()
    {
        await AttachedAsync();
        _client.Hold();
        Gateway.Tell(Fetch("icon_d2"));
        Gateway.Tell(Fetch("icon_d2"));
        _client.Release();

        var answered = await ReceiveRepliesAsync(2);
        Assert.All(answered, r => Assert.IsType<FetchForecastCompleted>(r.Reply));
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unknown_location_fails_fast_without_a_fetch()
    {
        await AttachedAsync();
        Gateway.Tell(new FetchForecast(new LocationId("atlantis"), "icon_d2"));

        var failed = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failed.Reason),
            () => Assert.Equal(0, _client.Calls));
    }

    private async Task<List<(string ModelId, FetchForecastResponse Reply)>> ReceiveRepliesAsync(int expected)
    {
        var answered = new List<(string ModelId, FetchForecastResponse Reply)>();
        for (var i = 0; i < expected; i++)
        {
            var reply = await ExpectMsgAsync<FetchForecastResponse>(TestTimeouts.AwaitAssertMax,
                $"only {answered.Count} of {expected} requests were answered", TestContext.Current.CancellationToken);
            answered.Add(reply switch
            {
                FetchForecastCompleted c => (c.ModelId, reply),
                FetchForecastFailed f => (f.ModelId, reply),
                FetchForecastRejected r => (r.ModelId, reply),
                _ => throw new InvalidOperationException($"Unexpected reply {reply}"),
            });
        }

        return answered;
    }

    private sealed class SwitchableBudgetProvider : IBudgetProvider
    {
        public volatile bool ThrowOnNextRead;

        public volatile int MaxBurst = 16;

        public volatile int CostPerMinute = 480;

        public BudgetRate GetCurrentRate()
        {
            if (ThrowOnNextRead)
            {
                ThrowOnNextRead = false;
                throw new InvalidOperationException("simulated budget read fault");
            }

            return new BudgetRate(CostPerMinute, MaxBurst);
        }
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
                var sinkRef = await StreamRefs.SinkRef<WeatherForecast>()
                    .To(Sink.ActorRef<WeatherForecast>(forecasts, "completed", _ => "failed"))
                    .Run(mat);
                requester.Tell(new RequestForecastSinkCompleted(msg.RequestId, sinkRef), Self);
                attached.Tell("attached");
            });
        }
    }
}
