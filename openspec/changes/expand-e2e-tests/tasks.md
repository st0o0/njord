## 1. Restructure Test Plan — Section Framework

- [x] 1.1 Rewrite `e2e/E2E-TEST-PLAN.md` header and Stack section: keep the existing Stack table and Configuration but rename "Phase 0–8" to "Section 0–24" structure as defined in the design (S0–S24 dependency graph). Add a section index at the top listing all ~25 sections with one-line descriptions.
- [x] 1.2 Rewrite the Expected Entities table in `e2e/E2E-TEST-PLAN.md`: keep the same entity grid (~47 entities) but add columns for `device_class`, `state_class`, `unit_of_measurement`, and expected value ranges per entity type (weather conditions list, alert severity enum, index 0–10 range, etc.).
- [x] 1.3 Write S0 (Stack Setup) and S1 (HA Onboarding + Token) sections in `e2e/E2E-TEST-PLAN.md` — carry over from existing Phase 0 and Phase 1 with no functional changes, only renumbered step IDs.
- [x] 1.4 Write S2 (Integration Setup) and S3 (Entity Registration) sections — carry over from existing Phase 1C and Phase 2, renumbered.

## 2. gRPC API Coverage Sections (S4–S6, S10)

- [x] 2.1 Write S4 (WeatherService: GetCatalog) in `e2e/E2E-TEST-PLAN.md`: step table with grpcurl commands verifying response contains location "lucerne", 2 models, all enrichment features listed. Reference `e2e-grpc-coverage` spec scenarios.
- [x] 2.2 Write S5 (WeatherService: GetForecast) in `e2e/E2E-TEST-PLAN.md`: steps for calling GetForecast with location "lucerne" + model "icon_d2", verifying hourly entries with numeric temperature/humidity/wind_speed and timestamps within horizon range.
- [x] 2.3 Write S6 (WeatherService: GetEnrichments) in `e2e/E2E-TEST-PLAN.md`: steps for calling GetEnrichments with location "lucerne", verifying enrichment results for all 6 enabled features.
- [x] 2.4 Write S10 (OpsService: Full Coverage) in `e2e/E2E-TEST-PLAN.md`: steps for GetStatus (version, locations, models), GetTargets (target entries with next-poll timestamps), TriggerPoll (verify success response). Carry over existing Phase 6 gRPC steps and add GetTargets.

## 3. Entity Attribute Depth Section (S7)

- [x] 3.1 Write S7 (Entity Attribute Depth) in `e2e/E2E-TEST-PLAN.md`: step table validating weather entity attributes (temperature_unit=°C, wind_speed_unit=m/s, state is valid HA condition), alert sensor attributes (severity enum, confidence 0–100), index sensor values (0–10 range), derived sensor values (beaufort 0–12, sunshine unit=h), server sensor values (version semver, usage unit=requests), binary sensor states (on/off). Reference `e2e-entity-depth` spec scenarios. ~15 steps.

## 4. Enrichment Validation Sections (within S7 or as sub-steps)

- [x] 4.1 Add enrichment computation validation steps to S7 in `e2e/E2E-TEST-PLAN.md`: consensus agreement (0–100%), spread (non-negative °C), models_used (≥2), consensus temperature within model range. Reference `e2e-enrichment-validation` spec.
- [x] 4.2 Add alert severity plausibility steps: at least one alert has non-none severity, confidence correlates with severity. Add trend sensor state (non-empty descriptive string), history sensor state (not unavailable). ~8 steps.

## 5. Connectivity and Server Entities Section (S8–S9)

- [x] 5.1 Write S8 (Connectivity Entities) in `e2e/E2E-TEST-PLAN.md`: verify 3 stream binary_sensors are "on", inversion sensor is "on"/"off". Carry over from existing Phase 5 steps 5.1–5.3.
- [x] 5.2 Write S9 (Server Entities) in `e2e/E2E-TEST-PLAN.md`: verify version (semver), uptime (duration), monthly_usage (numeric, unit=requests), daily_usage (numeric, unit=requests), button.trigger_poll exists, 2 target sensors exist. Carry over from existing Phase 5 steps 5.4–5.9.

## 6. Error Handling Section (S11)

