## Context

Njord's startup currently uses three Servus setup containers: `NjordServiceSetup` (monolithic DI), `NjordActorSystemSetup` (all 13 actors), and `NjordApplicationSetup` (middleware). DI is delegated to 5 `AddNjord*` extension methods on `IServiceCollection` scattered across domain assemblies. FunkArr solved this with domain-specific setup containers where each domain owns both its DI and (indirectly) its actor registrations.

Additionally, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` are singletons with global persistence IDs, despite being naturally partitioned by location. The marker interfaces `IForecastSnapshotRegion` and `IEnrichmentSnapshotRegion` exist in `ActorKeys.cs` but are unused in production — only registered in `NjordFixture` for integration tests.

### Servus constraint

Servus allows only one `ActorSystemSetupContainer` per `AppBuilder` chain (`AddAkka` can be called once). However, `WithActors`, `WithShardRegion`, `WithClusterSingleton`, etc. are additive — each call appends to the builder's internal list.

## Goals / Non-Goals

**Goals:**
- Domain-specific setup containers (FunkArr-style) that each own their DI and actor declarations
- `IActorRegistration` DI pattern to bridge the single-`ActorSystemSetupContainer` constraint
- Migrate snapshot actors to ShardRegions (per-location entities)
- Akka.Streams fan-out for cross-shard queries (FunkArr pattern)
- Slim `AkkaSetupContainer` containing only infrastructure

**Non-Goals:**
- Changing actor behavior, message contracts, or enrichment logic
- Multi-node clustering
- Sharding inherently global actors (SchedulerActor, BudgetTrackerActor, stream consumers)
- Changing the middleware pipeline

## Decisions

### D1: IActorRegistration via DI (not extension methods on AkkaConfigurationBuilder)

**Choice:** Define an `IActorRegistration` interface in `Njord.Core`. Domain containers register implementations in DI. The central `AkkaSetupContainer` resolves `IEnumerable<IActorRegistration>` and invokes each.

**Alternatives considered:**
- *Extension methods on `AkkaConfigurationBuilder`* — This is essentially what exists today, just renamed. The registration call site stays in the central container, and the domain assemblies only provide static methods. No structural improvement.
- *Private helper methods in one container (FunkArr-style)* — FunkArr groups actors into private methods within a single `AkkaSetupContainer`. Clean for FunkArr's scale (15 actors, 5 shard regions), but Njord wants each domain to fully own its setup — including actors.

**Why IActorRegistration wins:** Each domain container becomes self-contained: DI services + actor declarations in one class. Adding a new domain means adding one container and one `WithSetup<>()` call in Program.cs — nothing else changes.

```
interface IActorRegistration
{
    void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider);
}
```

### D2: Container placement — each container lives in its domain assembly

Each `IServiceSetupContainer` lives in the assembly whose services it registers:
- `CoreSetupContainer` → `Njord.Core`
- `IngestSetupContainer` → `Njord.Ingest`
- `SensorSetupContainer` → `Njord.Sensors`
- `PipelineSetupContainer` → `Njord.Pipeline`
- `EgressSetupContainer` → `Njord.Egress`
- `EnrichmentSetupContainer` → `Njord.Enrichment`
- `MqttSetupContainer` → `Njord.Mqtt`
- `GrpcSetupContainer` → `Njord.Grpc`
- `AkkaSetupContainer` → `Njord` (host, infrastructure only)
- `NjordApplicationSetup` → `Njord` (host, middleware, unchanged)

**Why not all in the host?** The extension methods already live in the domain assemblies. Moving them into full containers in the same assembly is a natural evolution. The host only needs to chain `WithSetup<T>()` — it doesn't need to know what each container registers.

**Servus dependency:** Each domain assembly needs a reference to Servus for `IServiceSetupContainer`. This is already the case for `Njord.Core` (which references `Servus.Akka`). The other domain assemblies need only `Servus` (not `Servus.Akka`) for the interface.

### D3: Container chain order in Program.cs

```csharp
AppBuilder.Create(builder)
    .WithSetup<CoreSetupContainer>()          // shared options, TimeProvider, health state
    .WithSetup<IngestSetupContainer>()        // OpenMeteoClient
    .WithSetup<SensorSetupContainer>()        // SensorHubActor
    .WithSetup<PipelineSetupContainer>()      // Budget services, pipeline actors
    .WithSetup<EgressSetupContainer>()        // ModelState, ForecastSnapshot shard
    .WithSetup<EnrichmentSetupContainer>()    // Features, computers, enrichment actors + shards
    .WithSetup<MqttSetupContainer>()          // MQTT services + conditional actors
    .WithSetup<GrpcSetupContainer>()          // gRPC + GrpcSnapshotConsumer
    .WithSetup<AkkaSetupContainer>()          // Akka infra + resolves IActorRegistration
    .WithSetup<NjordApplicationSetup>()       // middleware pipeline
