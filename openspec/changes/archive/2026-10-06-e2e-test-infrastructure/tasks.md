## 1. Docker Compose Stack

- [x] 1.1 Create `e2e/docker-compose.e2e.yml` with njord service (build from repo root Dockerfile, ports 8080+8081, 1 location lucerne, 2 models icon_d2+ecmwf_ifs025, all 6 enrichments enabled) and HA service (stable image, port 8123, ha-njord volume-mounted from `../../ha-njord/custom_components/njord`)
- [x] 1.2 Validate stack starts cleanly: `docker compose -f e2e/docker-compose.e2e.yml up -d --build`, verify both containers reach healthy state, then `docker compose -f e2e/docker-compose.e2e.yml down -v`

## 2. E2E Test Plan Document

- [x] 2.1 Create `e2e/E2E-TEST-PLAN.md` with all 8 phases as numbered steps. Phase 0: stack setup (down -v, build, up, health poll). Phase 1: HA onboarding + njord integration config flow via browser. Phase 2: entity registration polling via HA REST API
- [x] 2.2 Continue test plan: Phase 3: forecast data validation (weather entity attributes, hourly forecast service). Phase 4: enrichment entity validation (14 alerts, 11 indices, trend, 5 derived, history — with expected entity IDs derived from config)
- [x] 2.3 Continue test plan: Phase 5: connectivity + server entities (3 stream binary_sensors, version, uptime, button). Phase 6: direct gRPC validation (OpsService.GetStatus). Phase 7: resilience (docker restart → reconnect). Phase 8: trigger poll via browser
- [x] 2.4 Add orchestration section: Opus main agent runs Phase 0-2 and 7-8 sequentially; Haiku #1 runs Phase 3+4, Haiku #2 runs Phase 5+6 in parallel; main agent re-checks Haiku FAILs. Add results format section: `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with Phase/Step/Result/Detail table, no SKIP allowed

## 3. Claude Code Skill

- [x] 3.1 Create the E2E test skill definition (`.claude/skills/e2e-test.md` or equivalent) that reads `e2e/E2E-TEST-PLAN.md` and orchestrates execution: Docker lifecycle, browser automation via claude-in-chrome, HA REST API calls, subagent spawning
- [x] 3.2 Skill handles HA onboarding (fresh HA instance requires user creation + timezone) — document the browser automation steps for first-time setup
- [x] 3.3 Skill handles HA long-lived access token creation (via HA profile page in browser) for REST API authentication in subsequent phases
- [x] 3.4 Skill spawns two Haiku subagents in parallel after Phase 2, collects results, re-validates FAILs, and writes the results document to `e2e/results/`

## 4. Cross-Repo Sync

- [x] 4.1 Copy `e2e/` directory (docker-compose.e2e.yml + E2E-TEST-PLAN.md) to `D:\GIT\ha-njord\e2e\`
- [x] 4.2 Add the E2E skill definition to ha-njord so the test can be invoked from either repo
- [x] 4.3 Add `e2e/results/` to `.gitignore` in both repos (results are per-run, not committed)

## 5. First Run & Documentation

- [x] 5.1 Execute the E2E skill for the first time, documenting: stack startup duration, HA onboarding steps, time from integration setup to first entities, total entity count and IDs
- [x] 5.2 Review first-run results, refine test plan timing expectations and entity ID list based on observed behavior
- [x] 5.3 Update `e2e/E2E-TEST-PLAN.md` with baseline timing data and confirmed entity expectations from the first run

## Validation

No production code is changed — validation is the successful first E2E run (task 5.1) producing a PASS/FAIL results document. Stack health is verified in task 1.2. The skill is verified by its execution in task 5.1.
