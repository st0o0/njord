## MODIFIED Requirements

### Requirement: Atomic file write
The override file SHALL be written atomically: write to a temporary file in the same directory, flush it to disk, then rename/move it to the target path. This prevents partial reads by the file watcher and ensures njord itself never leaves a half-written override file behind.

#### Scenario: Crash during write does not corrupt config
- **WHEN** the process crashes during a write operation
- **THEN** the previous override file (or absence of one) SHALL remain intact

#### Scenario: Successful write leaves no temporary file
- **WHEN** a mutation is persisted
- **THEN** only `data/appsettings.Override.json` SHALL hold the new content and no temporary file SHALL remain in `data/`

## ADDED Requirements

### Requirement: Writer reads every file the configuration loader accepts
The writer SHALL read the existing override file with the same JSON leniency as the configuration loader (comments and trailing commas are accepted), so a hand-edited file that njord loads can also be updated by a mutation. Comments are not preserved when the file is rewritten.

#### Scenario: Hand-edited file with comments is updated
- **WHEN** the override file contains a `//` comment and a trailing comma and sets `ForecastDays = 7`
- **AND** a mutation sets `PollInterval = 1800s`
- **THEN** the mutation SHALL be applied and the new file SHALL contain both `ForecastDays = 7` and `PollInterval = 1800s`
