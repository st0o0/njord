## ADDED Requirements

### Requirement: WeatherForecastBuilder creates DomainForecast with known values
WeatherForecastBuilder SHALL produce a `DomainForecast<WeatherPoint>` with configurable hourly values, defaulting to sensible weather (20°C, 50% humidity, 3 m/s wind, 0mm rain). Override methods SHALL allow setting specific parameters at specific hours.

#### Scenario: Default forecast has 24 hourly points
- **WHEN** `WeatherForecastBuilder.Default().Build()` is called
- **THEN** the result SHALL have 24 hourly WeatherPoints with TimeResolution.Hourly

#### Scenario: Override temperature at specific hour
- **WHEN** `builder.WithTemperature(hour: 6, value: -2.0).Build()` is called
- **THEN** the point at hour 6 SHALL have Temperature = -2.0 and all other points SHALL have the default

### Requirement: AnalysisFixture is deleted
After migration, no production or test code SHALL reference AnalysisFixture.

#### Scenario: No AnalysisFixture references
- **WHEN** the codebase is searched for AnalysisFixture
- **THEN** zero references SHALL exist
