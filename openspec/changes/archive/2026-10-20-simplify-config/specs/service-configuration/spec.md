## REMOVED Requirements

### Requirement: Models configuration
**Reason**: Ensemble is the default and only polling mode. Individual model configuration is dead — no polling consumes it. The catalog displays "Ensemble Mean" instead of a model list.
**Migration**: Remove `Njord:Models` from config. Catalog shows "Ensemble Mean".

### Requirement: Horizons configuration
**Reason**: Zero production consumers. The `Horizon` type stays as a domain model type but the config property is dead.
**Migration**: Remove `Njord:Horizons` from config.

### Requirement: Parameter groups configuration
**Reason**: The Groups/Extra/Exclude config is over-exposed complexity. The weather client owns its variable list internally. Only `IncludeSolar` is user-facing.
**Migration**: Remove `Njord:Parameters`. Use `Njord:Weather:IncludeSolar: true` for solar.

## MODIFIED Requirements

### Requirement: Startup validation enforces the budget projection
The system SHALL project monthly usage as `locations × domainCount × cycles-per-month × apiCallWeight` where apiCallWeight is computed from the weather client's variable count. Models and parameter groups are no longer inputs.

#### Scenario: Budget projection with 2 domains
- **WHEN** 1 location, 2 resolved domains, 60-minute poll interval, and the standard weather variable set
- **THEN** the projection SHALL use domain count 2 and the weather client's computed weight

### Requirement: NjordOptions contains only Locations
After the removal of Models, Horizons, Parameters and EnabledDomains, NjordOptions SHALL contain only the Locations list. ForecastDays moves to WeatherOptions.

#### Scenario: Minimal config starts successfully
- **WHEN** only `Njord:Locations:0:Name=Lucerne` and `Njord:Mqtt:Host=mosquitto` are configured
- **THEN** the system SHALL start with all defaults (WeatherAlerts, CommuteSafety, PollenAlert, Pm25Alert enabled, 2 domains, 60min polling)