```

Order matters: `AkkaSetupContainer` must come after all domain containers so their `IActorRegistration` instances are already in DI when `BuildSystem` resolves them. `NjordApplicationSetup` is last (middleware pipeline).

### D4: Snapshot actor sharding — per-location entity key

**ForecastSnapshotActor:** Entity key = `"{location}|{modelId}"`. Each entity holds a single `ModelForecast`. PersistenceId = `"forecast-snapshot-{entityId}"`. This is already specced in `shard-region-entities` and `snapshot-actors`.

**EnrichmentSnapshotActor:** Entity key = `"{location}|{typeName}"`. Each entity holds a single enrichment result. PersistenceId = `"enrichment-snapshot-{entityId}"`.

**Current singleton state → per-entity state:**
- Singleton holds `ImmutableDictionary<string, T>` keyed by composite key
- Entity holds a single `T` value — no dictionary needed
- Persistence ID changes from global (`"forecast-snapshot"`) to per-entity (`"forecast-snapshot-lucerne|icon_d2"`)

**Breaking change (0.x allowed):** Old global persistence data is abandoned. Fresh start on first deploy after this change.

### D5: Fan-out query pattern (from FunkArr)

For `QueryAllForecasts` and `QueryAllEnrichments`, use Akka.Streams:

```csharp
// In GrpcSnapshotConsumerActor or a dedicated query handler
Source.From(knownEntityKeys)
    .Select(key => new QueryForecast(key.Location, key.ModelId))
    .Ask<ForecastQueryResult>(forecastSnapshotRegion, _fanOutTimeout, parallelism: 4)
    .WithAttributes(ActorAttributes.CreateSupervisionStrategy(Deciders.ResumingDecider))
    .RunWith(Sink.Seq<ForecastQueryResult>(), _materializer)
    .PipeTo(Sender, Self,
        success: items => new QueryAllForecastsResult(items.OfType<ForecastFound>().ToList()),
        failure: ex => new QueryAllForecastsFailed(ex));
```

**Entity keys come from config:** `NjordOptions.Locations` × `NjordOptions.WeatherModels` for forecasts; `NjordOptions.Locations` × `EnrichmentTypeNames` for enrichments. The caller always knows the full key set — no shard discovery needed.

**Parallelism:** 4 concurrent asks (matching FunkArr's pattern for moderate entity counts).

**Timeout:** Per-entity ask timeout (e.g. 2s). Total wall time = `ceil(entities / parallelism) × timeout` worst case.

**Supervision:** `ResumingDecider` — skip failed/timed-out entities, return partial results.

### D6: IActorRegistration lives in Njord.Core

The `IActorRegistration` interface goes into `Njord.Core` (alongside `ActorKeys.cs`). All domain assemblies already reference `Njord.Core`. The `AkkaSetupContainer` in the host resolves `IEnumerable<IActorRegistration>` — no additional reference needed.

## Risks / Trade-offs

**[Servus package dependency spread]** → Each domain assembly needs a Servus reference for `IServiceSetupContainer`. Mitigation: it's a lightweight interface-only dependency; `Njord.Core` already has it.

**[Persistence ID migration]** → Snapshot actors get new per-entity persistence IDs, orphaning old global data. Mitigation: 0.x allows breaking changes. Document in release notes. The old data stays in the DB harmlessly (no cleanup needed for SQLite; can be cleaned manually for PostgreSQL).

**[Registration order sensitivity]** → `IActorRegistration` instances are invoked in DI registration order (= container chain order in Program.cs). If an actor depends on another being registered first, order matters. Mitigation: Akka's `WithActors`/`WithShardRegion` are additive and order-independent — actors start together when the system starts, not when registered.

**[Fan-out latency for large entity sets]** → With 2 locations × 10 models = 20 entities, fan-out takes ~10s worst case (ceil(20/4) × 2s). Acceptable for gRPC queries. Would need revisiting if location count grows significantly.

## Open Questions

None — all decisions were resolved during exploration. The sharding spec already exists and matches the implementation plan.
