# tier-2-enrichments Specification

## Purpose
TBD - created by archiving change tier-2-enrichments. Update Purpose after archive.
## Requirements
### Requirement: SolarYieldEstimate computes daily PV yield
SolarYieldHandler SHALL estimate daily PV yield in kWh from direct and diffuse radiation, cloud cover and sunshine duration, scaled by configured panel parameters (PeakKw, Azimuth, Tilt).

#### Scenario: Sunny day with 5kWp system
- **WHEN** direct_radiation averages 600 W/m² over 8 sunshine hours and PeakKw=5.0
- **THEN** the estimated yield SHALL be approximately 4-5 kWh

#### Scenario: Cloudy day reduces yield
- **WHEN** cloud cover is 90% and direct_radiation averages 100 W/m²
- **THEN** the estimated yield SHALL be significantly lower than a sunny day

### Requirement: GardeningAdvisory evaluates outdoor gardening conditions
GardeningHandler SHALL report planting safety (no frost in 48h), watering need (no rain >2mm in 24h), and frost protection needed (min temp <2°C in 24h).

#### Scenario: Frost risk triggers protection advisory
- **WHEN** minimum temperature in the next 24h is -1°C
- **THEN** FrostRisk SHALL be true and PlantingSafe SHALL be false


### Requirement: OutdoorActivityScore composites Weather and AQ
OutdoorScoreHandler SHALL produce a 0-100 score from temperature comfort, rain probability, wind, cloud cover, UV index, PM2.5 and pollen, with a best time window.

#### Scenario: Perfect outdoor day
- **WHEN** temp 22°C, no rain, light wind, low cloud, low UV, low PM2.5, no pollen
- **THEN** the score SHALL be 85 or above

### Requirement: BBQScore evaluates grilling conditions
BBQScoreHandler SHALL produce a 0-100 score: temperature 18-28°C ideal, wind below 5 m/s, no rain, low cloud cover.

#### Scenario: Ideal BBQ evening
- **WHEN** temp 24°C, wind 2 m/s, precip_prob 0%, cloud 20%
- **THEN** the score SHALL be 90 or above

### Requirement: FloodTimeline analyzes discharge forecast
FloodTimelineHandler SHALL find the peak discharge day, count days above the p75 threshold, and estimate time to recession.

#### Scenario: Rising river with peak in 3 days
- **WHEN** discharge forecast shows increasing values peaking on day 3 above p75
- **THEN** PeakDay SHALL be day 3 and DaysAboveWarning SHALL be at least 2

### Requirement: ApproachingFront detects weather system changes
ApproachingFrontHandler SHALL detect pressure drops >3 hPa/6h, wind direction shifts >45°, and precipitation onset from current vs previous forecast.

#### Scenario: Front approaching from the west
- **WHEN** pressure drops 5 hPa over 6h and wind shifts from S to NW and rain starts in 8h
- **THEN** an ApproachingFront event SHALL be emitted with EstimatedIn ~6-8h

