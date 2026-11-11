using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Njord.Core;

namespace Njord.Grpc.Tests;

public sealed class ActorAskExtensionsTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    [Fact]
    public void Default_timeout_is_five_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ActorAskExtensions.DefaultTimeout);
    }

    [Fact(Timeout = 10_000)]
    public async Task Throws_unavailable_when_actor_not_registered()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            ActorRegistry.AskAsync<IForecastPipeline, string>(
                "hello", TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = 10_000)]
    public async Task Throws_deadline_exceeded_on_timeout()
    {
        var blackHole = Sys.ActorOf(Props.Create(() => new BlackHoleActor()));
        ActorRegistry.Register<IForecastPipeline>(blackHole);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            ActorRegistry.AskAsync<IForecastPipeline, string>(
                "hello", TestServerCallContext.Create(TestContext.Current.CancellationToken),
                TimeSpan.FromMilliseconds(100)));

        Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
    }

    private sealed class BlackHoleActor : ReceiveActor
    {
        public BlackHoleActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
