## MODIFIED Requirements

### Requirement: Claude Code skill to execute E2E tests

A Claude Code skill SHALL exist that, when invoked, orchestrates the full E2E test run against the Docker stack, covering ~25 sections with ~150+ test steps.

#### Scenario: Skill invocation
- **WHEN** the user invokes the E2E skill (e.g., `/e2e-test` or equivalent)
- **THEN** the skill reads the E2E test plan, starts the Docker stack, and executes all phases including gRPC API coverage, entity depth, enrichment validation, error handling, multi-cycle, config scenarios, browser verification, resilience, and teardown

#### Scenario: Docker lifecycle management
- **WHEN** the skill starts
- **THEN** it runs `docker compose -f e2e/docker-compose.e2e.yml down -v` followed by `docker compose -f e2e/docker-compose.e2e.yml up -d --build` and polls for health

#### Scenario: Browser automation
- **WHEN** the skill executes browser phases (HA onboarding, entity card verification, trigger poll)
- **THEN** it uses claude-in-chrome tools to interact with the HA web UI

#### Scenario: HA REST API validation
- **WHEN** the skill executes entity and data validation phases
- **THEN** it uses HTTP calls against HA REST API (`http://localhost:8123/api/`) with a long-lived access token

#### Scenario: gRPC validation
- **WHEN** the skill executes gRPC API coverage phases
- **THEN** it uses `grpcurl` to call all 16 RPCs and verify responses

### Requirement: Parallel subagent orchestration

The skill SHALL spawn three Haiku subagents for independent validation phases after entity registration completes.

#### Scenario: Three-way parallel spawn
- **WHEN** Phase 2 completes (entities registered)
- **THEN** the skill spawns three Haiku subagents in a single message: #1 for forecast + enrichment validation, #2 for connectivity + server + gRPC API, #3 for error handling

#### Scenario: Subagent briefing includes full context
- **WHEN** a Haiku subagent is spawned
- **THEN** it receives the HA base URL, the long-lived access token, its assigned phases with all test steps, and the expected entity list

#### Scenario: Subagent result validation
- **WHEN** subagents return results with FAILs
- **THEN** the main agent re-checks each FAIL before accepting it into the final results

### Requirement: Sequential post-parallel phases

The skill SHALL execute config-mutation, multi-cycle, browser, resilience, and teardown phases sequentially after parallel validation completes.

#### Scenario: Config scenario execution
- **WHEN** parallel validation phases complete
- **THEN** Opus executes config scenario tests (AdminService mutations, entity set verification) sequentially, restoring default config between scenarios

#### Scenario: Multi-cycle execution
- **WHEN** config scenarios complete with config restored to default
- **THEN** Opus triggers additional poll cycles and verifies data freshness, usage counters, and budget tracking

#### Scenario: Teardown as final phase
- **WHEN** all other phases complete
- **THEN** the skill removes the njord integration from HA, verifies entity cleanup, and runs `docker compose down -v`

### Requirement: Results document generation

The skill SHALL produce a structured results document after each run.

#### Scenario: Output file
- **WHEN** the test run completes
- **THEN** the skill writes `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with a summary table of all phases and steps

#### Scenario: Timing documentation
- **WHEN** the first run executes
- **THEN** the skill records timing data: stack startup duration, time to first entity, reconnect duration after restart

#### Scenario: Step count validation
- **WHEN** the results document is written
- **THEN** the total step count matches the test plan (no steps omitted or skipped)

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
