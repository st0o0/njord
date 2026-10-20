## ADDED Requirements

### Requirement: EnrichmentOptions toggles individual enrichments
The system SHALL provide an `EnrichmentOptions` class with one boolean property per enrichment handler. Defaults SHALL be: WeatherAlerts=true, CommuteSafety=true, PollenAlert=true, Pm25Alert=true. All others SHALL default to false.

#### Scenario: Default enrichments active
- **WHEN** no enrichment configuration is provided
- **THEN** WeatherAlerts, CommuteSafety, PollenAlert and Pm25Alert SHALL be enabled and all others disabled

#### Scenario: User enables flood enrichments
- **WHEN** `Njord:Enrichments:FloodRisk=true` is configured
- **THEN** FloodRisk SHALL be enabled and the Flood domain SHALL be included in required domains

### Requirement: Domain resolution derives required domains from enabled enrichments
The system SHALL compute the required API domains as the union of all enabled handlers' Triggers. Only these domains SHALL be fetched by the PollWorker.

#### Scenario: Minimal config fetches 2 domains
- **WHEN** only the 4 default enrichments are enabled
- **THEN** required domains SHALL be Weather and AirQuality (2 API calls per cycle)

#### Scenario: Flood enrichment adds Flood domain
- **WHEN** FloodRisk is also enabled
- **THEN** required domains SHALL be Weather, AirQuality and Flood (3 API calls per cycle)

#### Scenario: Disabling all AQ enrichments removes AQ domain
- **WHEN** PollenAlert and Pm25Alert are disabled and no other AQ enrichment is enabled
- **THEN** AirQuality SHALL NOT be in required domains

### Requirement: IDomainHandler declares its config key
Each IDomainHandler SHALL expose a `ConfigKey` property matching its corresponding EnrichmentOptions property name. The pipeline SHALL evaluate only handlers whose ConfigKey is enabled.

#### Scenario: Disabled handler is not evaluated
- **WHEN** LaundryScore is disabled in EnrichmentOptions
- **THEN** the LaundryScoreHandler SHALL NOT be called by the pipeline

### Requirement: Budget projection uses resolved domain count
BudgetCalculator SHALL use the resolved domain count (not a hardcoded constant) for budget projection.

#### Scenario: Budget reflects actual domains
- **WHEN** 2 domains are resolved (Weather + AirQuality)
- **THEN** budget projection SHALL use 2 calls per cycle per location
