## MODIFIED Requirements

### Requirement: Persistence options are their own configuration sections
Persistence settings SHALL NOT be part of `NjordOptions`. `PersistenceOptions` SHALL bind the `Njord:Persistence` section (`Provider` default `Sqlite`, `ConnectionString` default null) and `StorageOptions` SHALL bind `PersistencePath` (default `data/njord.db`) from the `Njord:Storage` section; both are registered by `ConfigSetupContainer`.

#### Scenario: Default persistence options
- **WHEN** no `Persistence` section is configured
- **THEN** `PersistenceOptions.Provider` is `Sqlite` and `PersistenceOptions.ConnectionString` is null

#### Scenario: PersistencePath coexists with Persistence section
- **WHEN** both `Njord:Storage:PersistencePath` and `Njord:Persistence:Provider` are configured
- **THEN** both values are available; `PersistencePath` is used only when the provider is `Sqlite` and no explicit `ConnectionString` is set
