## Why

All test types (unit, actor, container-integration, E2E) live in a single `Njord.Tests` project. This means unit tests restore Docker-related NuGet packages they never use, container tests can't be parallelized or gated separately in CI, and the dependency footprint is larger than necessary. The GaudiHTTP project at `D:\GIT\Akka.Streams.Http` demonstrates that splitting by test type improves clarity, CI speed, and dependency hygiene.

## What Changes

- Create `Njord.Tests.Shared` project for shared test infrastructure: JSON fixtures, `FakeOpenMeteoClient`, `MosquittoHelper` (retained-message collection), common constants
- Keep `Njord.Tests` for unit tests and actor tests (no Docker, no network I/O)
- Create `Njord.Tests.Integration` for container-based tests (WireMock for Open-Meteo, Mosquitto for MQTT)
- Create `Njord.Tests.Integration.E2E` for the full pipeline test (WireMock + Mosquitto end-to-end)
- Move existing test files to their new projects
- Remove `Testcontainers`, `WireMock.Net.Testcontainers`, `MQTTnet` from `Njord.Tests`
- Update `Njord.slnx` to include all 4 test projects
- Update `CLAUDE.md` build & test commands

## Non-goals

- No new tests — this is purely structural reorganization
- No production code changes
- No changes to test logic or assertions
- No CI pipeline changes (that's a follow-up)

## Capabilities

### New Capabilities

- `test-project-structure`: The organization of test projects, their dependency boundaries, shared infrastructure, and which test types belong where

### Modified Capabilities

_(none — no spec-level behavior changes)_

## Impact

- **Solution file**: `Njord.slnx` gains 3 new project entries
- **Dependencies**: `Njord.Tests` loses container-related NuGet packages; `Integration` and `E2E` projects carry them instead
- **CI**: Each test project can be run independently via `dotnet run --project`; enables future parallelization
- **CLAUDE.md**: Build & test commands section updated for multi-project test suite
