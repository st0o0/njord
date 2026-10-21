## MODIFIED Requirements

### Requirement: SeasonalOutlookHandler emits the outlook
The `SeasonalOutlook` feature (Forecast output, default off) SHALL require only the Seasonal domain and SHALL be
computed from the seasonal forecast alone; it SHALL NOT need weather data. On each seasonal update it SHALL emit one
outlook result with one month entry per month after the current month, temperature anomaly rounded to one decimal and
precipitation anomaly in percent of the normal month (`mean - anomaly`).

#### Scenario: Emits with seasonal data only
- **WHEN** a location has received seasonal data but no weather data
- **THEN** the outlook SHALL be emitted

#### Scenario: Anomalies converted
- **WHEN** the November point has a temperature anomaly of 1.74, a precipitation anomaly of 2.4 mm and a mean of 99.5 mm
- **THEN** the November entry SHALL have TempAnomaly 1.7 and PrecipAnomaly 2.5

#### Scenario: Current and past months dropped
- **WHEN** the forecast contains the current month and later months
- **THEN** only the later months SHALL be emitted

#### Scenario: No data
- **WHEN** the location has no seasonal forecast
- **THEN** no event SHALL be emitted

#### Scenario: Toggle default
- **WHEN** no configuration is supplied
- **THEN** the feature SHALL be off and excluded from the enabled features
