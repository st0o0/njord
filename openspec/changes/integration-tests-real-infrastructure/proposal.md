## Why

Every njord test runs in process against fakes: the MQTT publisher is covered by 3 tests of static helpers, persistence uses in-memory TestKit journals although production wires SQL persistence, gRPC services are called directly with a hand-written `TestServerCallContext`, and nothing starts `Program.cs`. A wrong connection string, a serialization difference between the in-memory and the SQL journal, a Kestrel/HTTP-2 routing mistake, a missing DI registration in the real host or an MQTT retained/last-will mistake would pass the whole suite. The only end-to-end check is the manual, agent-driven e2e plan. FunkArr has a dedicated `IntegrationTests` project that runs the real HTTP surface in process; njord has none (the host project already declares `InternalsVisibleTo Njord.IntegrationTests`, but the project was never created).

## What Changes

- New test project `Njord.IntegrationTests` (xUnit v3 on MTP like the others, added to `Njord.slnx` and picked up by the architecture scans) with its own fixtures and a README section in `docs/development`.
- **Host smoke:** the composition of `Program.cs` moves into `NjordHost.Create(builder, ...)` (Program.cs keeps one call). `WebApplicationFactory<Program>` is not usable here: the Servus runner builds the host first and maps endpoints afterwards inside `RunAsync`, so the factory would intercept at `Build()` and never see `/healthz`, `/metrics` or the gRPC routes. The tests call `NjordHost.Create` with `UseTestServer`, test configuration (SQLite file in a temp directory, MQTT disabled, one location and model) and a `build` callback that swaps `IOpenMeteoClient` for a fake, then assert the `/healthz` and `/alive` status and the Prometheus metrics endpoint.
- **gRPC over a real channel:** `GrpcChannel` on the test server handler calls the four services end to end (weather forecast and streaming, ops, admin config read and update, sensor reading), including the `Unavailable` and `DeadlineExceeded` mappings when an actor is stopped.
- **SQL persistence with restart:** two host lifetimes over the same SQLite file prove that the poll worker state and the budget usage are recovered after a restart (events replayed, snapshot offered), and that a corrupt or missing database path fails startup validation with a clear message.
- **MQTT against a broker:** Testcontainers runs a pinned Mosquitto image; tests connect the real transport, publish the state payloads of the deterministic analysis fixture, read them back with a second MQTTnet client (retained messages equal the golden-master payloads), verify the retained `online`/`offline` service status and the last-will on an abrupt disconnect, and the reconnect and republish after a broker restart.
- Tests that need Docker live in one namespace (`Njord.IntegrationTests.Broker`) so a developer without Docker can filter them out; they fail loudly (no dynamic skip) when Docker is missing, consistent with `DisabledTestArchitectureSpec`.
- CI: the project is part of the solution, so the existing `dotnet test --solution` job runs it on the Docker-capable runner; the time budget (15-minute job) is checked and the job timeout raised in the shared workflow if needed (that file lives in `st0o0/github-workflows`).

## Capabilities

### New Capabilities
- `integration-testing`: scope, fixtures and isolation rules for tests that use the real host, a real SQLite journal and a real MQTT broker.

### Modified Capabilities
<!-- none -->

## Impact

- New project and package references (`Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`, `Grpc.Net.Client`) in `Directory.Packages.props`; `Program.cs` delegates to the new `NjordHost` (override-file path and host build step are parameters); `docs/development` page; `AGENTS.md` build and test section and test counts; `DisabledTestArchitectureSpec` special case removed or kept as-is because the project now exists.
- No production behaviour change. Not breaking; commit type `test:` (plus a `refactor:` commit for the `NjordHost` extraction).
- Applies after `test-gaps-and-drift` and `test-rules-enforcement`, and after the structure changes, so fixtures use final project names.
