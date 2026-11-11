using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class NjordGrpcServicePartialTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            registry.Register<IPollManager>(system.ActorOf(Props.Create(() => new NoReplyActor())));
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new NoReplyActor())));
        });

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_sets_budget_partial_when_tracker_times_out()
    {
        var service = CreateService();

        var status = await service.GetStatus(new Proto.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.True(status.BudgetPartial),
            () => Assert.Equal(0, status.Budget.MonthlyUsed),
            () => Assert.Equal(0, status.Budget.DailyUsed));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_sets_models_partial_when_poll_manager_times_out()
    {
        var service = CreateService();

        var status = await service.GetStatus(new Proto.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.True(status.ModelsPartial),
            () => Assert.Empty(status.Models));
    }

    private NjordGrpcService CreateService()
    {
        IFeatureRule[] rules = [new EntityTestRule("Heat")];
        var registry = new EntityRegistry(
            rules,
            Options.Create(new NjordOptions { ResolvedLocations = [_vienna] }),
            Options.Create(new ActiveModels()));
        return new NjordGrpcService(ActorRegistry, Sys, registry, TimeProvider.System,
            TestServiceInfo.Opts(AppSettingsDefaults.Options<PollingOptions>()), rules, NullLogger<NjordGrpcService>.Instance);
    }

    private sealed class NoReplyActor : ReceiveActor
    {
        public NoReplyActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
