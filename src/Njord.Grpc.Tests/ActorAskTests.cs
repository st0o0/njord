using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;

namespace Njord.Grpc.Tests;

public sealed class ActorAskTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private interface IUnregisteredTarget;

    private interface ISilentTarget;

    private sealed record Ping;

    private sealed record Pong;

    [Fact(Timeout = 30_000)]
    public async Task An_unregistered_actor_fails_with_unavailable()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => ActorRegistry.AskAsync<IUnregisteredTarget, Pong>(
            new Ping(), TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task An_actor_that_does_not_answer_in_time_fails_with_deadline_exceeded()
    {
        ActorRegistry.Register<ISilentTarget>(Sys.ActorOf(Props.Create(() => new SilentActor()), $"silent-{Guid.NewGuid():N}"));

        var ex = await Assert.ThrowsAsync<RpcException>(() => ActorRegistry.AskAsync<ISilentTarget, Pong>(
            new Ping(), TestServerCallContext.Create(TestContext.Current.CancellationToken),
            TimeSpan.FromMilliseconds(200)));

        Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
    }

    private sealed class SilentActor : ReceiveActor;
}
