---
name: njord-actor-spec
description: Use when writing or changing tests for actors, enrichment features or persistence DTOs in the src/Njord*.Tests projects — Akka.Hosting TestKit specs via NjordActorSpec, in-memory persistence, FakeTimeProvider, nested fake actors and Verify wire-format snapshots.
---

# njord actor spec

Test rules (`Spec` suffix, `sealed`, `[Fact(Timeout = ...)]` as outer safety net above the dilated TestKit waits (`TestTimeouts.Hosted` for hosted specs; TestKit dilates explicit durations too, so pass the un-dilated `TestTimeouts.AwaitAssertMax` to `AwaitAssert`/`AwaitCondition`/`ExpectMsg` and use `TestTimeouts.RealTimeWaitMax` for `Task.WaitAsync`), BDD-style names, assertion conventions) live in `AGENTS.md` — follow them. Run tests from `src/` with `dotnet run`, never `dotnet test`:

```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj -- -class "Njord.Budget.Tests.BudgetTrackerSpec"
```

Every `Njord.<Project>.Tests` project tests exactly one production project; shared fakes and helpers live in `Njord.Tests.Shared`.

## Which kind of spec

| Subject | Base | Example |
|---|---|---|
| Actor (incl. persistent) | `NjordActorSpec` (`Njord.Tests.Shared`, derives from `Akka.Hosting.TestKit.TestKit`) | `src/Njord.Budget.Tests/BudgetTrackerSpec.cs`, `src/Njord.Pipeline.Tests/PollWorkerOutcomeSpec.cs` |
| Analysis feature / pure logic | plain class, no TestKit | `src/Njord.Weather.Tests/WeatherAlertHandlerSpec.cs` |
| gRPC service (ask and map) | `NjordActorSpec` + a fake actor behind the marker | `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs` |
| Persistence record wire format | plain class + Verify | `src/Njord.Persistence.Tests/PollWorkerRecordsSpec.cs` |
| Architecture rule | plain class, ArchUnitNET or reflection | `src/Njord.Architecture.Tests/` |

## Actor spec skeleton (from `BudgetTrackerSpec`)

```csharp
public sealed class BudgetTrackerSpec : NjordActorSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.BudgetEpoch;
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(T0);   // Microsoft.Extensions.Time.Testing
    private readonly BudgetUsageState _usage = new();
    private ConfigView _config = TestConfigView.Create();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        => services.AddSingleton<TimeProvider>(_time);

    private IActorRef CreateActor(string? name = null) => Sys.ActorOf(
        Props.Create(() => new BudgetTracker(_time, _usage, _config)),
        name ?? $"budget-tracker-{Guid.NewGuid():N}");   // unique name per test

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Records_and_queries_usage()
    {
        var actor = CreateActor();
        actor.Tell(new RecordApiCall(1));

        var usage = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(1, usage.DailyUsed);
    }
}
```

- `NjordActorSpec` registers in-memory persistence (`AddTestPersistence()`) and the test time factor; override `UseTestPersistence => false` for non-persistent actors and `ConfigureActors(...)` to add actors to the Akka configuration. `GetActor<TKey>()` resolves a registered marker.
- Always pass `TestContext.Current.CancellationToken`; advance time with `_time.SetUtcNow(...)` / `Advance(...)`, never sleep. Use `TestTimeouts.AtEpoch()` for a `FakeTimeProvider` and `AppSettingsDefaults.Options<T>(...)` for options (never `new XOptions()` for values that have a shipped default).
- Recovery test: `await actor.GracefulStop(...)`, then recreate the actor under a new unique name (never reuse a name after `GracefulStop`); recovery follows the persistence id, not the actor name. Assert the recovered state (see `BudgetTrackerSpec`).
- Collaborators: a nested `private sealed class FakeXxx : ReceiveActor` registered under the marker interface in `ActorRegistry`, or `CreateTestProbe()` (examples: `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs`, `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs`, `src/Njord.OpenMeteo.Tests/GatewayBudgetGateSpec.cs`).
- Waiting for eventually-consistent state: `AwaitAssertAsync(..., TestTimeouts.AwaitAssertMax, cancellationToken: ct)`, never `Task.Delay`/`Thread.Sleep`.
- Time: use the framework `FakeTimeProvider` everywhere; do not write your own. Helpers that need a clock take a `TimeProvider` parameter.

## Verify wire-format snapshot (from `PollWorkerRecordsSpec`)

```csharp
using static VerifyXunit.Verifier;

[Fact]
public Task PersistedPollWorkerState_wire_format() =>
    Verify(JsonConvert.SerializeObject(Snapshot, Formatting.Indented));
```

- Baselines sit next to the spec as `<Class>.<Method>.verified.txt`; a change in them is a wire-format change — review it against the persistence record rules in `AGENTS.md`.
- `ModuleInitializer.cs` (in `Njord.Budget.Tests`, `Njord.Core.Tests`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`) disables the diff tool (`DiffRunner.Disabled = true`): failures print the diff, nothing launches.

## Checklist

1. Right base class (`NjordActorSpec` only for actors); unique actor names.
2. Persistence via `NjordActorSpec` defaults, time via `FakeTimeProvider` registered as `TimeProvider`.
3. Cancellation token and timeouts on every async call.
4. Persistent actor: a recovery spec. Persistence record: a roundtrip and a Verify spec.
5. Run the single class first, then the whole project.
