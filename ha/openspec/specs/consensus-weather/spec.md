## Capability: consensus-weather

A weather entity per location that uses multi-model consensus values instead of a single model's forecast.

### Entity

| Entity ID | Platform | 
|-----------|----------|
| `weather.njord_{loc}_consensus` | weather |

### State Mapping

The entity's current state uses the time-adjusted horizon `h{elapsed}` consensus values, where `elapsed` is the number of full hours since `consensus_updated_at`. Falls back to `h0` if `consensus_updated_at` is `None`, and to the first available horizon if the adjusted horizon is missing.

| Weather attribute | Consensus parameter | Field used |
|-------------------|-------------------|------------|
| `temperature` | `temperature_2m` | `median` |
| `humidity` | `relative_humidity_2m` | `median` |
| `pressure` | `pressure_msl` | `median` |
| `wind_speed` | `wind_speed_10m` | `median` |
| `wind_bearing` | `wind_direction_10m` | `median` |
| `cloud_cover` | `cloud_cover` | `median` |
| `condition` | `weather_code` | `median` → WMO mapping |
| `visibility` | `visibility` | `median` |
| `dew_point` | `dew_point_2m` | `median` |
| `apparent_temperature` | `apparent_temperature` | `median` |
| `precipitation` | `precipitation` | `median` |

### Extra State Attributes

| Attribute | Source | Description |
|-----------|--------|-------------|
| `agreement` | consensus h{elapsed} temperature `agreement` | How well models agree (0.0–1.0) |
| `available_models` | consensus h{elapsed} `available_models` | Number of models contributing |
| `spread` | consensus h{elapsed} temperature `spread` | Temperature spread across models (°C) |
| `reliable_hours` | count of consecutive horizons from h{elapsed} with temperature agreement >= 0.5 | Number of hours where models reliably agree |
| `current_horizon` | `f"h{elapsed}"` | The horizon currently being read (e.g. "h5"); changes every hour to prevent HA staleness |
| `consensus_age_hours` | elapsed hours since `consensus_updated_at` | Hours since njord computed the consensus; absent when `consensus_updated_at` is None |

### Forecast Support

The consensus entity supports `forecast_hourly` and `forecast_daily` via HA's weather forecast service.

**Hourly forecasts**: Built from consecutive consensus horizons h{elapsed+1}..hN (h0 through h{elapsed} are excluded as they represent past/current state). Each entry has timestamp = now + (N - elapsed) hours, with median values for temperature, precipitation, wind speed, wind bearing, humidity, cloud cover, and condition (mapped from weather_code median via nearest known WMO code).

**Daily forecasts**: Aggregated from hourly consensus data per calendar day. The current (partial) day is excluded. Each daily entry includes: max temperature, min temperature, precipitation sum, max wind speed, and midday condition (derived from weather_code median at the horizon closest to 12:00 UTC for that day).

### Data Source

- gRPC: `ForecastService.GetEnrichments(location)` → `ConsensusUpdate.parameters[]`
- Each `ParameterConsensus` has `parameter` (name), `unit`, and `by_horizon[]`
- Each `HorizonConsensus` has `horizon`, `median`, `trimmed_mean`, `spread`, `iqr`, `agreement`, `available_models`

### Requirements

#### Requirement: Current state uses time-adjusted horizon
The consensus entity's current state (temperature, humidity, wind, condition, etc.) SHALL use the time-adjusted horizon `h{elapsed}` instead of hardcoded `h0`, where `elapsed` is the number of full hours since `consensus_updated_at`. Falls back to `h0` if `consensus_updated_at` is `None`.

##### Scenario: Current temperature from h0
- **WHEN** consensus data has an h0 horizon with temperature_2m median = 22.5
- **AND** consensus was just computed (0 hours elapsed)
- **THEN** the entity's temperature is 22.5

##### Scenario: Current temperature after 3 hours
- **WHEN** consensus data has h3 with temperature_2m median = 25.0
- **AND** 3 hours have elapsed since consensus computation
- **THEN** the entity's temperature is 25.0