- [x] 6.1 Write S11 (gRPC Error Handling) in `e2e/E2E-TEST-PLAN.md`: step table with grpcurl commands for invalid requests — GetForecast with unknown location (expect NOT_FOUND/INVALID_ARGUMENT), GetForecast with invalid model, Push with SENSOR_KIND_UNSPECIFIED, SetLocations with empty list. Reference `e2e-error-handling` spec. ~6 steps.
- [x] 6.2 Add HA REST API error steps to S11: GET non-existent entity (expect 404), POST invalid service call (expect error, not crash). ~2 steps.

## 7. HA Browser Verification Sections (S12–S14)

- [x] 7.1 Write S12 (HA Browser: Weather Cards) in `e2e/E2E-TEST-PLAN.md`: use claude-in-chrome to navigate to a weather entity card, verify temperature/humidity/wind displayed with condition icon. ~3 steps.
- [x] 7.2 Write S13 (HA Browser: Enrichment Cards) in `e2e/E2E-TEST-PLAN.md`: navigate to Developer Tools → States, inspect an enrichment entity (e.g., consensus), verify custom attributes visible (agreement, spread, models_used). Filter by "lucerne", verify entity count. ~3 steps.
- [x] 7.3 Write S14 (HA Browser: Server Entities) in `e2e/E2E-TEST-PLAN.md`: inspect server entities in Developer Tools, verify version string and usage values display. ~2 steps.

## 8. SensorService Section (S15)

- [x] 8.1 Write S15 (SensorService: Push + StreamPush) in `e2e/E2E-TEST-PLAN.md`: steps for Push with valid SensorReading (INDOOR_TEMPERATURE, 21.5) via grpcurl, verify success response. StreamPush with multiple readings, verify success. ~3 steps.

## 9. Multi-Cycle Sections (S16–S17)

- [x] 9.1 Write S16 (Multi-Cycle: TriggerPoll × 2) in `e2e/E2E-TEST-PLAN.md`: record last_updated on weather entity, call TriggerPoll, poll until last_updated advances (timeout 120s, poll 5s), repeat for second cycle. Verify enrichment timestamps also update. ~5 steps.
- [x] 9.2 Write S17 (Budget Tracking Across Cycles) in `e2e/E2E-TEST-PLAN.md`: read daily_usage before and after TriggerPoll, verify increment. Compare OpsService/GetStatus budget fields to HA sensor values. ~4 steps.

## 10. AdminService and Config Scenario Sections (S18–S22)

- [x] 10.1 Write S18 (AdminService: GetConfig + StreamConfig) in `e2e/E2E-TEST-PLAN.md`: call GetConfig, verify locations/models/enrichments/budget match Docker config. Open StreamConfig (bounded by `timeout 30`), apply a SetSettings mutation, verify StreamConfig delivers updated config. ~4 steps.
- [x] 10.2 Write S19 (Config Mutation: Disable Alerts) in `e2e/E2E-TEST-PLAN.md`: call SetEnrichment disabling alerts, verify GetConfig reflects change. Poll HA REST API until 14 alert sensors become unavailable or disappear (timeout 60s). ~3 steps.
- [x] 10.3 Write S20 (Config Mutation: Verify Entity Removal) in `e2e/E2E-TEST-PLAN.md`: verify total entity count dropped by ~14, verify specific alert entity IDs are gone. ~2 steps.
- [x] 10.4 Write S21 (Config Mutation: Re-enable Alerts) in `e2e/E2E-TEST-PLAN.md`: call SetEnrichment re-enabling alerts, trigger a poll, poll HA until alert sensors reappear (timeout 120s). Verify count restored. ~3 steps.
- [x] 10.5 Write S22 (Config Mutation: SetSettings — Change Horizons) in `e2e/E2E-TEST-PLAN.md`: call SetSettings changing horizons, verify GetConfig reflects change, trigger a poll, verify forecast entries reflect new horizon set. Restore default horizons afterwards. ~4 steps.

## 11. Streaming RPC Section (within parallel block)

- [x] 11.1 Add streaming RPC steps to S4 or a dedicated sub-section: StreamForecasts (open, trigger poll, verify at least one ForecastUpdate within 120s, kill stream), StreamEnrichments (same pattern). Use `timeout 120 grpcurl ...` to bound the wait. ~4 steps.

