## MODIFIED Requirements

### Requirement: Per-score overrides via ScoreOverrides dictionary

`IndexOptions` SHALL contain a `ScoreOverrides` property of type `IDictionary<string, IndexPreferences>`. Keys SHALL be score names: `Laundry`, `Outdoor`, `Running`, `Cycling`, `Bbq`, `Irrigation`, `Solar`, `NightVentilation` (case-insensitive). Properties set in a score override SHALL take precedence over the global `Preferences` for that score. Unset properties SHALL fall through to the global level.

#### Scenario: NightVentilation override

- **WHEN** `ScoreOverrides.NightVentilation.HumiditySensitivity` is 1.5
- **THEN** the resolved `HumiditySensitivity` for NightVentilation is 1.5

#### Scenario: Old Ventilation key logged as warning

- **WHEN** `ScoreOverrides` contains a key `"Ventilation"`
- **THEN** config validation SHALL log a warning and ignore the entry (unknown score name)

#### Scenario: Score-specific sensitivity

- **WHEN** global `HeatSensitivity` is 1.0 and `ScoreOverrides.Running.HeatSensitivity` is 0.7
- **THEN** the resolved `HeatSensitivity` for Running is 0.7 and for all other scores is 1.0

### Requirement: ResolvedPreferences record for scorer consumption

A `ResolvedPreferences` record SHALL be produced per (location, score) pair. It SHALL contain all preference properties as non-nullable doubles with fully resolved values. `IndexScorer` methods SHALL accept `ResolvedPreferences` instead of raw config values. The score name `NightVentilation` SHALL be used in the resolution key instead of `Ventilation`.

#### Scenario: Scorer receives resolved values

- **WHEN** `IndexScorer.NightVentilation` is called
- **THEN** it receives a `ResolvedPreferences` with `IndoorTemp`, `HeatSensitivity`, `HumiditySensitivity`, `WindSensitivity`, `RainSensitivity` fully resolved

#### Scenario: Resolution key uses NightVentilation

- **WHEN** preferences are resolved for the ventilation scorer
- **THEN** the key SHALL be `("Lucerne", "NightVentilation")`, not `("Lucerne", "Ventilation")`
