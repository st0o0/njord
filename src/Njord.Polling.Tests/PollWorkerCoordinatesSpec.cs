using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerCoordinatesSpec : PollWorkerSpecBase
{
    private static readonly Location Moved = new("lucerne", 47.10, 8.40);

    private static (string, double, double) Coordinates(Location l) => (l.Name, l.Latitude, l.Longitude);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_started_worker_targets_the_coordinates_of_its_location()
    {
        var ct = TestContext.Current.CancellationToken;
        CreateWorker().Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location)),
            () => Assert.Equal(Coordinates(Lucerne), Coordinates(request.Location)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_recovered_worker_keeps_the_coordinates_of_its_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);
        await StopAsync(worker, ct);

        CreateWorker();

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restart_with_changed_coordinates_retargets_and_keeps_the_poll_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        var before = await AnswerAllAndLastReportAsync(Completed(1), ct);

        worker.Tell(new StartPolling(new PollTarget(Moved), Discovery));

        var after = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(Coordinates(Moved), Coordinates(after.Target.Location)),
            () => Assert.Equal(before.State.LastChangeUtc, after.State.LastChangeUtc),
            () => Assert.Equal(before.State.Phase, after.State.Phase));

        _time.Advance(TimeSpan.FromHours(1));
        worker.Tell(new TriggerLocationPoll(TargetId));
        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Coordinates(Moved), Coordinates(request.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restart_with_changed_coordinates_survives_a_recovery()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        worker.Tell(new StartPolling(new PollTarget(Moved), Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        await StopAsync(worker, ct);

        CreateWorker();

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Moved), Coordinates(report.Target.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restart_with_identical_target_and_interval_only_reports_the_plan()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);

        worker.Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }
}
