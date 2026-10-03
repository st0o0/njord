## Why

The project has solid unit test coverage for domain logic, analysis computations, and actor message handling, but lacks integration tests that exercise the real I/O boundaries. The `OpenMeteoClient` is tested with an in-process `HttpMessageHandler` stub — no real HTTP server validates request construction or response parsing against actual network behavior. The single MQTT integration test is gated behind `NJORD_DOCKER_TESTS=1`, making it easy to skip in CI. There is no end-to-end test proving the full data path (API fetch → domain mapping → egress projection → MQTT publish) works as a connected system. Several value objects and projections (`CycleId`, `TimeAnchor`, `RequestBudget`, `WeightedTarget`, `HorizonProjection`, `TopicSlug`) have no dedicated tests. Target: >90% line coverage.

## What Changes

- Add `WireMock.Net.Testcontainers` NuGet package for container-based Open-Meteo API simulation
- Create integration tests for `OpenMeteoClient` against a WireMock container serving the existing JSON fixtures
- Create a full E2E pipeline integration test: WireMock (Open-Meteo) + Mosquitto (MQTT broker), verifying the complete data path from poll tick to retained MQTT messages
- Remove the `NJORD_DOCKER_TESTS=1` gate from `MqttEgressIntegrationSpec` — Docker tests run by default
- Add unit tests for uncovered value objects and projections: `CycleId`, `TimeAnchor`, `RequestBudget`, `WeightedTarget`, `HorizonProjection`, `TopicSlug`
- Add shared test fixture infrastructure for WireMock container lifecycle

## Non-goals

- No production code changes — this is purely test infrastructure
- No `MqttNetPublisher` dedicated test — it is already exercised implicitly through the existing Mosquitto integration test
- Not targeting 100% coverage — pragmatic about pure infrastructure wiring (`Program.cs` bootstrap, DI registration internals)
- No OpenAPI spec authoring — Open-Meteo has no official spec; WireMock mappings are hand-crafted from the existing JSON fixtures

## Capabilities

### New Capabilities

- `integration-test-infrastructure`: Shared WireMock and Mosquitto container fixtures, test base classes, and fixture-serving helpers for container-based integration tests

### Modified Capabilities

_(none — no spec-level behavior changes, only test additions)_

## Impact

- **Dependencies**: New NuGet `WireMock.Net.Testcontainers` in `Directory.Packages.props` and `Njord.Tests.csproj`
- **CI**: Docker daemon required for all test runs (previously optional). CI pipelines must have Docker available.
- **Test runtime**: Integration tests add container startup overhead (~5-10s per container). Acceptable tradeoff for confidence.
- **Files touched**: `Njord.Tests.csproj`, `Directory.Packages.props`, `MqttEgressIntegrationSpec.cs` (gate removal), plus new test files
