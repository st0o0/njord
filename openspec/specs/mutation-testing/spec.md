# mutation-testing Specification

## Purpose
TBD - created by archiving change mutation-testing-pure-logic. Update Purpose after archive.
## Requirements
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

