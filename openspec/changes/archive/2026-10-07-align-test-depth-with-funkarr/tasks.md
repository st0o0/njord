> Re-audited 2026-10-07 against actual code: section 2 is genuinely
> implemented. Section 1's `NjordFixture`/`Njord.IntegrationTests` was built,
> then deliberately deleted in a later, separate decision the same day (the
> host moved to domain-specific Servus setup containers and sharded snapshot
> actors) — not reinstated here. Sections 3-5's `Njord.E2E.Tests` tasks had
> been checked off without that project ever existing; the change is rescoped
> below to describe the `e2e-test` skill that was actually built instead.

## 1. Integration Test Fixture (built, then retired — see note above)

- [x] 1.1 ~~Create `NjordFixture`~~ — built, later deleted with the rest of `Njord.IntegrationTests` when the host moved to sharded actors
- [x] 1.2 ~~Create collection definitions~~ — built, later deleted alongside 1.1
- [x] 1.3 ~~Migrate `HealthEndpointSpec`~~ — built, later deleted alongside 1.1 (the file no longer exists anywhere)
- [x] 1.4 ~~Add gRPC integration tests~~ — built, later deleted alongside 1.1

## 2. Persistence Roundtrip Tests

- [x] 2.1 Add roundtrip test helper in `src/Njord.Persistence.Tests/PersistenceRoundtripHelper.cs` — static method `AssertRoundtrip<T>(T dto)` that serializes with `JsonConvert.SerializeObject(dto, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })`, deserializes, and asserts field-level equality (lives in `Njord.Persistence.Tests`, not `Njord.Tests.Shared` — it's used only there, and `test-project-structure` requires shared-project files to be used by at least two projects)
- [x] 2.2 Add roundtrip tests in `src/Njord.Persistence.Tests/BudgetTrackerDtoRoundtripSpec.cs` — `BudgetTrackerSnapshotDto_roundtrip()` and `ApiCallRecordedDto_roundtrip()` with representative field values
- [x] 2.3 Add roundtrip tests in `src/Njord.Persistence.Tests/SchedulerDtoRoundtripSpec.cs` — `SchedulerSnapshotDto_roundtrip()` and `DataChangedDto_roundtrip()` with nested `ModelPollStateDto`
- [x] 2.4 Add roundtrip tests in `src/Njord.Persistence.Tests/ForecastSnapshotDtoRoundtripSpec.cs` — `ForecastSnapshotDto_roundtrip()` with hourly and daily forecast points
- [x] 2.5 Add roundtrip tests in `src/Njord.Persistence.Tests/EnrichmentSnapshotDtoRoundtripSpec.cs` — `EnrichmentSnapshotDto_roundtrip()` with enrichment entries
- [x] 2.6 Add roundtrip tests in `src/Njord.Persistence.Tests/ForecastHistoryDtoRoundtripSpec.cs` — `ForecastHistorySnapshotDto_roundtrip()` and `ForecastRecordDto_roundtrip()`

## 3. End-to-end stack verification (rescoped from a Njord.E2E.Tests Testcontainers project)

- [x] 3.1 Build an agent-orchestrated E2E test instead of a `Njord.E2E.Tests` project — `.claude/skills/e2e-test/SKILL.md` + `e2e/E2E-TEST-PLAN.md`, driving a real Docker Compose stack (njord + ha-njord + Home Assistant + Mosquitto)
- [x] 3.2 Stack lifecycle via `e2e/docker-compose.e2e.yml` (`docker compose up -d --build` / `down -v`), health-polled before use
- [x] 3.3 HA onboarding + long-lived token creation + njord integration setup via browser automation (claude-in-chrome)
- [x] 3.4 Entity registration verified via HA's real REST API (`/api/states`), not a simulated MQTT subscriber

## 4. E2E Test Scenarios (rescoped from Verify-snapshotted MQTT payload specs)

- [x] 4.1 Forecast data + weather entity states verified against the real HA REST API (Phase 3 of the test plan)
- [x] 4.2 Enrichment entities (alerts, indices, trends, derived, history) verified against real HA entity states (Phase 4)
- [x] 4.3 Connectivity/server diagnostic entities and direct gRPC calls verified (Phases 5-6)
- [x] 4.4 Resilience: container restart, reconnect, and entity-availability recovery verified (Phase 7)
- [x] 4.5 Manual poll trigger verified via HA service call / button press, checking `last_updated` advances (Phase 8)

Not covered by the skill (acknowledged gap vs. the original Testcontainers proposal): byte-exact MQTT discovery/state payload regression via Verify snapshots, multi-model consensus payload detail, and HA birth/re-discovery on `homeassistant/status`. These would need the skill extended or a separate, narrower proposal — not blocking this change.

## 5. Cleanup and Validation

- [x] 5.1 Remove `[Collection("HostIntegration")]` from specs that are migrated to the new fixture pattern; delete stale `WebApplicationFactory` usage (confirmed: zero references to either anywhere in `src/`)
- [x] 5.2 Update `AGENTS.md` test counts and solution structure — removed the stale `Njord.IntegrationTests` row and paragraph (deleted project), corrected the total to 817 tests across 12 projects
- [x] 5.3 Add E2E test run instructions to `AGENTS.md` — points to the `e2e-test` skill and `e2e/E2E-TEST-PLAN.md` instead of a `dotnet test` project

## Validation

```bash
# From src/
dotnet build Njord.slnx
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
# E2E (requires Docker + Chrome): invoke the e2e-test skill
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
