---
name: njord-actor-spec
description: Use when writing or changing tests for actors, enrichment features or persistence DTOs in the src/Njord*.Tests projects — Akka.Hosting TestKit, in-memory persistence, FakeTimeProvider, nested fake actors and Verify wire-format snapshots.
---

# njord actor spec

Test rules (`Tests` suffix, `sealed`, `[Fact(Timeout = 30000)]` as outer safety net, BDD-style names, assertion conventions) live in `AGENTS.md` -- follow them. Run tests from `src/` with `dotnet run`, never `dotnet test`:

```powershell
dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj -- -class "Njord.Budget.Tests.BudgetTrackerTests"
```

Every `Njord.<Project>.Tests` project tests exactly one production project; shared fakes and helpers live in `Njord.Tests.Shared`.

## Which kind of test

| Subject | Base | Example |
|---|---|---|
| Actor (incl. persistent) | `Akka.Hosting.TestKit.TestKit` | `src/Njord.Budget.Tests/BudgetTrackerTests.cs`, `src/Njord.Polling.Tests/PollWorkerLifecycleTests.cs` |
| Analysis feature / pure logic | plain class, no TestKit | `src/Njord.Features.Tests/` |
| gRPC service (ask and map) | `Akka.Hosting.TestKit.TestKit` + fake actors behind markers | `src/Njord.Grpc.Tests/WeatherGrpcServiceTests.cs` |
| Persistence record wire format | plain class + Verify | `src/Njord.Persistence.Tests/WireFormatTests.cs` |
| Architecture rule | plain class, ArchUnitNET or reflection | `src/Njord.Architecture.Tests/` |

## Actor test skeleton (from `BudgetTrackerTests`)

```csharp
public sealed class BudgetTrackerTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(TestTimeConstants.BudgetEpoch);
    private IOptions<PollingOptions> _polling = Options.Create(AppSettingsDefaults.Options<PollingOptions>());

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.AddTestPersistence();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(_time);

    private IActorRef CreateTracker() =>
        Sys.ActorOf(Props.Create(() => new BudgetTracker(_time, _polling)), $"budget-tracker-{Guid.NewGuid():N}");

    [Fact(Timeout = 30000)]
    public async Task Recorded_calls_are_reported_as_weighted_usage()
    {
        var tracker = CreateTracker();
        tracker.Tell(new RecordApiCall(4));

        var status = await tracker.Ask<BudgetStatusResult>(
            new QueryBudgetStatus(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(4, status.DailyUsed);
    }
}
```

- Derive from `Akka.Hosting.TestKit.TestKit` directly (no intermediate base class).
- Override `ConfigureAkka` to call `builder.AddTestPersistence()` for persistent actors (in-memory journal + snapshot store via `Njord.Tests.Shared.TestAkkaExtensions`). Non-persistent actors: leave `ConfigureAkka` empty or omit the persistence call.
- Override `ConfigureServices` to register `FakeTimeProvider` as `TimeProvider`.
- Always pass `TestContext.Current.CancellationToken`; advance time with `_time.SetUtcNow(...)` / `_time.Advance(...)`, never sleep.
- Use `AppSettingsDefaults.Options<T>()` for options that have shipped defaults (never `new XOptions()` for values that have a shipped default). Mutate with `AppSettingsDefaults.Options<T>(o => o.Prop = val)`.
- Actor names: always unique per test via `$"name-{Guid.NewGuid():N}"`.

## Collaborators

Register probes or fake actors for every marker the actor under test resolves:

```csharp
private TestProbe _gateway = null!;
private TestProbe _manager = null!;

protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
{
    builder.AddTestPersistence();
    builder.WithActors((_, registry) =>
    {
        _gateway = CreateTestProbe("gateway");
        _manager = CreateTestProbe("manager");
        registry.Register<IForecastGateway>(_gateway.Ref);
        registry.Register<IPollManager>(_manager.Ref);
    });
}
```

- `CreateTestProbe()` for message-level assertions (`ExpectMsgAsync`, `LastSender`).
- Nested `private sealed class FakeXxx : ReceiveActor` registered under the marker for more complex fakes (examples: `src/Njord.Grpc.Tests/WeatherGrpcServiceTests.cs`, `src/Njord.Grpc.Tests/OpsGrpcServiceTests.cs`).

## Recovery test pattern

```csharp
[Fact(Timeout = 30000)]
public async Task A_restarted_tracker_recovers_usage_from_the_journal()
{
    var tracker = CreateTracker();
    tracker.Tell(new RecordApiCall(4));
    await UsageOf(tracker, TestContext.Current.CancellationToken);

    Watch(tracker);
    Sys.Stop(tracker);
    await ExpectTerminatedAsync(tracker, cancellationToken: TestContext.Current.CancellationToken);

    var recovered = CreateTracker();
    Assert.Equal((4, 4), await UsageOf(recovered, TestContext.Current.CancellationToken));
}
```

- Stop with `Sys.Stop` + `Watch`/`ExpectTerminatedAsync` (or `GracefulStop`).
- Recreate under a new unique name (never reuse a name after stopping); recovery follows the persistence id, not the actor name.

## Verify wire-format snapshot (from `WireFormatTests`)

```csharp
[Fact]
public Task Poll_worker_state_shape() =>
    Verify(JsonConvert.SerializeObject(State, Formatting.Indented));
```

- Baselines sit next to the test as `<Class>.<Method>.verified.txt`; a change in them is a wire-format change -- review it against the persistence record rules in `AGENTS.md`.
- `ModuleInitializer.cs` (in test projects that use Verify) disables the diff tool (`DiffRunner.Disabled = true`): failures print the diff, nothing launches.

## Shared helpers (`Njord.Tests.Shared`)

| Helper | Purpose |
|---|---|
| `TestAkkaExtensions.AddTestPersistence()` | In-memory journal + snapshot store |
| `TestTimeConstants` | Fixed epoch timestamps (`BudgetEpoch`, `PipelineEpoch`, `NoonAnchor`, `ForecastEpoch`, `EnrichmentEpoch`) |
| `AppSettingsDefaults.Options<T>()` | Shipped option values bound like the host binds them |
| `TestRequirements.For(domains)` | `DomainRequirements` for a given set of domains |

## Checklist

1. Derive from `Akka.Hosting.TestKit.TestKit`; unique actor names.
2. `AddTestPersistence()` for persistent actors, `FakeTimeProvider` registered as `TimeProvider`.
3. `TestContext.Current.CancellationToken` and explicit timeouts on every async call.
4. Persistent actor: a recovery test. Persistence record: a roundtrip test and a Verify wire-format test.
5. Run the single class first, then the whole project.
