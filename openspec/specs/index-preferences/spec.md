# index-preferences Specification

## Purpose

Cascading configuration model for index scoring preferences. Allows users to tune sensitivity multipliers and ideal-point parameters at three levels: global defaults, per-score overrides, and per-location overrides. Provides a resolved preferences type that scorers consume without knowing about the cascade.

## Requirements

### Requirement: IndexPreferences config section with sensitivity multipliers and ideal points
`IndexOptions` SHALL contain a `Preferences` property of type `IndexPreferences`. `IndexPreferences` SHALL expose: `IdealOutdoorTemp` (double, default 22.0), `RunningIdealTempLow` (double, default 5.0), `RunningIdealTempHigh` (double, default 20.0), `BbqMinTemp` (double, default 10.0), `BbqIdealWindLow` (double, default 1.0), `BbqIdealWindHigh` (double, default 3.0), `IndoorTemp` (double, default 22.0), `HeatSensitivity` (double, default 1.0), `HumiditySensitivity` (double, default 1.0), `WindSensitivity` (double, default 1.0), `RainSensitivity` (double, default 1.0). All sensitivity multipliers SHALL be clamped to the range [0.0, 5.0] at validation time.

#### Scenario: Default preferences
- **WHEN** no `Preferences` section is configured
- **THEN** all values SHALL use their documented defaults

#### Scenario: Partial override
- **WHEN** only `IdealOutdoorTemp: 24.0` is set in `Preferences`
- **THEN** `IdealOutdoorTemp` is 24.0 and all other values remain at defaults

#### Scenario: Sensitivity clamping
- **WHEN** `HeatSensitivity` is set to 8.0
- **THEN** validation SHALL clamp it to 5.0

### Requirement: Per-score overrides via ScoreOverrides dictionary
`IndexOptions` SHALL contain a `ScoreOverrides` property of type `IDictionary<string, IndexPreferences>`. Keys SHALL be score names: `Laundry`, `Outdoor`, `Running`, `Cycling`, `Bbq`, `Irrigation`, `Solar`, `Ventilation` (case-insensitive). Properties set in a score override SHALL take precedence over the global `Preferences` for that score. Unset properties SHALL fall through to the global level.

#### Scenario: Score-specific sensitivity
- **WHEN** global `HeatSensitivity` is 1.0 and `ScoreOverrides.Running.HeatSensitivity` is 0.7
- **THEN** the resolved `HeatSensitivity` for Running is 0.7 and for all other scores is 1.0

#### Scenario: Score-specific ideal point
- **WHEN** `ScoreOverrides.Bbq.BbqMinTemp` is 15.0 and global `BbqMinTemp` is 10.0
- **THEN** the resolved `BbqMinTemp` for Bbq is 15.0

#### Scenario: Unknown score name logged as warning
- **WHEN** `ScoreOverrides` contains a key `"Swimming"`
- **THEN** config validation SHALL log a warning and ignore the entry

### Requirement: Per-location overrides via LocationOverrides list
`IndexOptions` SHALL contain a `LocationOverrides` property of type `IList<LocationIndexOverride>`. Each `LocationIndexOverride` SHALL have a `Location` (string), an optional `Preferences` (`IndexPreferences`), and an optional `ScoreOverrides` (`IDictionary<string, IndexPreferences>`). Location matching SHALL be case-insensitive against configured `LocationOptions.Name`.

#### Scenario: Location-specific global preference
- **WHEN** global `IdealOutdoorTemp` is 22.0 and `LocationOverrides` for "Lucerne" sets `IdealOutdoorTemp` to 24.0
- **THEN** the resolved `IdealOutdoorTemp` for Lucerne/Outdoor is 24.0 and for other locations is 22.0

#### Scenario: Location-specific score override
- **WHEN** `LocationOverrides` for "Lucerne" has `ScoreOverrides.Outdoor.WindSensitivity` set to 0.8
- **THEN** the resolved `WindSensitivity` for Lucerne/Outdoor is 0.8

#### Scenario: Unknown location logged as warning
- **WHEN** `LocationOverrides` contains a location "Berlin" not present in `NjordOptions.Locations`
- **THEN** config validation SHALL log a warning but not fail

### Requirement: Five-level cascade resolution
For any (location, score, property) tuple, resolution SHALL proceed in order: (1) `LocationOverrides[location].ScoreOverrides[score].{Property}`, (2) `LocationOverrides[location].Preferences.{Property}`, (3) `ScoreOverrides[score].{Property}`, (4) `Preferences.{Property}`, (5) hardcoded default. The first non-null value wins.

#### Scenario: Full cascade -- location score override wins
- **WHEN** global `HeatSensitivity` is 1.0, `ScoreOverrides.Outdoor.HeatSensitivity` is 1.5, and `LocationOverrides["Lucerne"].ScoreOverrides.Outdoor.HeatSensitivity` is 2.0
- **THEN** resolved for Lucerne/Outdoor is 2.0

#### Scenario: Location global overrides score override
- **WHEN** `ScoreOverrides.Outdoor.HeatSensitivity` is 1.5 and `LocationOverrides["Lucerne"].Preferences.HeatSensitivity` is 0.5 (no location score override set)
- **THEN** resolved for Lucerne/Outdoor is 0.5 (level 2 beats level 3)

#### Scenario: Fallback to global default
- **WHEN** no overrides are configured at any level for `WindSensitivity`
- **THEN** resolved value is 1.0 (hardcoded default)

### Requirement: ResolvedPreferences record for scorer consumption
A `ResolvedPreferences` record SHALL be produced per (location, score) pair. It SHALL contain all preference properties as non-nullable doubles with fully resolved values. `IndexScorer` methods SHALL accept `ResolvedPreferences` instead of raw config values.

#### Scenario: Scorer receives resolved values
- **WHEN** `IndexScorer.OutdoorScore` is called
- **THEN** it receives a `ResolvedPreferences` with `IdealTemp`, `HeatSensitivity`, `HumiditySensitivity`, `WindSensitivity`, `RainSensitivity` fully resolved

#### Scenario: Resolution happens once at config load
- **WHEN** configuration is loaded or reloaded
- **THEN** a `IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences>` SHALL be computed and reused for all subsequent poll cycles until the next config change

### Requirement: IndexOptions removes HeatingBaseTemp and CoolingBaseTemp
`IndexOptions` SHALL NOT contain `HeatingBaseTemp` or `CoolingBaseTemp` properties. These were used exclusively by the removed HDD/CDD scoring.

#### Scenario: Old config with HeatingBaseTemp
- **WHEN** a config file contains `Indices.HeatingBaseTemp: 18.0`
- **THEN** the property SHALL be silently ignored (no binding target)
