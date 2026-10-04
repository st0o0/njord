### Requirement: Parse ParameterValue extra fields from protobuf

The gRPC client SHALL parse the `repeated ParameterValue extra` field from both `HourlyForecast` and `DailyForecast` protobuf messages into a `dict[str, float | str | bool]` on the corresponding dataclass.

Each `ParameterValue` entry SHALL be converted based on its oneof case:
- `numeric` → `float`
- `text` → `str`
- `flag` → `bool`

Entries with no value set (none of the oneof cases) SHALL be skipped.

#### Scenario: Hourly forecast with numeric extras

- **WHEN** njord returns an `HourlyForecast` with `extra` containing `[{name: "cape", numeric: 450.0}, {name: "uv_index", numeric: 7.2}]`
- **THEN** the resulting `HourlyForecastData.extra` SHALL be `{"cape": 450.0, "uv_index": 7.2}`

#### Scenario: Daily forecast with mixed-type extras

- **WHEN** njord returns a `DailyForecast` with `extra` containing `[{name: "frost_risk", flag: true}, {name: "pollen_level", text: "high"}]`
- **THEN** the resulting `DailyForecastData.extra` SHALL be `{"frost_risk": True, "pollen_level": "high"}`

#### Scenario: Empty extra field

- **WHEN** njord returns a forecast with no `extra` entries (empty repeated field)
- **THEN** the resulting dataclass `extra` SHALL be an empty dict `{}`

#### Scenario: ParameterValue with no value set

- **WHEN** a `ParameterValue` entry has `name` set but none of the oneof value fields
- **THEN** that entry SHALL be skipped and not appear in the `extra` dict

### Requirement: Expose current hour extras as entity state attributes

The `NjordWeatherEntity` SHALL expose the `extra` dict from the current hour (first hourly entry) as `extra_state_attributes`.

#### Scenario: Entity with extras available

- **WHEN** the current hourly forecast has `extra = {"cape": 450.0, "uv_index": 7.2}`
- **THEN** `extra_state_attributes` SHALL include `{"cape": 450.0, "uv_index": 7.2}`

#### Scenario: Entity with no extras

- **WHEN** the current hourly forecast has `extra = {}`
- **THEN** `extra_state_attributes` SHALL return `None` (no extra attributes)

#### Scenario: Entity with no forecast data

- **WHEN** the entity has no forecast data available
- **THEN** `extra_state_attributes` SHALL return `None`

### Requirement: Include extras in hourly forecast service response

The `_async_forecast_hourly` callback SHALL merge each hour's `extra` dict into the corresponding `Forecast` dict entry.

#### Scenario: Hourly forecast entries include extras

- **WHEN** `weather.get_forecasts` is called and hourly data has extras
- **THEN** each forecast entry in the response SHALL contain the extra keys alongside standard keys (datetime, native_temperature, etc.)

#### Scenario: Hourly entry with empty extras

- **WHEN** an hourly entry has `extra = {}`
- **THEN** the forecast dict entry SHALL contain only standard forecast keys (no spurious empty keys added)

### Requirement: Include extras in daily forecast service response

The `_async_forecast_daily` callback SHALL merge each day's `extra` dict into the corresponding `Forecast` dict entry.

#### Scenario: Daily forecast entries include extras

- **WHEN** `weather.get_forecasts` is called with `type: "daily"` and daily data has extras
- **THEN** each forecast entry in the response SHALL contain the extra keys alongside standard keys

#### Scenario: Daily entry with empty extras

- **WHEN** a daily entry has `extra = {}`
- **THEN** the forecast dict entry SHALL contain only standard forecast keys
