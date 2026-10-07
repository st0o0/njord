## Why

Njord's Akka setup is a monolith: one `NjordServiceSetup` delegates to 5 `AddNjord*` extension methods scattered across domain assemblies, and one `NjordActorSystemSetup` registers all 13 actors in a single method. FunkArr solved this cleanly with domain-specific Servus setup containers — each domain owns its DI and actor registrations. Additionally, `ForecastSnapshotActor` and `EnrichmentSnapshotActor` run as singletons with global persistence IDs despite being naturally keyed by location; the unused `IForecastSnapshotRegion` and `IEnrichmentSnapshotRegion` marker interfaces confirm sharding was planned but never executed.

## What Changes

- **Replace 5 `AddNjord*` extension methods** (`AddNjordPipeline`, `AddNjordEnrichment`, `AddNjordMqtt`, `AddNjordGrpc`, `AddNjordIngest`) with domain-specific `IServiceSetupContainer` implementations
- **Introduce `IActorRegistration` DI pattern** — each domain container registers an `IActorRegistration` that the central `AkkaSetupContainer` resolves and executes (respects Servus's single-`ActorSystemSetupContainer` constraint)
- **Migrate `ForecastSnapshotActor` to `IForecastSnapshotRegion` ShardRegion** — entity key is location, per-location persistence ID
- **Migrate `EnrichmentSnapshotActor` to `IEnrichmentSnapshotRegion` ShardRegion** — entity key is location, per-location persistence ID
- **Implement Akka.Streams fan-out** for `QueryAllForecasts` using FunkArr's proven `Source.From(ids).Ask<T>(region).RunWith(Sink.Seq)` pattern
- **Slim down `AkkaSetupContainer`** to infrastructure only (logging, persistence, remoting, clustering) plus `IActorRegistration` resolution
- **Remove duplicate `TimeProvider.System` registration** (currently in both `NjordServiceSetup` and `AddNjordIngest`)
- **Delete `NjordServiceSetup`** — its responsibilities move to the domain containers

## Non-goals

- Changing actor behavior, message contracts, or enrichment logic
- Multi-node clustering (remains single-node self-seed)
- Sharding actors that are naturally global (SchedulerActor, BudgetTrackerActor, SensorHubActor, pipeline stream consumers)
- Changing the middleware pipeline (`NjordApplicationSetup`)

## Capabilities

### New Capabilities

- `domain-setup-containers`: Domain-specific Servus `IServiceSetupContainer` implementations with `IActorRegistration` pattern for actor declaration
- `snapshot-sharding`: Migration of ForecastSnapshotActor and EnrichmentSnapshotActor from singletons to per-location ShardRegions with Akka.Streams fan-out queries

### Modified Capabilities

- `servus-bootstrap`: Program.cs chains domain containers instead of the monolithic NjordServiceSetup
- `snapshot-actors`: Snapshot actors become sharded entities with per-location persistence IDs

## Impact

- **Host project (`Njord/Configuration/`)**: `NjordServiceSetup` deleted, `NjordActorSystemSetup` slimmed, `Program.cs` updated to chain domain containers
- **Each feature library**: Gains a setup container class (new file per library)
- **`Njord.Egress`**: `ForecastSnapshotActor` changes persistence model (singleton → per-location entity)
- **`Njord.Enrichment`**: `EnrichmentSnapshotActor` changes persistence model (singleton → per-location entity)
- **`Njord.Grpc`**: `GrpcSnapshotConsumerActor` / `WeatherGrpcService` fan-out queries to shard regions
- **`Njord.Core/Actors/ActorKeys.cs`**: `IForecastSnapshotRegion` and `IEnrichmentSnapshotRegion` activated (no longer unused)
- **Persistence**: Existing snapshot data under the old global persistence IDs needs migration or fresh start (0.x — breaking change allowed)
- **Tests**: Actor specs for snapshot actors need shard-aware test setup; integration tests (`NjordFixture`) updated for new container chain
