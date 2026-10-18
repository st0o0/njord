# model-snapshot Specification

## Purpose

Running state of the latest model forecast data per (location, model), updated on every successful fetch. Provides change detection so downstream consumers only recompute when data actually changed.
## Requirements
### Requirement: ModelForecasts holds the latest forecast per location and model
The `ModelForecasts` SHALL be an immutable record holding an `IReadOnlyDictionary<(string Location, WeatherModel Model), Forecast>`. Construction from an empty state SHALL yield `ModelForecasts.Empty` with an empty dictionary. The `ModelForecasts` SHALL expose the dictionary as `Entries`.

#### Scenario: Empty ModelForecasts has no entries
- **WHEN** `ModelForecasts.Empty` is accessed
- **THEN** its `Entries` dictionary is empty

#### Scenario: ModelForecasts exposes typed entries
- **WHEN** a `ModelForecasts` holds forecasts for (lucerne, icon_d2) and (lucerne, ecmwf_ifs025)
- **THEN** `Entries.Count` is 2 and both keys are retrievable

### Requirement: Update returns a new `ModelForecasts` with the forecast replaced
The `ModelForecasts` SHALL expose an `Update(Forecast)` method that returns a new `ModelForecasts` with the forecast for the given (location, model) replaced. If no entry existed for that key, it SHALL be added. The original `ModelForecasts` SHALL remain unchanged.

#### Scenario: First forecast for a model is added
- **WHEN** `ModelForecasts.Empty.Update(forecast)` is called with a forecast for (lucerne, icon_d2)
- **THEN** the returned `ModelForecasts` has 1 entry with key (lucerne, icon_d2)
- **THEN** the original `ModelForecasts.Empty` still has 0 entries

#### Scenario: Existing forecast is replaced
- **WHEN** a `ModelForecasts` with (lucerne, icon_d2) cycle-1 is updated with (lucerne, icon_d2) cycle-2
- **THEN** the returned `ModelForecasts` has 1 entry and its forecast is cycle-2

#### Scenario: Different models coexist
- **WHEN** a `ModelForecasts` with (lucerne, icon_d2) is updated with (lucerne, ecmwf_ifs025)
- **THEN** the returned `ModelForecasts` has 2 entries

### Requirement: Change detection flags whether an update modified data
The `ModelForecasts` SHALL expose a `HasChanged` boolean property. `ModelForecasts.Empty` SHALL have `HasChanged` = `false`. A `ModelForecasts` returned by `Update` SHALL have `HasChanged` = `true` when the stored forecast for that key has a different `CycleId` (or the key was new). If the stored forecast has the same `CycleId`, the `ModelForecasts` SHALL be returned unchanged with `HasChanged` = `false`.

#### Scenario: New entry sets HasChanged
- **WHEN** an empty `ModelForecasts` is updated with a new forecast
- **THEN** the returned `ModelForecasts` has `HasChanged` = `true`

#### Scenario: Identical update does not set HasChanged
- **WHEN** a `ModelForecasts` is updated with the exact same forecast object already stored for that key
- **THEN** the returned `ModelForecasts` has `HasChanged` = `false`

#### Scenario: Different cycle sets HasChanged
- **WHEN** a `ModelForecasts` holding cycle-1 for (lucerne, icon_d2) is updated with cycle-2 for the same key
- **THEN** the returned `ModelForecasts` has `HasChanged` = `true`

### Requirement: ModelForecasts provides per-location model enumeration
The `ModelForecasts` SHALL expose a method `ModelsFor(string location)` returning all `WeatherModel` keys present for that location.

#### Scenario: Multiple models for a location
- **WHEN** a `ModelForecasts` holds (lucerne, icon_d2), (lucerne, ecmwf_ifs025), (zurich, icon_d2)
- **THEN** `ModelsFor("lucerne")` returns icon_d2 and ecmwf_ifs025
- **THEN** `ModelsFor("zurich")` returns icon_d2

#### Scenario: Unknown location returns empty
- **WHEN** `ModelsFor("unknown")` is called
- **THEN** the result is empty

