## ADDED Requirements

### Requirement: Claude Code skill to execute E2E tests

A Claude Code skill SHALL exist that, when invoked, orchestrates the full E2E test run against the Docker stack.

#### Scenario: Skill invocation
- **WHEN** the user invokes the E2E skill (e.g., `/e2e-test` or equivalent)
- **THEN** the skill reads the E2E test plan, starts the Docker stack, and executes all phases

#### Scenario: Docker lifecycle management
- **WHEN** the skill starts
- **THEN** it runs `docker compose -f e2e/docker-compose.e2e.yml down -v` followed by `docker compose -f e2e/docker-compose.e2e.yml up -d --build` and polls for health

#### Scenario: Browser automation
- **WHEN** the skill executes Phase 1 (HA integration setup) or Phase 8 (trigger poll)
- **THEN** it uses claude-in-chrome tools to interact with the HA web UI

#### Scenario: HA REST API validation
- **WHEN** the skill executes Phase 2-5 (entity and data validation)
- **THEN** it uses HTTP calls against HA REST API (`http://localhost:8123/api/`) with a long-lived access token

### Requirement: Parallel subagent orchestration

The skill SHALL spawn Haiku subagents for independent validation phases after entity registration completes.

#### Scenario: Parallel spawn
- **WHEN** Phase 2 completes (entities registered)
- **THEN** the skill spawns two Haiku subagents in a single message: one for Phase 3+4 (forecast + enrichment), one for Phase 5+6 (connectivity + gRPC)

#### Scenario: Subagent result validation
- **WHEN** subagents return results with FAILs
- **THEN** the main agent re-checks each FAIL before accepting it into the final results

### Requirement: Results document generation

The skill SHALL produce a structured results document after each run.

#### Scenario: Output file
- **WHEN** the test run completes
- **THEN** the skill writes `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with a summary table of all phases and steps

#### Scenario: Timing documentation
- **WHEN** the first run executes
- **THEN** the skill records timing data: stack startup duration, time to first entity, reconnect duration after restart

### Requirement: Cross-repo sync

The E2E infrastructure SHALL be maintained in njord (leading repo) and synced to ha-njord.

#### Scenario: Leading repo
- **WHEN** changes are made to `e2e/` files
- **THEN** they are made in the njord repo first

#### Scenario: Sync to ha-njord
- **WHEN** the E2E infrastructure is updated in njord
- **THEN** the identical `e2e/` directory (test plan, compose file) is copied to ha-njord at `D:\GIT\ha-njord\e2e\`

#### Scenario: Skill availability
- **WHEN** the E2E skill is defined
- **THEN** it is available in both repos (njord and ha-njord) so the test can be triggered from either working directory
