## MODIFIED Requirements

### Requirement: ModelSnapshot holds the latest forecast per location and model
The `ModelSnapshot` SHALL be an immutable record holding an `IReadOnlyDictionary<(string Location, WeatherModel Model), Forecast>`. Construction from an empty state SHALL yield `ModelSnapshot.Empty` with an empty dictionary. The snapshot SHALL expose the dictionary as `Entries`.

#### Scenario: Empty snapshot has no entries
- **WHEN** `ModelSnapshot.Empty` is accessed
- **THEN** its `Entries` dictionary is empty

#### Scenario: Snapshot exposes typed entries
- **WHEN** a snapshot holds forecasts for (lucerne, icon_d2) and (lucerne, ecmwf_ifs025)
- **THEN** `Entries.Count` is 2 and both keys are retrievable

### Requirement: Update returns a new snapshot with the forecast replaced
The `ModelSnapshot` SHALL expose an `Update(Forecast)` method that returns a new `ModelSnapshot` with the forecast for the given (location, model) replaced. If no entry existed for that key, it SHALL be added. The original snapshot SHALL remain unchanged.

#### Scenario: First forecast for a model is added
- **WHEN** `ModelSnapshot.Empty.Update(forecast)` is called with a forecast for (lucerne, icon_d2)
- **THEN** the returned snapshot has 1 entry with key (lucerne, icon_d2)
- **THEN** the original `ModelSnapshot.Empty` still has 0 entries

#### Scenario: Existing forecast is replaced
- **WHEN** a snapshot with (lucerne, icon_d2) cycle-1 is updated with (lucerne, icon_d2) cycle-2
- **THEN** the returned snapshot has 1 entry and its forecast is cycle-2

#### Scenario: Different models coexist
- **WHEN** a snapshot with (lucerne, icon_d2) is updated with (lucerne, ecmwf_ifs025)
- **THEN** the returned snapshot has 2 entries

### Requirement: Change detection flags whether an update modified data
The `ModelSnapshot` SHALL expose a `HasChanged` boolean property. `ModelSnapshot.Empty` SHALL have `HasChanged` = `false`. A snapshot returned by `Update` SHALL have `HasChanged` = `true` when the stored forecast for that key has a different `CycleId` (or the key was new). If the stored forecast has the same `CycleId`, the snapshot SHALL be returned unchanged with `HasChanged` = `false`.

#### Scenario: New entry sets HasChanged
- **WHEN** an empty snapshot is updated with a new forecast
- **THEN** the returned snapshot has `HasChanged` = `true`

#### Scenario: Identical update does not set HasChanged
- **WHEN** a snapshot is updated with the exact same forecast object already stored for that key
- **THEN** the returned snapshot has `HasChanged` = `false`

#### Scenario: Different cycle sets HasChanged
- **WHEN** a snapshot holding cycle-1 for (lucerne, icon_d2) is updated with cycle-2 for the same key
- **THEN** the returned snapshot has `HasChanged` = `true`
