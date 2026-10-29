using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Fetch;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerGatewayConnectionTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.AddTestPersistence();

    [Fact(Timeout = 30000)]
    public async Task Real_poll_worker_and_gateway_connect_and_poll()
    {
        var fetchCalled = new TaskCompletionSource<string>();

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "test", Latitude = 47.0, Longitude = 8.3 }],
        };
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));

        var fakeGateway = Sys.ActorOf(
            Props.Create(() => new FakeGatewayActor(fetchCalled)), "gateway");
        ActorRegistry.Register<IForecastGateway>(fakeGateway, overwrite: true);

        var target = new PollTarget(new Location(options.Locations[0].Name, 0, 0));
        var requirements = Options.Create(new FeatureRequirementsOptions { Domains = [DomainKind.Forecast] });
        var worker = Sys.ActorOf(
            Props.Create(() => new PollWorker(time, Options.Create(new PollingOptions()), requirements, target.Id)),
            "real-worker");
        worker.Tell(new StartPolling(target, TimeSpan.FromMinutes(20)));

        var location = await fetchCalled.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.Equal("test", location);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(TaskCompletionSource<string>? fetchCalled = null)
        {
            Receive<FetchDomain>(msg =>
            {
                fetchCalled?.TrySetResult(msg.Location.Name);
                var hash = msg.Location.GetHashCode() ^ msg.Domain.GetHashCode();
                Sender.Tell(new FetchDomainCompleted(msg.Location, msg.Domain, hash));
            });

        }
    }
}
