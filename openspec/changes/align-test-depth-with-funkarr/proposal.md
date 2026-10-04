## Why

njord's test suite (850 tests) covers unit and actor-level concerns well but lacks two test layers that FunkArr has proven valuable: integration tests against the real HTTP/gRPC pipeline with stubbed actors, and end-to-end tests that verify the full data flow from API response to MQTT payload. The current `WebApplicationFactory<Program>` approach boots the real host with SQLite persistence, causing file contention and flaky tests. Persistence DTOs have Verify snapshots but no roundtrip tests, missing deserialization bugs.

## What Changes

- **Integration test fixture**: Replace `WebApplicationFactory<Program>` in `Njord.Tests` with a `NjordFixture` that builds the HTTP/gRPC host via `WebApplicationBuilder` + `TestServer`, with all actors replaced by `TestProbe`s. Group tests into domain collections (Health, Ops, Weather, Admin, Sensor) sharing the fixture.
- **Persistence roundtrip tests**: Add `_roundtrip()` tests alongside existing Verify `_shape()` tests for every persistence DTO. Serialize via Newtonsoft.Json (the Akka persistence serializer), deserialize back, assert field-level equality.
- **E2E verify tests**: New `Njord.E2E.Tests` project using Testcontainers (Mosquitto broker). Boot the full njord host with fake Open-Meteo HTTP responses, trigger a poll cycle, Verify-snapshot all produced MQTT messages (discovery + state payloads). Cover: happy path, multi-model consensus, enrichment pipeline, sensor push, HA birth re-discovery.

## Non-goals

- Changing production code behavior — this is test infrastructure only.
- Browser-based UI testing (njord has no UI).
- Testing against a real Open-Meteo API endpoint.
- Replacing existing unit/actor tests — the new layers complement them.

## Capabilities

### New Capabilities
- `integration-test-fixture`: TestServer-based fixture with TestProbe-stubbed actors, domain-grouped collections, shared gRPC channel and HttpClient
- `persistence-roundtrip-tests`: Serialize/deserialize roundtrip tests for every persistence DTO alongside existing Verify snapshots
- `e2e-verify-tests`: Testcontainers-based end-to-end tests that verify the full pipeline from fake API response to MQTT payloads via Verify snapshots

### Modified Capabilities
- `test-project-structure`: Existing spec updated to reflect the new E2E project, the integration fixture, and the collection-based test organization in Njord.Tests

## Impact

- **New project**: `src/Njord.E2E.Tests/` (references Njord host, Testcontainers, Verify, MQTTnet)
- **Modified project**: `src/Njord.Tests/` (new fixture, collection attributes, removed WebApplicationFactory approach for actor-dependent tests)
- **Modified project**: `src/Njord.Persistence.Tests/` (added roundtrip tests)
- **New packages**: `Testcontainers.MosquittoMqtt` (or generic Testcontainers) in E2E project
- **CI**: E2E tests require Docker — may need a separate CI job or conditional execution
- **No API-budget impact**: no polling changes
