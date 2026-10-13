using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class ActorAskSpec : Akka.Hosting.TestKit.TestKit
{
    private interface IEchoTarget;

    private interface IUnregisteredTarget;

    private sealed record Ping;

    private sealed record Pong;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_answering_actor_returns_its_response()
    {
        ActorRegistry.Register<IEchoTarget>(Sys.ActorOf(Props.Create(() => new EchoActor()), $"echo-{Guid.NewGuid():N}"));

        var response = await ActorRegistry.AskAsync<IEchoTarget, Pong>(
            new Ping(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.IsType<Pong>(response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unresolved_actor_fails_with_unavailable()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => ActorRegistry.AskAsync<IUnregisteredTarget, Pong>(
            new Ping(), TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_actor_that_does_not_answer_in_time_fails_with_deadline_exceeded()
    {
        ActorRegistry.Register<IEchoTarget>(Sys.ActorOf(Props.Create(() => new SilentActor()), $"silent-{Guid.NewGuid():N}"));

        var ex = await Assert.ThrowsAsync<RpcException>(() => ActorRegistry.AskAsync<IEchoTarget, Pong>(
            new Ping(), TestServerCallContext.Create(TestContext.Current.CancellationToken), TimeSpan.FromMilliseconds(200)));

        Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
    }

    private sealed class EchoActor : ReceiveActor
    {
        public EchoActor() => Receive<Ping>(_ => Sender.Tell(new Pong()));
    }

    private sealed class SilentActor : ReceiveActor
    {
        public SilentActor() => Receive<Ping>(_ => { });
    }
}
