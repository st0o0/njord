## ADDED Requirements

### Requirement: Features are grouped by category with per-feature cost
The builder SHALL list all features grouped by feature category. Each feature SHALL show its name, description, required domains and its marginal cost as "+N calls/month", or "+0" when its domains are already active.

#### Scenario: Cost of a feature that enables a new domain
- **WHEN** no enabled feature needs AirQuality and a user views PollenAlert for one location
- **THEN** it shows the full monthly AirQuality cost

#### Scenario: Cost when domain already active
- **WHEN** Weather is already required and a user views FrostAlert
- **THEN** it shows "+0"

### Requirement: Budget meter reflects the free tier
The builder SHALL show a live meter of projected monthly usage against 300,000 calls, colored green below 50 %, yellow from 50 % to 80 % and red above 80 %.

#### Scenario: Enabling a costly feature
- **WHEN** a user enables a feature that pushes usage past 80 %
- **THEN** the meter turns red and shows used and total calls

#### Scenario: Disabling the last user of a domain
- **WHEN** a user disables the last enabled feature requiring Flood
- **THEN** the usage drops by the Flood cost

### Requirement: Builder exports and imports the feature section
The builder SHALL export `Njord:Features` (JSON and environment variables) and SHALL import both `Njord:Features` and legacy `Njord:Enrichments` input, expanding `WeatherAlerts` to the individual alert features.

#### Scenario: Import legacy config
- **WHEN** a user imports a config with `Enrichments.WeatherAlerts: true`
- **THEN** all individual alert features are enabled and the export uses `Features`

## REMOVED Requirements

### Requirement: Parameter group selector shows API weight impact
**Reason**: Superseded by per-feature cost display; users choose features, not parameter groups.
**Migration**: Use the feature list; weather weight is reflected in the cost of the first feature requiring Weather.
