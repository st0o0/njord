## Why

Integration and E2E tests currently orchestrate Docker containers manually via Testcontainers — each test class builds its own Mosquitto/WireMock infrastructure with port mappings, config mounting, and wait strategies. Meanwhile the Aspire AppHost already knows how to wire Mosquitto and Njord together but is only used for dev. The AppHost is no longer needed for dev workflows, so repurposing it as the test orchestrator eliminates the manual container plumbing, gives us a real black-box E2E test that boots the full Njord host (DI, Akka, streams, MQTT lifecycle — the things that break in production), and keeps a single source of truth for infrastructure wiring.

## What Changes

- **Repurpose AppHost for testing**: Remove dev-only resources (MQTT Explorer), add WireMock container, wire its endpoint into Njord as the Open-Meteo base URL.
- **Make Open-Meteo base URL configurable**: Extract the hardcoded `https://api.open-meteo.com/` from `IngestServiceCollectionExtensions` into `NjordOptions` so the AppHost (and tests) can redirect API calls to WireMock.
- **Add `TriggerPoll` gRPC RPC**: New unary RPC on `ConfigService` that tells the SchedulerActor to poll immediately for specified (or all) location/model pairs. Fire-and-forget — the test observes completion via MQTT retained messages. Useful beyond tests for debugging and post-config-change scenarios.
- **Migrate integration tests to Aspire fixtures**: Replace manual Testcontainers usage with `DistributedApplicationTestingBuilder` backed by the AppHost. Shared fixture per AppHost instance.
- **Rewrite E2E test as black-box**: Instead of manually assembling pipeline components in-process, the E2E test boots the full Njord host via Aspire, loads WireMock fixtures, triggers a poll via gRPC, and asserts on MQTT retained messages.
- **Remove Testcontainers dependency** from integration/E2E test projects once migration is complete.

## Non-goals

- Consensus implementation — out of scope, deferred per existing decision.
- Adding new test scenarios beyond what exists today — this is infrastructure migration, not coverage expansion.
- CI/CD pipeline changes — test runner commands stay the same (`dotnet run`).
- PostgreSQL testing — the Aspire fixture uses SQLite persistence (default profile).

## Capabilities

### New Capabilities
- `trigger-poll-rpc`: gRPC `TriggerPoll` unary RPC on `ConfigService` for on-demand poll triggering with location/model filtering.
- `aspire-test-fixture`: Aspire-based shared test fixture using `DistributedApplicationTestingBuilder` against the repurposed AppHost, providing WireMock admin API and MQTT subscriber access to tests.

### Modified Capabilities
- `aspire-apphost`: Repurposed from dev-only to test orchestrator — adds WireMock container, removes MQTT Explorer, wires OpenMeteoBaseUrl.
- `openmeteo-client`: Open-Meteo base URL becomes configurable via `NjordOptions.OpenMeteoBaseUrl` instead of hardcoded.
- `integration-test-infrastructure`: Tests migrate from manual Testcontainers to Aspire fixtures; E2E becomes true black-box against the full host.
- `test-project-structure`: Integration/E2E projects replace `Testcontainers` dependency with `Aspire.Hosting.Testing`; add `Njord.AppHost` project reference.

## Impact

- **Production code**: Minimal — one new config property (`OpenMeteoBaseUrl`), one new gRPC RPC (`TriggerPoll`), one new message to `SchedulerActor`.
- **Proto**: `config_service.proto` gets `TriggerPoll` RPC + request/response messages.
- **AppHost**: `Program.cs` restructured (WireMock added, MQTT Explorer removed, OpenMeteoBaseUrl wired).
- **Test projects**: `Njord.Tests.Integration` and `Njord.Tests.Integration.E2E` csprojs change dependencies; test classes rewritten against Aspire fixture.
- **Dependencies**: Add `Aspire.Hosting.Testing` to test projects; remove `Testcontainers` and `WireMock.Net.Testcontainers` from test projects (WireMock.Net client library stays for admin API).
- **No API-budget impact**: No changes to polling frequency or request patterns.
