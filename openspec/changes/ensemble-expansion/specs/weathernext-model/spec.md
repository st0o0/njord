## ADDED Requirements

### Requirement: WeatherNext as default Ensemble model
The system SHALL include `google_weathernext2_ensemble_mean` in the default Ensemble model list alongside ECMWF IFS and GFS (NCEP). The default order SHALL be `["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"]`.

#### Scenario: Three models in default config
- **WHEN** `EnsembleOptions.Models` is not overridden
- **THEN** the applied defaults SHALL contain exactly three model IDs
- **AND** the third model SHALL be `google_weathernext2_ensemble_mean`

### Requirement: WeatherNext weather variables for comparison
The system SHALL request weather variables from `google_weathernext2_ensemble_mean` for model comparison. WeatherNext supports a subset of variables (no `apparent_temperature`, no `cape`, no `wind_gusts_10m`, no `visibility`, no `uv_index`). Missing variables SHALL be null in the model's forecast point.

#### Scenario: WeatherNext returns null for unsupported variables
- **WHEN** the Ensemble response contains `apparent_temperature_google_weathernext2_ensemble_mean` with all null values
- **THEN** the parsed forecast point for WeatherNext SHALL have `ApparentTemperature` as null
- **AND** model comparison SHALL handle null gracefully (skip that variable for that model)
