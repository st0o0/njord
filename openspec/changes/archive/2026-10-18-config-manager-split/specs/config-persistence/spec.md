## ADDED Requirements

### Requirement: Config write logic lives in a dedicated handler class

The system SHALL provide a `ConfigUpdateHandler` class that owns `IConfigWriter` and the validation + persistence logic for config updates. `ConfigManager` SHALL delegate `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, and `UpdateBudget` messages to this handler. The handler SHALL be a plain sealed class, not an actor.

#### Scenario: Write delegation preserves behavior
- **WHEN** `ConfigManager` receives an `UpdateLocations` message
- **THEN** it SHALL delegate to `ConfigUpdateHandler` which validates and writes, and the response SHALL be identical to the current behavior

#### Scenario: Reload triggers automatically after write
- **WHEN** `ConfigUpdateHandler` writes an override via `IConfigWriter`
- **THEN** `IOptionsMonitor` change callbacks SHALL fire automatically (file-system watcher), and `ConfigManager` SHALL re-read and propagate without explicit notification from the handler

#### Scenario: ConfigManager no longer depends on IConfigWriter
- **WHEN** `ConfigManager` is constructed
- **THEN** it SHALL NOT receive `IConfigWriter` as a constructor parameter — that dependency moves to `ConfigUpdateHandler`
