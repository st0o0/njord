using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Common;
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

    protected override bool UseTestPersistence => false;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new SilentActor()), "budget"));
            var options = Options.Create(new NjordOptions
            {
                Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
                Models = ["icon_d2", "broken"],
            });
            var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
            registry.Register<IForecastGateway>(system.ActorOf(
                Props.Create(() => new OpenMeteoGateway(_client, _time, options, parameters, _budget)),
                "gateway"));
        });
    }

    private IActorRef Gateway => GetActor<IForecastGateway>();

    private static FetchForecast Fetch(string model) => new(new LocationId("lucerne"), model);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_throwing_fetch_is_answered_with_a_transport_failure()
    {
        Gateway.Tell(Fetch("broken"));

        var failed = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", failed.Location.Name),
            () => Assert.Equal("broken", failed.ModelId),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason),
            () => Assert.Contains(nameof(InvalidOperationException), failed.Detail));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_next_fetch_completes_after_a_throwing_fetch()
    {
        Gateway.Tell(Fetch("broken"));
        await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        Gateway.Tell(Fetch("icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", completed.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stopped_fetch_stream_fails_the_in_flight_request_and_recovers()
    {
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));

        Gateway.Tell(Fetch("icon_d2"));

        var failed = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("icon_d2", failed.ModelId),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason));

        Gateway.Tell(Fetch("icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", completed.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_stream_taken_before_the_recovery_keeps_receiving_forecasts()
    {
        var response = await Gateway.Ask<RequestForecastStreamResponse>(
            new RequestForecastStream(Guid.NewGuid()), TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
        var sourceRef = Assert.IsType<RequestForecastStreamCompleted>(response).SourceRef;
        var forecasts = CreateTestProbe();
        sourceRef.Source.RunWith(Sink.ActorRef<WeatherForecast>(forecasts, "done", _ => "failed"), Sys.Materializer());

        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        Gateway.Tell(Fetch("icon_d2"));
        await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        Gateway.Tell(Fetch("icon_d2"));
        await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var forecast = await forecasts.ExpectMsgAsync<WeatherForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", forecast.Model.Id);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task More_requests_than_the_fetch_queue_holds_are_each_answered_exactly_once()
    {
        const int requests = 48;
        _client.Hold();

        for (var i = 0; i < requests; i++)
        {
            Gateway.Tell(Fetch($"m{i:D2}"));
        }

        // The gateway handles its mailbox in order: once this is answered, every request above has been offered.
        await Gateway.Ask<RequestForecastStreamResponse>(
            new RequestForecastStream(Guid.NewGuid()), TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
        _client.Release();

        var answered = await ReceiveRepliesAsync(requests);
        Assert.Multiple(
            () => Assert.Equal(requests, answered.Count),
            () => Assert.Equal(requests, answered.Select(r => r.ModelId).Distinct().Count()));
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_beyond_the_full_backlog_is_answered_immediately_and_the_rest_after_release()
    {
        const int requests = OpenMeteoGateway.MaxWaitingRequests + 64;
        _client.Hold();

        for (var i = 0; i < requests; i++)
        {
            Gateway.Tell(Fetch($"m{i:D4}"));
        }

        var rejected = await ExpectMsgAsync<FetchForecastFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, rejected.Reason),
            () => Assert.Contains("backlog is full", rejected.Detail));

        _client.Release();

        var answered = await ReceiveRepliesAsync(requests - 1);
        answered.Add((rejected.ModelId, rejected));
        Assert.Equal(requests, answered.Select(r => r.ModelId).Distinct().Count());
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Requests_offered_while_the_fetch_stream_stops_are_each_answered_exactly_once()
    {
        const int requests = 6;
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));

        for (var i = 0; i < requests; i++)
        {
            Gateway.Tell(Fetch($"m{i:D2}"));
        }

        var answered = await ReceiveRepliesAsync(requests);
        Assert.Equal(requests, answered.Select(r => r.ModelId).Distinct().Count());
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        Gateway.Tell(Fetch("icon_d2"));
        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("icon_d2", completed.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_whose_location_differs_in_case_is_answered_in_the_requested_spelling()
    {
        Gateway.Tell(new FetchForecast(new LocationId("LUCERNE"), "icon_d2"));

        var completed = await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("LUCERNE", completed.Location.Name),
            () => Assert.Equal("icon_d2", completed.ModelId));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_requested_in_another_spelling_reaches_the_hub_under_the_configured_names()
    {
        var response = await Gateway.Ask<RequestForecastStreamResponse>(
            new RequestForecastStream(Guid.NewGuid()), TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
        var sourceRef = Assert.IsType<RequestForecastStreamCompleted>(response).SourceRef;
        var forecasts = CreateTestProbe();
        sourceRef.Source.RunWith(Sink.ActorRef<WeatherForecast>(forecasts, "done", _ => "failed"), Sys.Materializer());

        Gateway.Tell(new FetchForecast(new LocationId("Lucerne"), "ICON_D2"));
        await ExpectMsgAsync<FetchForecastCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var forecast = await forecasts.ExpectMsgAsync<WeatherForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("lucerne", forecast.Location),
            () => Assert.Equal("icon_d2", forecast.Model.Id));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Two_requests_for_the_same_target_in_flight_are_both_answered()
    {
        _client.Hold();
        Gateway.Tell(Fetch("icon_d2"));
        Gateway.Tell(Fetch("icon_d2"));
        _client.Release();

        var answered = await ReceiveRepliesAsync(2);
        Assert.All(answered, r => Assert.IsType<FetchForecastCompleted>(r.Reply));
        await ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
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
                _ => throw new InvalidOperationException($"Unexpected reply {reply}"),
            });
        }

        return answered;
    }

    private sealed class SwitchableBudgetProvider : IBudgetProvider
    {
        public volatile bool ThrowOnNextRead;

        public volatile int MaxBurst = 16;

        public BudgetRate GetCurrentRate()
        {
            if (ThrowOnNextRead)
            {
                ThrowOnNextRead = false;
                throw new InvalidOperationException("simulated budget read fault");
            }

            return new BudgetRate(480, MaxBurst);
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
