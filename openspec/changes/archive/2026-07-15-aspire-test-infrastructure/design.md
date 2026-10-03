## Context

Integration and E2E tests currently use Testcontainers to manually orchestrate WireMock and Mosquitto containers per test class. Each test handles port mapping, config mounting, wait strategies, and endpoint wiring. The Aspire AppHost (`Njord.AppHost`) already orchestrates Mosquitto + Njord for dev but is unused — the user no longer needs it for development. The E2E test assembles the pipeline manually in-process rather than booting the real host, so it misses DI wiring, Akka actor lifecycle, and MQTT connection management bugs.

## Goals / Non-Goals

**Goals:**
- Repurpose the AppHost as the single infrastructure definition for tests
- Provide a true black-box E2E test that boots the full Njord host
- Add a `TriggerPoll` gRPC RPC so tests (and operators) can trigger polls deterministically
- Eliminate manual container orchestration from test code
- Keep a shared fixture per AppHost instance (not per test)

**Non-Goals:**
- Adding new test scenarios beyond what already exists
- CI/CD pipeline changes (test commands stay `dotnet run`)
- PostgreSQL integration testing
- Consensus implementation
- Making the AppHost usable for dev again (it becomes test-only)

## Decisions

### D1: Repurpose existing AppHost rather than creating a test-specific one

**Choice**: Modify `Njord.AppHost/Program.cs` to serve test needs.

**Alternatives considered**:
- *Separate `Njord.Tests.AppHost` project*: Cleaner separation but duplicates infrastructure definitions. Since the user no longer uses the AppHost for dev, repurposing avoids maintaining two orchestration definitions.

**Rationale**: Single source of truth for "what infrastructure does Njord need." If dev use returns, a separate test AppHost can be extracted then.

### D2: WireMock as Aspire container (not the WireMock.Net.Testcontainers integration)

**Choice**: Add WireMock as `builder.AddContainer("wiremock", "wiremock/wiremock", "latest")` in the AppHost, inject its HTTP endpoint as `Njord__OpenMeteoBaseUrl`.

**Alternatives considered**:
- *Keep WireMock.Net.Testcontainers in test projects*: Defeats the purpose of Aspire-managed infrastructure.
- *Static WireMock mappings via file mount*: Less flexible — tests need the Admin API to configure responses dynamically.

**Rationale**: Aspire manages the container lifecycle; tests only interact with the WireMock Admin API via HTTP. The `WireMock.Net` NuGet client library (not the Testcontainers variant) stays for the admin API.

### D3: `OpenMeteoBaseUrl` as a config property on NjordOptions

**Choice**: Add `public string OpenMeteoBaseUrl { get; set; } = "https://api.open-meteo.com/";` to `NjordOptions`. The `IngestServiceCollectionExtensions` reads it from the resolved options instead of hardcoding.

**Alternatives considered**:
- *Separate `OpenMeteoOptions` class*: Over-engineered for one URL.
- *Named HttpClient with address override in DI*: More complex, less discoverable.

**Rationale**: Consistent with how `MqttOptions.Host` works — simple config property, injected via environment variable by Aspire.

### D4: `TriggerPoll` as fire-and-forget unary RPC

**Choice**: Add `rpc TriggerPoll (TriggerPollRequest) returns (TriggerPollResponse)` to `ConfigService`. The RPC tells `SchedulerActor` to schedule immediate polls and returns the count/targets. It does NOT wait for the polls to complete.

**Alternatives considered**:
- *Server-streaming RPC that streams completion events*: More complex, the test already has MQTT subscription as completion signal.
- *Short PollInterval instead of manual trigger*: Non-deterministic, racey, slower.
- *Health endpoint polling*: Indirect — health says "something succeeded" not "my trigger completed."

**Rationale**: Fire-and-forget is simplest. The test's completion signal is MQTT retained messages arriving — that's the actual observable outcome. The gRPC call just starts the process deterministically.

### D5: Shared fixture with `IAsyncLifetime` on the test class collection

**Choice**: One `DistributedApplication` instance shared across all tests in a project via xUnit `ICollectionFixture<T>`. The fixture starts the AppHost once, exposes WireMock admin API URL, MQTT connection options, and gRPC channel.

**Alternatives considered**:
- *Per-test AppHost instance*: Too slow — AppHost startup includes container pulls, health waits, and Akka actor system boot.
- *Per-class fixture via `IAsyncLifetime`*: Still wasteful when multiple test classes exist.

**Rationale**: AppHost startup is expensive (5-15s). Sharing one instance keeps the test suite fast. Tests that need isolated WireMock state reset mappings between tests via the admin API. MQTT state (retained messages) from prior tests is acceptable — tests subscribe after triggering their own poll cycle and validate their specific topics.

### D6: WireMock fixture loading via Admin API from tests

**Choice**: Tests use `IWireMockAdminApi` (from `WireMock.Net` client package) to load fixtures before triggering a poll. The fixture JSON files remain in `Njord.Tests.Shared`.

**Rationale**: Same pattern as today's integration tests. Tests control exactly which responses WireMock serves, enabling error scenario tests (429, 400) alongside success tests.

### D7: MQTT retained message collection as assertion mechanism

**Choice**: Tests subscribe to MQTT topics after triggering a poll and wait for expected retained messages with a timeout. Reuse the existing `MosquittoHelper.CollectRetainedAsync` pattern from `Njord.Tests.Shared`.

**Rationale**: The retained messages ARE the product — they're what Home Assistant sees. Asserting on them validates the entire pipeline end-to-end without coupling to internals. The 3-second collection delay in `MosquittoHelper` is acceptable for E2E tests.

### D8: Remove MQTT Explorer from AppHost

**Choice**: Drop the MQTT Explorer container definition since the AppHost is now test-only.

**Rationale**: MQTT Explorer is a dev debugging tool with no role in automated tests. Removing it speeds up AppHost startup.

## Risks / Trade-offs

- **[Shared state between tests]** → Tests reset WireMock mappings and subscribe to specific MQTT topics after their own trigger. Retained MQTT messages from prior tests are noise but don't cause false failures if tests check their own topics precisely.
- **[AppHost startup time]** → Shared fixture amortizes the ~10s startup across all tests. First test in the suite pays the cost.
- **[WireMock container image pull]** → First run pulls `wiremock/wiremock:latest`. CI should cache Docker layers. Aspire's container management handles this.
- **[gRPC port discovery in tests]** → `DistributedApplicationTestingBuilder` provides endpoint resolution — tests get the gRPC endpoint via `app.GetEndpoint("njord", "grpc")` or equivalent Aspire API.
- **[Akka persistence state across tests]** → The shared fixture boots one actor system. If tests trigger multiple poll cycles, the SchedulerActor accumulates `DataChanged` events. This matches production behavior and is acceptable.
