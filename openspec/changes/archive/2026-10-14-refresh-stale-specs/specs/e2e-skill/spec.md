## MODIFIED Requirements

### Requirement: Cross-repo sync
The E2E infrastructure SHALL be maintained in njord (leading repo) and synced to ha-njord. Paths SHALL be given relative to the njord repository root, with ha-njord cloned as a sibling directory (`../ha-njord`).

#### Scenario: Leading repo
- **WHEN** changes are made to `e2e/` files
- **THEN** they are made in the njord repo first

#### Scenario: Sync to ha-njord
- **WHEN** the E2E infrastructure is updated in njord
- **THEN** the identical `e2e/` directory (test plan, compose file) is copied to `../ha-njord/e2e/`

#### Scenario: Skill availability
- **WHEN** the E2E skill is defined
- **THEN** it is available in both repos (njord and ha-njord) so the test can be triggered from either working directory
