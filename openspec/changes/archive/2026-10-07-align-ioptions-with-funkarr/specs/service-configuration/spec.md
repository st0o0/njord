## REMOVED Requirements

### Requirement: NjordOptions is the single options root
Reason: superseded by `standalone-sub-options`. `GrpcOptions` and `SensorOptions`
are now independently registered and injected (no longer nested on
`NjordOptions`, no longer accessed via `IOptions<NjordOptions>`).
`MqttOptions` and `EnrichmentOptions` are dual-bound (nested on `NjordOptions`
for the admin-mutation path, and independently registered for feature-local
consumers) — see `standalone-sub-options` for the full, current rule.

### Requirement: SensorOptions is a nested property on NjordOptions
Reason: superseded by `standalone-sub-options`. `SensorOptions` is no longer a
property of `NjordOptions`; it is registered and validated by `Njord.Sensors`'s
setup container.

## MODIFIED Requirements

### Requirement: Validators implement IValidateOptions of their own sub-option, except cross-cutting and Mqtt
`NjordOptionsValidator` SHALL implement `IValidateOptions<NjordOptions>` and validate only cross-cutting concerns (locations, models, horizons, persistence, and `Mqtt` — see `standalone-sub-options` for why Mqtt stays here). `ConsensusOptionsValidator`, `HistoryOptionsValidator`, and `IndexOptionsValidator` SHALL implement `IValidateOptions<EnrichmentOptions>` and access config via `options.*` (not `options.Enrichment.*`). `SensorOptionsValidator` SHALL implement `IValidateOptions<SensorOptions>`.

#### Scenario: IndexOptionsValidator validates through EnrichmentOptions
- **WHEN** `IndexOptionsValidator` validates index preferences
- **THEN** it SHALL receive `EnrichmentOptions` directly and access `.Indices` without injecting `IOptions<NjordOptions>`

#### Scenario: SensorOptionsValidator validates through SensorOptions
- **WHEN** `SensorOptionsValidator` validates sensor configuration
- **THEN** it SHALL receive `SensorOptions` directly, not `NjordOptions`