## 12. Resilience Section (S23)

- [x] 12.1 Write S23 (Resilience: Container Restart) in `e2e/E2E-TEST-PLAN.md`: carry over existing Phase 7 steps with additions — verify entity states show "unavailable" during downtime (stop container for 30s, check HA), then restart and poll for recovery. ~6 steps.

## 13. Teardown Section (S24)

- [x] 13.1 Write S24 (Teardown: Integration Removal) in `e2e/E2E-TEST-PLAN.md`: use browser to remove njord integration via Settings → Devices & Services → Delete. Poll HA REST API until all njord entities disappear (timeout 30s). Verify no orphaned entities. Run `docker compose down -v`. ~5 steps.

## 14. Orchestration and Setup Recipes

- [x] 14.1 Rewrite the Orchestration diagram in `e2e/E2E-TEST-PLAN.md`: ASCII art showing S0–S3 sequential, then 3-way parallel block (Haiku #1: S4–S7, Haiku #2: S8–S11, Haiku #3: S12–S14), then S15–S24 sequential by Opus.
- [x] 14.2 Write Subagent Briefing section in `e2e/E2E-TEST-PLAN.md`: prompt templates for each of the 3 Haiku subagents — assigned sections, HA URL, token, expected entity list, output format (PASS/FAIL per step with detail).
- [x] 14.3 Write Setup Recipes appendix in `e2e/E2E-TEST-PLAN.md`: document default recipe (baseline config), single-model recipe (AdminService mutation), disabled-enrichments recipe (AdminService mutation), changed-horizons recipe (AdminService mutation). Each recipe lists the grpcurl commands to apply and restore it.
- [x] 14.4 Update the Results Format section in `e2e/E2E-TEST-PLAN.md`: same PASS/FAIL table format, update expected step count to ~150+, add timing baselines for new phases (config mutation settle time, multi-cycle duration, teardown duration).

## 15. Update E2E Skill

- [x] 15.1 Rewrite `.claude/skills/e2e-test/SKILL.md`: update Execution section to reference ~25 sections instead of 8 phases. Update Phase 0–2 step IDs to match new numbering.
- [x] 15.2 Update the Parallel Phase section in `.claude/skills/e2e-test/SKILL.md`: change from 2 Haiku subagents to 3 — Haiku #1 (S4–S7: Weather + Entities), Haiku #2 (S8–S11: Connectivity + Ops + Errors), Haiku #3 (S12–S14: HA Browser). Include updated prompt templates.
- [x] 15.3 Add sequential post-parallel phases to `.claude/skills/e2e-test/SKILL.md`: S15 (SensorService), S16–S17 (Multi-Cycle + Budget), S18–S22 (AdminService + Config Scenarios), S23 (Resilience), S24 (Teardown). Document config restore between scenario steps.
- [x] 15.4 Update the Write Results section in `.claude/skills/e2e-test/SKILL.md`: add new phases to the results template, update expected step count, add notes on config scenario results.
- [x] 15.5 Update the skill description/frontmatter in `.claude/skills/e2e-test/SKILL.md`: mention ~150+ steps, 3 subagents, config scenarios, teardown.

## 16. Cross-Repo Sync

- [x] 16.1 Copy updated `e2e/E2E-TEST-PLAN.md` to `D:\GIT\ha-njord\e2e\E2E-TEST-PLAN.md`.
- [x] 16.2 Copy updated `e2e/docker-compose.e2e.yml` to `D:\GIT\ha-njord\e2e\docker-compose.e2e.yml` (if changed).

## 17. Validation

- [x] 17.1 Review `e2e/E2E-TEST-PLAN.md` for section count (target: ~25 sections), step count (target: ~150+ steps), and completeness against all 10 spec files.
- [x] 17.2 Review `.claude/skills/e2e-test/SKILL.md` for consistency with the updated test plan — all sections referenced, 3-subagent orchestration, config scenario handling.
- [x] 17.3 Run `openspec validate --all --no-interactive` from repo root to verify change artifacts are valid.
- [x] 17.4 Run the full E2E test via `/e2e-test` skill invocation and verify all new sections execute. Record results in `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md`.
