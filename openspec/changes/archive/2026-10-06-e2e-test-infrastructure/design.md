## Context

njord publishes Home Assistant entities via two paths: MQTT Discovery (built-in HA integration) and gRPC streaming (ha-njord custom component). The gRPC path is the strategic direction — MQTT may become obsolete. Neither path has end-to-end test coverage against a real running stack.

ha-njord lives at `D:\GIT\ha-njord` as a separate repo. It connects to njord via gRPC on port 8081, using `StreamForecasts()`, `StreamEnrichments()`, `StreamConfig()` RPCs. Its config flow is a single screen (host + port). Given 1 location with 2 models and all enrichments, it creates ~47 HA entities across weather, sensor, binary_sensor, event, and button platforms.

The existing `E2EFixture` in `Njord.IntegrationTests` uses WebApplicationFactory (in-process) with Testcontainers for Mosquitto — it tests the MQTT path and doesn't involve ha-njord or a real HA instance.

## Goals / Non-Goals

**Goals:**
- Validate the full gRPC path: njord → ha-njord → HA entities with real data
- Reuse the FunkArr E2E pattern: agent-orchestrated, Docker-based, PASS/FAIL result table
- Create a reusable skill that any developer can invoke from either repo
- Document timing and entity expectations on first run for future regression detection

**Non-Goals:**
- Testing the MQTT discovery path
- Replacing existing unit/integration tests
- CI automation (developer-triggered only)
- Mock server for Open-Meteo
- Testing HA options flow or sensor push mapping (future extension)

## Decisions

### 1. gRPC-only stack (no Mosquitto)

The E2E stack contains only njord + HA. No Mosquitto broker.

**Why**: The gRPC path is the strategic direction. Testing MQTT would require a third container and njord config changes (`Mqtt:Enabled=true`). Keep the stack minimal — MQTT E2E can be added later if needed.

**Alternative**: Include Mosquitto and test both paths. Rejected because MQTT may become obsolete, and testing two paths doubles the validation surface without clear value.

### 2. Real Open-Meteo API (no mock)

The E2E test hits the real Open-Meteo API.

**Why**: 2 requests per run (1 location × 2 models) is negligible against the 10k/day free-tier limit. Real API tests the full deserialization path and catches API changes. A mock server (WireMock container) would add maintenance burden for fixture responses.

**Alternative**: WireMock container with canned responses. Rejected for now — adds a third service and fixture maintenance. Can be revisited if the test becomes flaky due to API issues.

### 3. HA REST API for entity validation (not browser scraping)

Phases 2-6 use HA's REST API (`/api/states`, `/api/services`) rather than browser automation to validate entities.

**Why**: REST API is deterministic, fast, and produces structured data. Browser scraping HA dashboards would be brittle (HA redesigns its UI). Browser is used only where it's necessary: config flow (Phase 1) and trigger poll button (Phase 8).

**Alternative**: All validation through claude-in-chrome. Rejected — HA's dashboard layout changes across versions and entity counts make visual verification impractical.

### 4. HA long-lived access token for API auth

The skill creates a HA long-lived access token during first setup (via browser or HA CLI) and uses it for subsequent REST API calls.

**Why**: HA REST API requires authentication. A long-lived token avoids re-authenticating on every API call. The token is created as part of Phase 1 (initial setup) and stored for the duration of the run.

**Alternative**: Use HA's auth flow programmatically. Rejected — complex OAuth flow not worth automating for a single-run test.

### 5. Sibling directory assumption for ha-njord

The Docker Compose file assumes ha-njord is cloned at `../ha-njord` relative to the njord repo root (i.e., `D:\GIT\ha-njord` alongside `D:\GIT\njord`).

**Why**: Both repos are already at `D:\GIT\`. A relative path keeps the compose file portable across machines with different base paths. The skill validates the path exists before starting.

**Alternative**: Environment variable for ha-njord path. Could be added as a fallback but the sibling convention covers the primary use case.

### 6. njord as leading repo for e2e/ directory

The `e2e/` directory is maintained in njord and copied to ha-njord.

**Why**: njord builds the Docker image and defines the server-side config. The test plan is primarily about validating njord's output. ha-njord is the consumer — its copy is for convenience (running the test from either repo).

**Alternative**: Shared git submodule or separate e2e repo. Rejected — submodules add complexity, and a third repo is overkill for 3-4 files.

### 7. Haiku subagents for parallel validation

After entity registration (Phase 2), two Haiku subagents run in parallel: one for forecast + enrichment (Phase 3+4), one for connectivity + gRPC (Phase 5+6).

**Why**: These phases are read-only and independent — they don't modify state. Parallel execution saves time. Haiku is cost-effective for structured API validation. The Opus main agent re-validates any FAILs (Haiku can misinterpret responses).

**Alternative**: All sequential on Opus. Slower but simpler. The FunkArr model proves the parallel pattern works well.

## Risks / Trade-offs

- **[Open-Meteo flakiness]** → The real API could be slow or return errors. Mitigation: the first poll has a generous timeout (120s); if it fails, the run documents it as a FAIL with the API error, not a test infrastructure problem.
- **[HA startup time]** → HA takes 30-60s to fully boot. Mitigation: health polling with backoff; Phase 1 doesn't start until HA responds.
- **[ha-njord version mismatch]** → The mounted ha-njord code might not match the njord gRPC API if protos diverged. Mitigation: the config flow validates gRPC connectivity — a version mismatch shows up immediately in Phase 1.
- **[Docker required]** → The test only runs on machines with Docker. Mitigation: acceptable — this is a developer-triggered test, not CI. Document the requirement.
- **[HA image size]** → The HA stable image is ~1GB. Mitigation: it's cached after first pull. Not a blocker.

## Open Questions

- **HA onboarding**: Fresh HA instances require an onboarding flow (create user, set timezone) before integrations can be added. The skill needs to handle this — either via browser automation or by pre-seeding the config volume. To be resolved during first run.
- **Long-lived access token creation**: How to create the token programmatically (HA CLI inside the container vs. browser UI profile page). To be resolved during first run.
- **Timing baselines**: First run establishes baseline timings. Whether to enforce timing thresholds on subsequent runs is deferred.
