## Context

Njord.Messages has 33 .cs files and ~115 public types. 18 inter-actor message types live outside it (in Core/Actors, Core/Domain, Grpc). The Pipeline/ directory mixes polling commands (StartPolling, QueryPollStates) with feature results (BbqScoreResult, FrostAlertResult) and unrelated enums (UvLevel, PollenType). Configuration options carry nullable+Effective* workarounds in 18 places.

The 0.5.0 release is the last chance for breaking namespace changes before users depend on the public surface.

## Goals / Non-Goals

**Goals:**
- Every inter-actor message in Njord.Messages, no exceptions
- Navigable namespace structure where the path tells you what a type does
- Zero dead code in the message and configuration layers
- Single source of truth for each configuration value

**Non-Goals:**
- Changing actor topology, stream graphs, or wire formats
- Redesigning the feature rule abstraction
- Moving persistence records

## Decisions

### D1: Akka.Streams reference in Njord.Messages

**Decision**: Add `Akka.Streams` NuGet to Njord.Messages.

**Why**: 3 stream handshake `*Completed` types carry `ISinkRef<T>` / `ISourceRef<T>`. Moving them requires the package. The alternatives:
- Keep them in Core (violates "all messages in Messages") -- rejected
- Wrap in `object` and cast at receiver (type-unsafe, hides intent) -- rejected
- Extract interfaces without Akka (over-engineering for 3 types) -- rejected

`ISinkRef`/`ISourceRef` are serializable data handles, not actor infrastructure. The dependency is narrow (no ActorSystem, no Props, no Receive).

### D2: Namespace tree

**Decision**: Six top-level namespaces plus Results with OutputKind sub-namespaces.

```
Njord.Messages.Alerts         (enums + shared alert infra)
Njord.Messages.Budget         (budget actor commands/queries)
Njord.Messages.Config         (catalog DTOs)
Njord.Messages.Fetch          (gateway fetch commands + client result types)
Njord.Messages.Polling        (poll lifecycle + state queries)
Njord.Messages.Stream         (stream payloads + handshakes)
Njord.Messages.Results        (FeatureResult base + ActivityWindow)
Njord.Messages.Results.Alerts
Njord.Messages.Results.Scores
Njord.Messages.Results.Conditions
Njord.Messages.Results.Trends
Njord.Messages.Results.Forecasts
```

**Why Fetch instead of Forecast**: The directory contained gateway-level fetch/response messages, not forecast domain types. "Fetch" matches the command (`FetchDomain`) and avoids confusion with `Njord.Core.Forecast` (the point types).

**Why not one file per result**: 66 result files would be excessive. Group by OutputKind: `AlertResults.cs` (30 types), `ScoreResults.cs`, `ConditionResults.cs`, `TrendResults.cs`, `ForecastResults.cs` -- each cohesive and under 100 lines. Enums go into the same file as their result.

### D3: PostConfigure for option defaults

**Decision**: Register `PostConfigure<T>` callbacks that replace null arrays with their defaults, then make the properties non-nullable.

```csharp
// Before (18 places):
public string[]? Models { get; set; }
public string[] EffectiveModels => Models ?? [DefaultModel];

// After:
public string[] Models { get; set; } = [DefaultModel];
// + PostConfigure ensures Models is never replaced with empty by the binder
```

**Why PostConfigure**: `IConfiguration.Bind()` replaces non-null defaults with empty arrays when the section exists. `PostConfigure` runs after binding and restores the default if the bound value is null or empty. This is the idiomatic .NET pattern.

**Why not IConfigureOptions**: PostConfigure runs after all Configure calls, so it catches both binding and programmatic changes. It is the standard "ensure invariant after configuration" hook.

### D4: SolarYieldConfig reads from SolarYieldOptions

**Decision**: Remove PeakKw and SystemEfficiency from SolarYieldConfig. The SolarYield rule takes `IOptions<SolarYieldOptions>` as a constructor parameter (in addition to its own config).

**Why**: SolarYieldOptions already has PeakKw (for the API tilt/azimuth request). Duplicating it in the feature config at a different config path (`Njord:SolarYield` vs `Njord:Features:Energy:SolarYield`) confuses operators. One source at `Njord:SolarYield`.

### D5: Un-nest PipelineEvent subtypes

**Decision**: Move the 8 nested records to top-level in `Njord.Messages.Stream`. Keep the abstract base `PipelineEvent`.

**Why**: The rest of the codebase uses top-level sealed records. The nested union is a C# pattern that predates the project's conventions. Top-level types allow simpler `using` statements and pattern matching without the parent prefix.

### D6: Implementation order

Namespace moves must happen atomically per namespace (all types in a namespace move together) to avoid partial-migration compile errors. Order:

1. Add Akka.Streams dep to Messages
2. Move types in batch (Core->Messages, Grpc->Messages) with using-alias forwarding for one commit
3. Reorganize Pipeline/ into the new namespaces
4. Split result mega-files
5. Delete dead code
6. Configuration cleanup (PostConfigure, Model shim, SolarYield)
7. Update all consumers (features, tests, host)

## Risks / Trade-offs

**[Large diff]** Moving ~130 types across namespaces produces a noisy diff. Mitigation: each batch is a single commit with a clear message. IDE "move type" refactoring handles most of it.

**[Akka.Streams in Messages]** Opens the door to heavier Akka dependencies creeping in. Mitigation: ArchUnit rule asserting Messages references only Akka.Streams, not Akka.Actor.

**[PostConfigure ordering]** If a third-party PostConfigure runs after ours and nulls the array, the invariant breaks. Mitigation: none expected -- we control all registrations.

**[Test churn]** Every `using Njord.Messages.Pipeline` in test files changes. Mitigation: bulk find-replace, then `dotnet build` to catch stragglers.

## Open Questions

None -- all decisions were validated in the explore phase.
