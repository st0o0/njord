## Context

The njord test suite currently has three tiers:
1. **Unit tests** (domain, analysis, builders, options validation) — well covered
2. **Actor specs** (using raw `ActorSystem.Create`, fake collaborators) — good coverage
3. **Integration tests** — one Mosquitto container test (`MqttEgressIntegrationSpec`), gated behind `NJORD_DOCKER_TESTS=1`

The `OpenMeteoClient` is tested with an in-process `RecordingHandler` — a custom `HttpMessageHandler` that records requests and returns canned responses. This validates parsing logic but never exercises a real HTTP connection. Several value objects (`CycleId`, `TimeAnchor`, `RequestBudget`, `WeightedTarget`, `HorizonProjection`, `TopicSlug`) lack dedicated tests.

Existing test infrastructure:
- xUnit v3 on Microsoft.Testing.Platform (`dotnet run`, not `dotnet test`)
- `Testcontainers` NuGet already present (used by Mosquitto test)
- `Verify.XunitV3` for snapshot testing (used by `ModuleInitializer.cs`)
- `Microsoft.AspNetCore.Mvc.Testing` present (used by `HealthEndpointSpec`)
- JSON fixtures: `openmeteo-icon_eu-96h.json`, `openmeteo-icon_d2-96h.json`

## Goals / Non-Goals

**Goals:**
- Achieve >90% line coverage across the codebase
- All integration tests run by default (no env-var gate)
- Container-based integration test for `OpenMeteoClient` against WireMock
- Full E2E test: WireMock (API) + Mosquitto (broker), proving the complete data path
- Dedicated unit tests for all uncovered value objects and projections

**Non-Goals:**
- 100% line coverage (pragmatic about `Program.cs` bootstrap, `MqttNetPublisher` internals)
- OpenAPI spec for Open-Meteo (doesn't exist; WireMock mappings are hand-crafted)
- Production code changes
- Performance benchmarking of container startup

## Decisions

### 1. WireMock.Net.Testcontainers over in-process WireMock.Net

**Choice**: Use `WireMock.Net.Testcontainers` NuGet which starts a `wiremock/wiremock` Docker container.

**Alternatives considered**:
- *In-process `WireMock.Net`*: Faster startup (~0ms vs ~3-5s), but doesn't test real network I/O. Since CI has Docker and the user wants container-based tests, the extra fidelity is worth the startup cost.
- *Hand-rolled `HttpListener`*: More control but significant boilerplate; WireMock provides request matching, verification, and fault injection out of the box.

**Rationale**: Consistent container-based approach across all integration tests. WireMock provides request verification (assert the client sent the right query params), fault simulation (timeouts, connection resets), and fixture serving — all via C# fluent API.

### 2. xUnit class fixtures for container lifecycle

**Choice**: Use `IAsyncLifetime` on a shared fixture class per container type (one for WireMock, one reuse pattern for Mosquitto). Tests within a class share the container; the container starts once per test class.

**Rationale**: Container startup is the expensive part (~3-5s). Sharing a container across tests in a class amortizes this. Individual tests configure WireMock stubs per-test (stateless between tests via `Reset()`).

### 3. Remove NJORD_DOCKER_TESTS gate entirely

**Choice**: Remove `Assert.SkipWhen(Environment.GetEnvironmentVariable("NJORD_DOCKER_TESTS") != "1", ...)` from `MqttEgressIntegrationSpec`. All Docker tests run unconditionally.

**Rationale**: CI has Docker. Local dev on this project uses Docker (Aspire AppHost, docker-compose reference). No reason to gate.

### 4. E2E test scope: single poll cycle with 1 location, 2 models

**Choice**: The E2E test boots a minimal actor system with WireMock as the API backend and Mosquitto as the broker. It triggers one poll cycle, waits for retained messages on Mosquitto, and asserts:
- Correct number of horizon state topics published
- Correct JSON payload structure on state topics
- Discovery device configs present with correct component count
- Availability topic shows "online"

**Alternatives considered**:
- *WebApplicationFactory-based E2E*: Would test `Program.cs` wiring too, but adds complexity (config injection, waiting for actor system readiness). The existing `HealthEndpointSpec` already validates `WebApplicationFactory` works. The actor-system-level E2E is more valuable.
- *Multi-cycle test*: Would test delta publishing / deduplication, but significantly increases complexity and test duration. Single cycle proves the data path.

### 5. Unit test additions — direct, no shared base class

**Choice**: Each value object gets a simple, standalone test class following the existing `Spec` suffix convention. No shared test utilities or base classes for unit tests.

**Rationale**: The uncovered types (`CycleId`, `TimeAnchor`, `RequestBudget`, etc.) are simple value objects. Each test class is 20-50 lines. Shared infrastructure would be over-engineering.

## Risks / Trade-offs

- **[Container flakiness]** → Docker containers can fail to start due to image pull issues, port conflicts, or daemon unavailability. Mitigation: WireMock.Net.Testcontainers handles retries and random port assignment. Tests use `WaitStrategy` for readiness. Local Docker daemon is a prerequisite (documented).

- **[Test runtime increase]** → Adding WireMock + expanding Mosquitto tests adds ~10-15s to total test runtime. Mitigation: Acceptable for integration confidence. Container reuse within test classes minimizes overhead.

- **[WireMock container image availability]** → The `wiremock/wiremock` image must be pullable. Mitigation: Standard Docker Hub image, widely cached in CI environments. First run pulls; subsequent runs use cache.

- **[Fixture drift]** → JSON fixtures may diverge from real API responses over time. Mitigation: The existing `OpenMeteoSmokeSpec` (gated behind `NJORD_SMOKE_TESTS`) validates against the real API. WireMock tests validate our parsing of the fixture format, not API compatibility.
