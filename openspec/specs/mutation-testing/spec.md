# mutation-testing Specification

## Purpose
TBD - created by archiving change mutation-testing-pure-logic. Update Purpose after archive.
## Requirements
### Requirement: Pure-logic projects are mutation tested
Stryker.NET SHALL be configured, through a pinned local tool, for the deterministic Akka-free libraries: Weather, Consensus (Compute), Alerts, Derived, Trends, Indices and the pure parts of Core. It SHALL use the Microsoft.Testing.Platform runner with per-test coverage analysis and SHALL NOT mutate actors, streams, Grpc, Mqtt or the host.

#### Scenario: Run a project locally
- **WHEN** a developer runs the documented Stryker command for `Njord.Alerts`
- **THEN** the run completes against `Njord.Alerts.Tests`, reports killed, survived and no-coverage counts, and writes an HTML report under `StrykerOutput/`

#### Scenario: Out-of-scope project
- **WHEN** the configuration list is inspected
- **THEN** no config exists for Pipeline, Mqtt, Grpc, OpenMeteo, Budget, SensorHub or the host

### Requirement: Mutation runs never gate pull requests
Mutation runs SHALL execute in a scheduled and manually dispatched workflow, upload per-project reports as artifacts, and SHALL NOT be a required check. A `break` threshold SHALL be introduced only after the baseline of a project has been reviewed and recorded in the documentation.

#### Scenario: Pull request
- **WHEN** a pull request is opened
- **THEN** no mutation job runs

#### Scenario: Scheduled run
- **WHEN** the scheduled workflow runs
- **THEN** one report artifact per mutated project is uploaded and the job summary lists the mutation score per project

### Requirement: Excluded mutants are justified
Every mutant or code region excluded from mutation SHALL carry a reason in the Stryker configuration or a comment, and equivalent mutants SHALL NOT be fixed by weakening production code.

#### Scenario: Equivalent mutant
- **WHEN** a surviving mutant is judged equivalent
- **THEN** the exclusion entry names the file, the mutator and the reason

