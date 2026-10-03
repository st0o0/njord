## Context

The codebase established `TimeProvider` as the canonical time abstraction (architecture guardrail). Most actors and services already use it, but 7 call sites slipped through using `DateTimeOffset.UtcNow` directly. Separately, several actors resolve peer references synchronously in `PreStart` via `Context.GetActor<T>()`, which throws if the target actor hasn't registered yet. Finally, three enrichment option classes accept any value without validation.

## Goals / Non-Goals

**Goals:**
- Every time access goes through `TimeProvider` — zero direct `DateTimeOffset.UtcNow` calls in production code.
- Actors that resolve peers at startup use `GetActorAsync` with `PipeTo` instead of sync `GetActor`, eliminating startup-order sensitivity.
- Invalid enrichment config fails fast at startup with clear error messages.

**Non-Goals:**
- Changing actor supervision or message protocols.
- Adding `TimeProvider` to code that doesn't currently use time.
- Migrating away from `IActorRegistry`.
- Validating all config classes — only the three identified enrichment option types.

## Decisions

### D1: TimeProvider injection via constructor

**Choice:** Inject `TimeProvider` via constructor where not already available (WeatherGrpcService, HistoryAnalyzer). MqttConnectionActor already has it.

**Why not a static helper?** Constructor injection is the established pattern in the codebase and supports test fakes.

### D2: GetActorAsync with PipeTo for deferred resolution

**Choice:** Replace `Context.GetActor<T>()` in PreStart with `Context.GetActorAsync<T>().PipeTo(Self)`, handling the resolved reference in a `Receive<IActorRef>` or a wrapper message. Actors that already use stashing (EnrichmentActor) naturally handle this; others need a `Become` transition or a nullable field + guard.

**Why not lazy resolution on first message?** Lazy resolution delays failure detection and makes every message handler check for null. The PipeTo pattern fails fast if the actor isn't registered and integrates with the existing stash patterns.

**Alternative considered:** SchedulerActor already uses `GetActorAsync` — follow the same pattern for consistency.

### D3: IValidateOptions for enrichment config

**Choice:** Implement `IValidateOptions<T>` for ConsensusOptions, EnergyOptions, and HistoryOptions, registered in DI. Validation runs at first `IOptions<T>.Value` access (effectively startup).

**Rules:**
- ConsensusOptions.Method: must be "Mean", "Median", or "TrimmedMean"
- ConsensusOptions.TrimPercent: must be in (0, 0.5) when Method is TrimmedMean
- EnergyOptions.CarnotEfficiency: must be in (0, 1)
- EnergyOptions.FlowTemp: must be > 0
- HistoryOptions.SnapshotInterval: must be > 0
- HistoryOptions.RetentionDays: must be > 0
- HistoryOptions.MinSampleSize: must be > 0

## Risks / Trade-offs

- [GetActorAsync adds startup complexity] → Actors already using stash (EnrichmentActor, DiscoveryActor) absorb this naturally. For simpler actors (ModelStateActor, MqttEgressActor, GrpcSnapshotConsumerActor), a single `Become` or nullable field is sufficient.
- [Validation may break existing configs with unusual values] → All default values are valid; only truly nonsensical values (e.g., CarnotEfficiency > 1, negative intervals) are rejected.