##### Scenario: Fallback when adjusted horizon is missing
- **WHEN** elapsed hours exceeds the highest available horizon
- **THEN** the entity shows "Unknown" state

#### Requirement: Hourly forecast from consecutive horizons
The consensus entity SHALL support `FORECAST_HOURLY` by building forecast entries from `h{elapsed+1}..hN` consensus horizons, each with a real timestamp.

##### Scenario: Hourly forecast entries after elapsed time
- **WHEN** `async_forecast_hourly` is called and 3 hours have elapsed since consensus computation
- **THEN** forecast entries start from h4 (not h1), each with timestamp = now + (N - elapsed) hours

##### Scenario: Condition mapped from weather_code median
- **WHEN** an hourly consensus horizon has weather_code median = 1.2
- **THEN** the forecast entry's condition is mapped from WMO code 1 (nearest known code)

##### Scenario: h0 through h{elapsed} excluded from hourly forecast
- **WHEN** `async_forecast_hourly` is called and 3 hours have elapsed
- **THEN** horizons h0, h1, h2, h3 are not included

#### Requirement: Daily forecast aggregated from hourly
The consensus entity SHALL support `FORECAST_DAILY` by aggregating hourly consensus data per calendar day.

##### Scenario: Daily aggregation
- **WHEN** `async_forecast_daily` is called and consensus has 72 hourly horizons
- **THEN** forecast entries are returned for each full future day with: max temperature, min temperature, precipitation sum, max wind speed, and midday condition

##### Scenario: Today excluded
- **WHEN** `async_forecast_daily` is called
- **THEN** the current (partial) day is not included in the daily forecast

##### Scenario: Midday condition
- **WHEN** a daily forecast entry is built for a future day
- **THEN** condition is derived from the weather_code median at the horizon closest to 12:00 UTC for that day

#### Requirement: Reliability extra state attributes
The consensus entity SHALL expose reliability and freshness information in extra_state_attributes using the time-adjusted horizon.

##### Scenario: Reliable hours attribute after elapsed time
- **WHEN** 2 hours have elapsed and temperature agreement drops below 0.5 at h10
- **THEN** `reliable_hours` is 8 (counting from h2 through h9)

##### Scenario: Agreement and spread from adjusted horizon
- **WHEN** 2 hours have elapsed and h2 temperature has agreement=0.75, spread=2.8, available_models=7
- **THEN** extra_state_attributes contains `agreement=0.75`, `spread=2.8`, `available_models=7`

##### Scenario: Current horizon attribute reflects offset
- **WHEN** 5 hours have elapsed since consensus computation
- **THEN** extra_state_attributes contains `current_horizon = "h5"`

##### Scenario: Current horizon at zero offset
- **WHEN** consensus was just computed (0 hours elapsed)
- **THEN** extra_state_attributes contains `current_horizon = "h0"`

##### Scenario: Consensus age in hours
- **WHEN** consensus was computed 3 hours ago
- **THEN** extra_state_attributes contains `consensus_age_hours = 3`

##### Scenario: Consensus age absent when no timestamp
- **WHEN** `consensus_updated_at` is None
- **THEN** extra_state_attributes SHALL NOT contain `consensus_age_hours`

##### Scenario: Attributes change every hour to prevent staleness
- **WHEN** the hourly refresh fires and the horizon offset advances from 4 to 5
- **THEN** `current_horizon` changes from `"h4"` to `"h5"` and `consensus_age_hours` increments
- **AND** HA updates `last_updated` on the entity because attributes changed

#### Requirement: Supported features set at init
The consensus entity SHALL determine `supported_features` at init based on available consensus data.

##### Scenario: Hourly consensus data available
- **WHEN** consensus data has multiple consecutive horizons (h0, h1, h2, ...)
- **THEN** `_attr_supported_features` includes `FORECAST_HOURLY | FORECAST_DAILY`

##### Scenario: No consensus data
- **WHEN** consensus data is None
- **THEN** `_attr_supported_features` is `0`

### Files

- `custom_components/njord/weather.py` — add `NjordConsensusWeatherEntity` alongside existing `NjordWeatherEntity`
- `tests/test_weather.py` — consensus entity tests (may need separate test file if too large)
