## MODIFIED Requirements

### Requirement: NjordOptions is the single options root

`NjordOptions` SHALL be the only options type registered via `IOptions<>`. All configuration sub-sections (Mqtt, Grpc, Enrichment, Sensors, Persistence, Parameters) SHALL be nested properties on `NjordOptions`. There SHALL be no independent `IOptions<EnrichmentOptions>` or `IOptions<SensorOptions>` registrations. All consumers SHALL inject `IOptions<NjordOptions>` and access sub-sections via the nested property path.

#### Scenario: Enrichment feature uses NjordOptions

- **WHEN** `IndexEnrichment` needs enrichment configuration
- **THEN** it SHALL inject `IOptions<NjordOptions>` and access `.Value.Enrichment.Indices`

#### Scenario: Sensor consumer uses NjordOptions

- **WHEN** `SensorHubActor` needs sensor configuration
- **THEN** it SHALL inject `IOptions<NjordOptions>` and access `.Value.Sensors`

#### Scenario: No independent enrichment options registration

- **WHEN** the DI container is inspected
- **THEN** there SHALL be no `IOptions<EnrichmentOptions>` or `IOptions<SensorOptions>` service registrations

### Requirement: SensorOptions is a nested property on NjordOptions

`NjordOptions` SHALL contain a `Sensors` property of type `SensorOptions` with a default value of `new()`. Config path `Njord:Sensors` SHALL bind to this nested property.

#### Scenario: SensorOptions bound through NjordOptions

- **WHEN** config JSON contains `"Njord": { "Sensors": { "Enabled": false } }`
- **THEN** `IOptions<NjordOptions>.Value.Sensors.Enabled` SHALL be `false`

### Requirement: All validators implement IValidateOptions of NjordOptions

All configuration validators (`NjordOptionsValidator`, `ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`, `SensorOptionsValidator`) SHALL implement `IValidateOptions<NjordOptions>`. They SHALL access enrichment config via `options.Enrichment.*` and sensor config via `options.Sensors.*`.

#### Scenario: IndexOptionsValidator validates through NjordOptions

- **WHEN** `IndexOptionsValidator` validates index preferences
- **THEN** it SHALL receive `NjordOptions` directly and access `.Enrichment.Indices` and `.Locations` without injecting a separate `IOptions<NjordOptions>`

### Requirement: IndexOptionsValidator does not mutate options

`IndexOptionsValidator` SHALL NOT modify option values during validation. If a sensitivity value is outside the range [0.0, 5.0], the validator SHALL return a validation failure. `PreferenceResolver.ClampSensitivity` handles clamping at resolution time.

#### Scenario: Out-of-range sensitivity fails validation

- **WHEN** `HeatSensitivity` is set to 8.0
- **THEN** validation SHALL fail with a message indicating the value is out of range [0.0, 5.0]

### Requirement: Options types are pure POCOs with no business logic

Options types SHALL contain only properties with defaults. Computed properties (`EffectiveBudget`) and methods (`ResolveModels`) SHALL NOT exist on options types. Budget resolution SHALL be a method on `BudgetCalculator`. Model resolution SHALL be inlined at the call-site.

#### Scenario: NjordOptions has no EffectiveBudget property

- **WHEN** `NjordOptions` is inspected
- **THEN** it SHALL NOT have an `EffectiveBudget` property or any methods

### Requirement: One type per configuration file

Each `.cs` file in the `Configuration/` directory SHALL contain exactly one public or internal type. File names SHALL match the type name.

#### Scenario: EnrichmentOptions in own file

- **WHEN** `EnrichmentOptions.cs` is inspected
- **THEN** it SHALL contain only the `EnrichmentOptions` class

### Requirement: AlertThresholdOptions renamed to AlertOptions

The type `AlertThresholdOptions` SHALL be renamed to `AlertOptions`. The property name on `EnrichmentOptions` remains `Alerts`. The config JSON key remains `Alerts`.

#### Scenario: AlertOptions type name

- **WHEN** alert configuration is accessed
- **THEN** the type SHALL be `AlertOptions`, not `AlertThresholdOptions`

### Requirement: BudgetValidator renamed to BudgetCalculator

The static class `BudgetValidator` SHALL be renamed to `BudgetCalculator`. It SHALL additionally expose a `GetEffectiveBudget(NjordOptions)` method that returns the resolved `RequestBudget` (override ?? free-tier default).

#### Scenario: BudgetCalculator provides effective budget

- **WHEN** the system needs the effective request budget
- **THEN** it SHALL call `BudgetCalculator.GetEffectiveBudget(options)` instead of accessing `options.EffectiveBudget`

### Requirement: PersistenceOptions is a class

`PersistenceOptions` SHALL be a `sealed class`, not a `sealed record`, consistent with all other options types.

#### Scenario: PersistenceOptions consistency

- **WHEN** `PersistenceOptions` is inspected
- **THEN** it SHALL be declared as `sealed class`, not `sealed record`
