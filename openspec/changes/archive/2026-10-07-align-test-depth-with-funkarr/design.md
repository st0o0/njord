## Context

njord has 850 unit/actor tests but lacks integration tests against the real HTTP/gRPC pipeline and end-to-end tests that verify the full data flow. The current `HealthEndpointSpec` uses `WebApplicationFactory<Program>` which boots the real host with SQLite persistence, causing file contention and flaky tests. FunkArr solved this with a `FunkArrFixture` pattern: a `TestServer`-backed fixture with all actors replaced by `TestProbe`s.

## Goals / Non-Goals

**Goals:**
- Integration test fixture with TestProbe-stubbed actors (no SQLite, no file contention)
- Persistence roundtrip tests catching deserialization bugs
- E2E tests verifying the full pipeline (Open-Meteo → enrichment → MQTT) via Verify snapshots

**Non-Goals:**
- Changing production code behavior
- Testing against real Open-Meteo or real HA instances
- Replacing existing unit/actor tests

## Decisions

### D1: NjordFixture in Njord.Tests.Shared, not in Njord.Tests

**Decision**: Place `NjordFixture` in `Njord.Tests.Shared` so both `Njord.Tests` and `Njord.E2E.Tests` can reference it.

**Alternative**: Fixture in `Njord.Tests` only. Rejected because E2E tests also need the fixture pattern (minus Testcontainers-specific setup).

### D2: WebApplicationBuilder + TestServer, not WebApplicationFactory

**Decision**: Build the host from scratch using `WebApplicationBuilder` → `builder.Build()` → `TestServer` with explicit service registration. Register all actor keys as `TestProbe`s via `IActorRegistry`.

**Alternative**: Continue with `WebApplicationFactory<Program>`. Rejected because it boots the real host entrypoint including SQLite persistence configuration, which is the root cause of flaky tests.

**Why**: FunkArr's proven pattern. The fixture controls the actor system configuration completely — no SQLite, no MQTT connection attempts, no Open-Meteo HTTP calls.

### D3: One TestProbe per actor key, exposed as named properties

**Decision**: The fixture creates one `TestProbe` per actor key and exposes them as properties (`SchedulerProbe`, `PipelineProbe`, etc.). Tests access probes to assert actor messages and reply with domain results.

**Actor keys** (15 total): ISchedulerActor, IBudgetTrackerActor, IPipelineActor, IModelStateActor, IEnrichmentActor, ISensorHubActor, IForecastSnapshotActor, IEnrichmentSnapshotActor, IGrpcSnapshotConsumerActor, IMqttConnectionActor, IMqttStateActor, IMqttDiscoveryActor, IForecastHistoryRegion, IForecastSnapshotRegion, IEnrichmentSnapshotRegion.

### D4: Domain-grouped xUnit collections

**Decision**: Five collections sharing the fixture: Health, Weather, Ops, Admin, Sensor. Each collection definition class uses `ICollectionFixture<NjordFixture>`. Tests within a collection run sequentially; collections may run in parallel.

**Why**: Matches FunkArr's 7-collection pattern. Prevents probe message interference within a domain while allowing cross-domain parallelism.

### D5: Roundtrip tests use Newtonsoft.Json with TypeNameHandling.All

**Decision**: Use `JsonConvert.SerializeObject` / `DeserializeObject` with `new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }` — the exact settings used by Akka.Persistence.Sql's default serializer.

**Alternative**: Use Akka's `Serialization.FindSerializerFor()`. Rejected because it requires a running actor system; the roundtrip test should be a pure serialization test.

### D6: E2E tests use Testcontainers for Mosquitto, DelegatingHandler for Open-Meteo

**Decision**: 
- **Mosquitto**: `Testcontainers.MosquittoMqtt` (or generic `ContainerBuilder` with `eclipse-mosquitto:2` image). The container exposes a mapped MQTT port.
- **Open-Meteo**: A `DelegatingHandler` registered in the `IHttpClientFactory` pipeline returns canned JSON responses from fixture files.
- **Host**: Built with `WebApplicationBuilder` like production, but with the injected handler and the Testcontainers MQTT host/port.

**Why**: Testcontainers is already a pattern in the dotnet ecosystem. `DelegatingHandler` is lighter than WireMock and deterministic.

### D7: Verify snapshots for all MQTT messages in E2E

**Decision**: The E2E fixture subscribes to `#` (all topics) on the Mosquitto broker, collects all published messages, and uses Verify to snapshot them. Each test scenario produces one `.verified.txt` file containing the ordered list of topics + JSON payloads.

**Scrubbing**: Timestamps, unique IDs, and port numbers are scrubbed before snapshotting.

### D8: E2E tests use FakeTimeProvider for deterministic timestamps

**Decision**: Register `FakeTimeProvider` (from `Microsoft.Extensions.Time.Testing`) in the E2E host to control `TimeProvider.GetUtcNow()`. All timestamps in MQTT payloads become deterministic for Verify comparison.

## Risks / Trade-offs

- **[Docker dependency for E2E]** → E2E tests require Docker. Mitigated by making them skippable in CI via trait/env-var check, and running them in a separate CI job.
- **[Fixture startup time]** → The TestServer + Akka actor system boot takes ~1-2s. Mitigated by sharing the fixture across collections (one boot per collection, not per test).
- **[Verify snapshot maintenance]** → MQTT payload changes require updating `.verified.txt` files. Mitigated by using `[ModuleInitializer]` scrubbers for volatile fields.
- **[TestProbe message ordering]** → Parallel probe assertions can be fragile. Mitigated by sequential execution within each domain collection.

## Open Questions

- Should the E2E project use the same `NjordFixture` base as integration tests, or a separate `E2EFixture` with Testcontainers lifecycle?
- Should E2E tests trigger polls via gRPC `TriggerPoll` or by advancing `FakeTimeProvider` past the poll interval?
