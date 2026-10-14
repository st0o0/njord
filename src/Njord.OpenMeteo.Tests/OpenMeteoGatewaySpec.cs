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
    private readonly SwitchableBudgetProvider _budget = new();

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

    private sealed class SwitchableBudgetProvider : IBudgetProvider
    {
        public volatile bool ThrowOnNextRead;

        public BudgetRate GetCurrentRate()
        {
            if (ThrowOnNextRead)
            {
                ThrowOnNextRead = false;
                throw new InvalidOperationException("simulated budget read fault");
            }

            return new BudgetRate(480, 16);
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
