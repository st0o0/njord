# domain-forecast-model Specification

## Purpose

Core domain forecast types for the multi-domain proxy architecture: Location (name + coordinates with name-based identity), DomainForecast<T> (typed time-series with model-run metadata and resolution), LocationForecast (closed read-model of current knowledge per location), and ForecastQueries (temporal operations for enrichment correlation).

## Requirements

### Requirement: Location carries coordinates with name-based identity
Location SHALL be a readonly record struct with Name (string), Latitude (double), Longitude (double). Equality SHALL be based on Name only, case-insensitive.

#### Scenario: Same name different case are equal
- **WHEN** Location("Lucerne", 47.05, 8.31) and Location("lucerne", 47.05, 8.31) are compared
- **THEN** they SHALL be equal

### Requirement: DomainForecast holds typed time-series with metadata
DomainForecast<T> SHALL hold Domain (DomainKind), ModelRun (DateTimeOffset), Resolution (TimeResolution), and Points (ImmutableArray<T>). Points SHALL implement IHasValidTime. There SHALL be no PollCycle or CycleId.

#### Scenario: DomainForecast carries model run time
- **WHEN** a DomainForecast is created from an Ensemble API response
- **THEN** its ModelRun SHALL be the model initialization time from the response, not the fetch time

### Requirement: LocationForecast is a closed read-model
LocationForecast SHALL have nullable properties for each domain (Weather, AirQuality, Flood, Solar, Marine). A With method SHALL update one domain while preserving others.

#### Scenario: Updating one domain preserves others
- **WHEN** LocationForecast has Weather and Flood data and Weather is updated
- **THEN** the new LocationForecast SHALL have the new Weather and the unchanged Flood

### Requirement: ForecastQueries provides temporal operations
ForecastQueries SHALL provide static methods: ValueAt (point valid at a specific time), Window (all points in a range), Nearest (closest point within tolerance), Correlate (inner join of two series on overlapping valid times).

#### Scenario: ValueAt finds the valid point
- **WHEN** an hourly series has points at 06:00, 07:00, 08:00 and ValueAt(07:30) is called
- **THEN** it SHALL return the 07:00 point (the one whose valid window contains 07:30)

#### Scenario: Correlate joins different resolutions
- **WHEN** an hourly Weather series and a 6-hourly Flood series are correlated
- **THEN** each Flood point SHALL be paired with the Weather points within its valid window
