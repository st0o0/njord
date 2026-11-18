## ADDED Requirements

### Requirement: Each domain has its own poll interval
Every enabled domain SHALL poll on its own interval, independent of other domains. The interval is resolved as: per-domain config override > domain default > global PollInterval.

#### Scenario: Forecast polls at its default interval
- **WHEN** no per-domain override is configured for Forecast
- **THEN** Forecast SHALL poll every 60 minutes

#### Scenario: Marine polls at its default interval
- **WHEN** no per-domain override is configured for Marine
- **THEN** Marine SHALL poll every 360 minutes (6 hours)

#### Scenario: Per-domain override takes precedence
- **WHEN** the operator sets `Njord:Polling:Domains:Marine:PollInterval` to `02:00:00`
- **THEN** Marine SHALL poll every 120 minutes instead of its 360-minute default

### Requirement: Domain defaults reflect documented API update frequencies
The system SHALL provide default poll intervals per domain based on the documented Open-Meteo API update frequencies. Defaults SHALL be: Forecast=60m, AirQuality=60m, Marine=360m (6h), Ensemble=360m (6h), Seasonal=1440m (24h), Flood=1440m (24h).

#### Scenario: Default intervals are assigned at startup
- **WHEN** a PollWorker starts with no per-domain overrides
- **THEN** each domain's effective poll interval matches its default

### Requirement: Global PollInterval acts as a floor
No domain SHALL poll faster than the global `Njord:Polling:PollInterval`. If a domain's resolved interval is shorter than the global floor, the global floor wins.

#### Scenario: Floor prevents overly aggressive polling
- **WHEN** global PollInterval is 120 minutes and Forecast default is 60 minutes
- **THEN** Forecast SHALL poll every 120 minutes (the floor)

#### Scenario: Floor does not affect slower domains
- **WHEN** global PollInterval is 60 minutes and Marine default is 360 minutes
- **THEN** Marine SHALL poll every 360 minutes (its own interval)

### Requirement: Validation rejects invalid per-domain intervals
Per-domain interval overrides SHALL be validated at startup. An interval that is non-positive or exceeds 24 hours SHALL be rejected.

#### Scenario: Zero interval is rejected
- **WHEN** `Njord:Polling:Domains:Forecast:PollInterval` is set to `00:00:00`
- **THEN** startup validation SHALL fail with a descriptive message

### Requirement: BudgetGuard projects from per-domain intervals
The startup budget projection SHALL compute cycles per month individually for each domain using its resolved interval, not a single global interval.

#### Scenario: Budget projection uses per-domain intervals
- **WHEN** Forecast polls every 60m and Marine polls every 360m
- **THEN** budget projection counts Forecast at 720 cycles/month and Marine at 120 cycles/month

### Requirement: Domain poll state uses Active/Suspended phases
Each domain SHALL be in one of two phases: Active (polling on its interval) or Suspended (waiting for a 24h recheck after NoData). The Discovery and Steady phases are removed.

#### Scenario: A domain starts in Active phase
- **WHEN** a PollWorker starts polling a domain
- **THEN** the domain's phase SHALL be Active

#### Scenario: Active domain polls on its interval
- **WHEN** a domain is Active and its timer fires
- **THEN** it fetches data and schedules the next poll at `now + effectiveInterval`
