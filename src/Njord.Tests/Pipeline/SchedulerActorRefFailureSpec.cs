using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Pipeline;
using Njord.Tests.Actors;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorRefFailureSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _requestProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(ParameterRegistry.Resolve(["Weather"], [], []));
        services.AddSingleton(new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _requestProbe = CreateTestProbe();
                registry.Register<PipelineActor>(system.ActorOf(FailingRefProvider.Props(_requestProbe)));
            })
            .WithResolvableActors(r =>
            {
                r.Register<SchedulerActor>("scheduler");
            })
            .AddTestTimefactor();
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_both_refs_after_pipeline_ref_failures()
    {
        var ct = TestContext.Current.CancellationToken;

        await _requestProbe.ExpectMsgAsync<RequestPipelineSink>(cancellationToken: ct);
        await _requestProbe.ExpectMsgAsync<RequestPipelineSource>(cancellationToken: ct);

        await _requestProbe.ExpectMsgAsync<RequestPipelineSink>(TimeSpan.FromSeconds(4), cancellationToken: ct);
        await _requestProbe.ExpectMsgAsync<RequestPipelineSource>(TimeSpan.FromSeconds(4), cancellationToken: ct);
    }

    [Fact(Timeout = 5000)]
    public async Task Stays_responsive_while_waiting_for_refs_after_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        await _requestProbe.ExpectMsgAsync<RequestPipelineSink>(cancellationToken: ct);

        var states = await ActorRegistry.Get<SchedulerActor>()
            .Ask<SchedulerQueryResponse>(new QueryPollStates(), TimeSpan.FromSeconds(2), ct);

        Assert.IsAssignableFrom<SchedulerQueryResponse>(states);
    }
}
