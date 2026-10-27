## ADDED Requirements

### Requirement: WeatherPoint carries clear-sky UV index
WeatherPoint SHALL include an optional `UvIndexClearSky` field (`double?`). The field SHALL be populated from the Forecast API's `uv_index_clear_sky` variable and SHALL be gated by `WeatherParams.UvIndexClearSky` in `HasFields`.

#### Scenario: Clear-sky UV present
- **WHEN** the Forecast API returns `uv_index_clear_sky` data
- **THEN** `WeatherPoint.UvIndexClearSky` SHALL carry the value

#### Scenario: Clear-sky UV absent
- **WHEN** no feature requires `UvIndexClearSky`
- **THEN** the variable SHALL not be requested and the field SHALL be null
