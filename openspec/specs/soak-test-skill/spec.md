## Requirements

### Requirement: Soak test skill lifecycle

The agent skill SHALL orchestrate the full soak test lifecycle in three phases: setup (stack start + HA onboarding + collector launch), wait (collector runs unattended for the configured duration), and evaluation (read JSONL, apply pass/fail matrix, generate report).

#### Scenario: Full lifecycle execution

- **WHEN** the user invokes the soak-test skill
- **THEN** the agent starts the Docker stack with soak compose override, completes HA onboarding (reusing E2E S0-S2 flow), starts the collector script in background, and instructs the user to re-invoke for evaluation when the collector completes

### Requirement: Reuse E2E stack setup

The skill SHALL reuse the existing E2E test's stack setup flow (S0: compose up, S1: HA onboarding + token, S2: integration setup, S3: entity registration wait) with the soak compose override applied.

#### Scenario: Stack setup with soak override

- **WHEN** the skill starts the stack
- **THEN** it uses `docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up -d --build` and follows the same HA onboarding and integration setup steps as the E2E test

### Requirement: JSONL evaluation

The skill SHALL read `soak-results.jsonl` and evaluate each of the 19 pass/fail criteria defined in the test plan. The evaluation SHALL produce a structured report with: overall result (PASS/WARN/FAIL), per-check results with evidence, and a summary of the restart recovery.

#### Scenario: Clean run evaluation

- **WHEN** the agent evaluates a JSONL file where all checks pass
- **THEN** the report shows overall PASS with 19/19 checks passed and a recovery summary

#### Scenario: Failed check evaluation

- **WHEN** the agent evaluates a JSONL file where entity count changed between ticks
- **THEN** the report shows overall FAIL, identifies check E1 as failed, and cites the specific ticks and counts as evidence

### Requirement: Evaluation-only mode

The skill SHALL support being invoked after the collector has already completed, reading existing output files without re-running the stack.

#### Scenario: Post-hoc evaluation

- **WHEN** the user invokes the skill and `soak-results.jsonl` already exists in the output directory
- **THEN** the skill offers to evaluate the existing results instead of starting a new run
