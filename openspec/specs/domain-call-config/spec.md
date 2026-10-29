# domain-call-config Specification

## Purpose
TBD - created by archiving change fix-budget-guard. Update Purpose after archive.
## Requirements
### Requirement: DomainCallConfig carries per-domain API call parameters
The system SHALL provide a read-only `DomainCallConfig` record in `Njord.Core` that
carries the per-domain forecast days, model counts, and past-hours values needed to
compute API call weights. The Host SHALL build it from the resolved domain Options at
startup and register it as `IOptions<DomainCallConfig>`.

#### Scenario: All domain parameters present
- **WHEN** `IOptions<DomainCallConfig>` is resolved
- **THEN** it SHALL contain ForecastDays and ForecastModelCount for Forecast, EnsembleDays and EnsembleModelCount for Ensemble, AirQualityDays for AirQuality, FloodDays and FloodIncludeEnsemble for Flood, MarineDays for Marine, SatellitePastHours for Satellite, and SeasonalMonths for Seasonal

#### Scenario: Values reflect configured options
- **WHEN** `Njord:Forecast:ForecastDays` is 7 and `Njord:Ensemble:Models` has 2 entries
- **THEN** `DomainCallConfig.ForecastDays` SHALL be 7 and `DomainCallConfig.EnsembleModelCount` SHALL be 2

### Requirement: CallWeight uses fractional day multiplier
`CallWeight.Of()` SHALL compute `ceil(variableCount / 10.0) * (forecastDays / 14.0)` and
return a `double`. The days factor SHALL be fractional, not ceiling-rounded, matching
Open-Meteo's documented billing formula.

#### Scenario: Short forecast period
- **WHEN** variableCount is 15 and forecastDays is 4
- **THEN** CallWeight SHALL return `2 * (4/14) = 0.571` (approximately)

#### Scenario: Long forecast period
- **WHEN** variableCount is 5 and forecastDays is 92
- **THEN** CallWeight SHALL return `1 * (92/14) = 6.571` (approximately)

#### Scenario: Exact two-week boundary
- **WHEN** variableCount is 10 and forecastDays is 14
- **THEN** CallWeight SHALL return `1 * 1.0 = 1.0`

### Requirement: BudgetGuard computes weights for all domains
BudgetGuard SHALL compute call weights for every required domain using
`DomainCallConfig` and `DomainRequirements`, not just Forecast and Ensemble.

#### Scenario: Flood weight with default 92 days
- **WHEN** Flood is required with 3 variables and FloodDays is 92
- **THEN** the Flood call weight SHALL be `ceil(3/10) * (92/14) ≈ 6.57`, not 1

#### Scenario: AirQuality weight with many variables
- **WHEN** AirQuality is required with 12 variables and AirQualityDays is 5
- **THEN** the AirQuality call weight SHALL be `ceil(12/10) * (5/14) ≈ 0.71`

#### Scenario: Marine weight
- **WHEN** Marine is required with 10 variables and MarineDays is 8
- **THEN** the Marine call weight SHALL be `ceil(10/10) * (8/14) ≈ 0.57`

#### Scenario: Satellite weight uses PastHours
- **WHEN** Satellite is required with 5 variables and SatellitePastHours is 6
- **THEN** the Satellite call weight SHALL be `ceil(5/10) * (6/336) ≈ 0.018` per cycle, matching the archive API's hourly-data billing

#### Scenario: Total projection rejects over-budget configuration
- **WHEN** 1 location, all features enabled, 60-min poll, default ForecastDays
- **THEN** the projected monthly usage SHALL be higher than the old calculation and SHALL accurately reflect the Open-Meteo billing formula

