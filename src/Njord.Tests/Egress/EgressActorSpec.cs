using Akka.Actor;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Tests.Egress;

public sealed class EgressActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("egress-spec");

    public void Dispose() => _system.Dispose();

    [Fact(Timeout = 5000)]
    public async Task Registered_publisher_receives_broadcast()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        var probe = _system.ActorOf(Props.Create(() => new ProbeActor()));

        egress.Tell(new RegisterPublisher(probe));
        await Task.Delay(100);

        egress.Tell(new PublishStateResult("lucerne", "test-result"));

        var received = await probe.Ask<PublishStateResult>("get", TimeSpan.FromSeconds(2));
        Assert.Equal("lucerne", received.Location);
    }

    [Fact(Timeout = 5000)]
    public async Task Multiple_publishers_all_receive_broadcast()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        var probe1 = _system.ActorOf(Props.Create(() => new ProbeActor()));
        var probe2 = _system.ActorOf(Props.Create(() => new ProbeActor()));

        egress.Tell(new RegisterPublisher(probe1));
        egress.Tell(new RegisterPublisher(probe2));
        await Task.Delay(100);

        egress.Tell(new PublishStateResult("home", "data"));

        var r1 = await probe1.Ask<PublishStateResult>("get", TimeSpan.FromSeconds(2));
        var r2 = await probe2.Ask<PublishStateResult>("get", TimeSpan.FromSeconds(2));

        Assert.Equal("home", r1.Location);
        Assert.Equal("home", r2.Location);
    }

    [Fact(Timeout = 5000)]
    public async Task No_publishers_drops_message_silently()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        egress.Tell(new PublishStateResult("lucerne", "data"));
        await Task.Delay(100);
    }

    [Fact(Timeout = 5000)]
    public async Task Duplicate_registration_is_idempotent()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        var probe = _system.ActorOf(Props.Create(() => new ProbeActor()));

        egress.Tell(new RegisterPublisher(probe));
        egress.Tell(new RegisterPublisher(probe));
        await Task.Delay(100);

        egress.Tell(new PublishStateResult("lucerne", "data"));

        var received = await probe.Ask<PublishStateResult>("get", TimeSpan.FromSeconds(2));
        Assert.Equal("lucerne", received.Location);
    }

    [Fact(Timeout = 5000)]
    public async Task Terminated_publisher_is_auto_unregistered()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        var probe = _system.ActorOf(Props.Create(() => new ProbeActor()));

        egress.Tell(new RegisterPublisher(probe));
        await Task.Delay(100);

        _system.Stop(probe);
        await Task.Delay(300);

        egress.Tell(new PublishStateResult("lucerne", "data"));
        await Task.Delay(100);
    }

    [Fact(Timeout = 5000)]
    public async Task Unregister_unknown_publisher_is_noop()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());
        var probe = _system.ActorOf(Props.Create(() => new ProbeActor()));

        egress.Tell(new UnregisterPublisher(probe));
        await Task.Delay(50);
    }

    private sealed class ProbeActor : ReceiveActor
    {
        private readonly Queue<(IActorRef Sender, string Request)> _waiters = new();
        private readonly Queue<PublishStateResult> _received = new();

        public ProbeActor()
        {
            Receive<PublishStateResult>(msg =>
            {
                if (_waiters.Count > 0)
                {
                    var (sender, _) = _waiters.Dequeue();
                    sender.Tell(msg);
                }
                else
                {
                    _received.Enqueue(msg);
                }
            });

            Receive<string>(msg =>
            {
                if (msg == "get")
                {
                    if (_received.Count > 0)
                        Sender.Tell(_received.Dequeue());
                    else
                        _waiters.Enqueue((Sender, msg));
                }
            });
        }
    }
}
