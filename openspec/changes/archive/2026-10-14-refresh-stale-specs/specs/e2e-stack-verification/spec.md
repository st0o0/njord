## MODIFIED Requirements

### Requirement: The e2e-test skill runs the real stack via Docker Compose
The `e2e-test` Claude Code skill SHALL start njord and Home Assistant (with the ha-njord integration mounted from the sibling `../ha-njord` checkout) via `e2e/docker-compose.e2e.yml`, and poll both njord's `/alive` and Home Assistant's root endpoint until healthy before proceeding. The stack has no MQTT broker; Home Assistant talks to njord over gRPC.

#### Scenario: Stack comes up healthy
- **WHEN** the skill runs Phase 0 (Stack Setup)
- **THEN** `docker compose -f e2e/docker-compose.e2e.yml up -d --build` SHALL be followed by njord `/alive` returning 200 within 60s and Home Assistant returning 200/302 within 120s

### Requirement: Verification runs in parallel subagents with a FAIL re-check pass
The skill SHALL spawn three subagents in parallel (in a single message) to verify different groups against the HA REST API and the gRPC API, then re-run any reported FAIL itself before accepting it, to distinguish a subagent misinterpretation from a real bug.

#### Scenario: Parallel verification covers forecast, enrichment, connectivity, and gRPC
- **WHEN** entities have registered
- **THEN** subagent #1 verifies forecast and enrichment entities, #2 verifies connectivity/server-diagnostic entities and the gRPC API, and #3 verifies error handling, each reporting PASS/FAIL per step

#### Scenario: Reported FAIL is independently re-checked
- **WHEN** a subagent reports a FAIL
- **THEN** the orchestrating run SHALL re-run that specific check itself before the result is written as FAIL
