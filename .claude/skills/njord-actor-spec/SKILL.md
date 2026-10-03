---
name: njord-actor-spec
description: Use when writing or changing tests for actors, enrichment features or persistence DTOs in src/Njord.Tests — Akka.Hosting TestKit specs, in-memory persistence, FakeTimeProvider, nested fake actors and Verify wire-format snapshots.
---

# njord actor spec

Test rules (`Spec` suffix, `sealed`, `[Fact(Timeout = 5000)]`, BDD-style names, assertion conventions) live in `AGENTS.md` — follow them. Run tests with `dotnet run`, never `dotnet test`:

```powershell
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.BudgetTrackerActorSpec"
```

## Which kind of spec

| Subject | Base | Example |
|---|---|---|
| Actor (incl. persistent) | `Akka.Hosting.TestKit.TestKit` | `src/Njord.Tests/Pipeline/BudgetTrackerActorSpec.cs` |
| Enrichment feature / pure logic | plain class, no TestKit | `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs` |
| Cross-feature invariants | plain class | `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` |
| DTO wire format | plain class + Verify | `src/Njord.Tests/Persistence/EnrichmentSnapshotDtoSerializationSpec.cs` |

## Actor spec skeleton (from `BudgetTrackerActorSpec`)

```csharp
public sealed class BudgetTrackerActorSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(T0);   // Microsoft.Extensions.Time.Testing

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        => builder.AddTestPersistence().AddTestTimefactor();   // AddTestPersistence: Njord.Tests.Shared

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        => services.AddSingleton<TimeProvider>(_time);

    private IActorRef CreateActor(string? name = null) => Sys.ActorOf(
        Props.Create(() => new BudgetTrackerActor(_time, _healthState)),
        name ?? $"budget-tracker-{Guid.NewGuid():N}");   // unique name per test

    [Fact(Timeout = 5000)]
    public async Task Records_and_queries_usage()
    {
        var actor = CreateActor();
        actor.Tell(new BudgetTrackerActor.RecordApiCall(1));

        var usage = await actor.Ask<BudgetUsageResult>(
            new BudgetTrackerActor.QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(1, usage.DailyUsed);
    }
}
```

- `AddTestPersistence()` (`src/Njord.Tests.Shared/TestPersistenceConfig.cs`) = in-memory journal + snapshot store.
- Always pass `TestContext.Current.CancellationToken`; advance time with `_time.SetUtcNow(...)`, never sleep.
- Recovery test: use a fixed name, `await actor.GracefulStop(...)`, recreate with the same name, assert state (`Recovery_replays_events_from_current_month`).
- Collaborators: nested `private sealed class FakeXxxActor : ReceiveActor` or `CreateTestProbe()` (examples: `src/Njord.Tests/Grpc/OpsGrpcServiceSpec.cs`, `src/Njord.Tests/Egress/ModelStateActorSpec.cs`).
- Time: use the framework `FakeTimeProvider` everywhere (the feature specs do too); do not write your own.

## Verify wire-format snapshot (from `EnrichmentSnapshotDtoSerializationSpec`)

```csharp
using static VerifyXunit.Verifier;

[Fact]
public Task EnrichmentSnapshot_dto_produces_stable_wire_format()
{
    var dto = EnrichmentSnapshotMapping.ToDto(state);
    return Verify(JsonConvert.SerializeObject(dto, Formatting.Indented));
}
```

- Baselines sit next to the spec as `<Class>.<Method>.verified.txt`; a change in them is a wire-format change — review it against the DTO rules in `AGENTS.md`.
- `src/Njord.Tests/ModuleInitializer.cs` disables the diff tool (`DiffRunner.Disabled = true`): failures print the diff, nothing launches.

## Checklist

1. Right base class (TestKit only for actors); unique actor names.
2. Persistence via `AddTestPersistence()`, time via `FakeTimeProvider` registered as `TimeProvider`.
3. Cancellation token and timeouts on every async call.
4. Persistent actor: a recovery spec. DTO: a Verify spec.
5. Run the single class first, then the whole project.
