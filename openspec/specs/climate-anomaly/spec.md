# climate-anomaly Specification

## Purpose
TBD - created by archiving change climate-anomaly. Update Purpose after archive.
## Requirements
### Requirement: Climate baseline fetched and cached at startup
The system SHALL fetch 30 years (1991-2020) of daily climate data per location from the Climate API at startup and cache monthly averages.

#### Scenario: Baseline available after startup
- **WHEN** njord starts with location "Lucerne"
- **THEN** the climate baseline SHALL contain 12 monthly averages for temperature, precipitation and sunshine for Lucerne

#### Scenario: Baseline refresh daily
- **WHEN** 24 hours have passed since the last fetch
- **THEN** the baseline SHALL be refreshed

### Requirement: ClimateAnomalyHandler compares forecast vs baseline
ClimateAnomalyHandler SHALL trigger on Weather domain data and compare today's forecast values with the cached 30-year monthly baseline for the location.

#### Scenario: Warm anomaly detected
- **WHEN** today's forecast temperature is 28°C and the July baseline mean is 24°C
- **THEN** a ClimateAnomalyComputed event SHALL be emitted with TempAnomaly = +4.0

#### Scenario: No baseline available
- **WHEN** the climate baseline has not been fetched yet
- **THEN** the handler SHALL return no events

### Requirement: Anomaly reports temperature and precipitation
ClimateAnomalyComputed SHALL include TempAnomaly (°C difference), PrecipAnomaly (percentage vs baseline), and BaselinePeriod (e.g. "1991-2020 July average").

#### Scenario: Dry anomaly
- **WHEN** today's forecast precipitation is 0mm and the July baseline daily mean is 4mm
- **THEN** PrecipAnomaly SHALL be -100%

