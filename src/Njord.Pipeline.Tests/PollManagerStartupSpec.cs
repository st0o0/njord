using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollManagerStartupSpec : PollManagerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Every_resolved_model_of_every_location_is_started_once()
    {
        // StartedManagerAsync asserts the exact StartPolling sequence: global models first (the duplicate
        // spelling collapsed), then the location's own models.
        await StartedManagerAsync(TestContext.Current.CancellationToken);

        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_running_worker_of_a_removed_target_is_stopped()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedManagerAsync(ct, "atlantis");

        var stop = await _region.ExpectMsgAsync<StopPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        Assert.Equal("atlantis", stop.LocationName);
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_terminated_worker_is_started_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var worker = CreateTestProbe("worker");
        manager.Tell(new PollWorkerReport(Targets[1], PollWorkerOutcome.Planned, Entry(Targets[1])), worker.Ref);
        await QueryAsync(manager, ct);

        await worker.Ref.GracefulStop(TestTimeouts.RealTimeWaitMax);

        var start = await _region.ExpectMsgAsync<StartPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(new StartPolling(Targets[1], Discovery), start);
    }
}
