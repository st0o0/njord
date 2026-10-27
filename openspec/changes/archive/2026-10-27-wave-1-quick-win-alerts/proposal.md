## Why

The ForecastClient now delivers `precipitation_probability` and `uv_index` natively from the deterministic Forecast API. Several high-value alerts can be built with minimal new infrastructure -- just new FeatureRules and one new WeatherParam (`uv_index_clear_sky`). These alerts cover rain readiness, heat-related medical danger, UV risk under intermittent clouds, and winter snow depth -- all common HA automation triggers that njord does not yet provide.

## What Changes

- **New `RainProbability` alert** (`Weather:Alerts`): fires when `precipitation_probability` exceeds thresholds in a rolling 3-6 h window. Severity: moderate (>50 %), high (>70 %), extreme (>90 %). Requires Ensemble for confidence. Default enabled.
- **New `WetBulbDanger` alert** (`Weather:Alerts`): fires when `wet_bulb_temperature_2m` exceeds medically significant thresholds. Yellow >26 °C, orange >28 °C, red >32 °C. Heat stroke danger even in shade. Requires Ensemble for confidence. Default disabled (rare at most locations).
- **New `ClearSkyUv` alert** (`Lifestyle:Alerts`): fires when `uv_index_clear_sky` > 6, warning that UV is dangerous even with intermittent clouds. Does not require Ensemble (UV is deterministic from solar angle). Default disabled.
- **New `SnowDepthWatch` condition** (`Weather:Conditions`): reports snow accumulation status. Levels: light (>2 cm), moderate (>10 cm), heavy (>25 cm), extreme (>50 cm). Does not require Ensemble. Default disabled.
- **New `WeatherParams.UvIndexClearSky`** flag, corresponding `WeatherPoint.UvIndexClearSky` field, `WeatherApiVariable.UvIndexClearSky` (`"uv_index_clear_sky"`), and `WeatherHourlyData.UvIndexClearSky` deserialization field.

## Capabilities

### New Capabilities
- `rain-probability-alert`: RainProbability alert based on precipitation_probability thresholds
- `wet-bulb-danger-alert`: WetBulbDanger alert for medically dangerous wet-bulb temperatures
- `clear-sky-uv-alert`: ClearSkyUv alert for UV danger under intermittent cloud cover
- `snow-depth-watch`: SnowDepthWatch condition reporting snow accumulation status

### Modified Capabilities
- `domain-point-types`: WeatherPoint gains `UvIndexClearSky` field; WeatherParams gains matching flag

## Impact

- **Njord.Forecast**: `WeatherParams` gains `UvIndexClearSky`; `WeatherPoint` gains `UvIndexClearSky` field.
- **Njord.OpenMeteo**: `WeatherApiVariable` gains `UvIndexClearSky`; `WeatherHourlyData` gains deserialization field; mapping extensions updated.
- **Njord.Features**: 4 new feature folders under `Alerts/` and `Conditions/`; `AddFeatures` registration; 4 new `<Name>Config` classes.
- **Njord.Features.Tests**: tests for all 4 new features.
- **docs/data/features.json**: 4 new feature entries.
- **Config Builder**: updated for new features and their domain requirements.
- **ha-njord**: new entity mappings for the 4 features (binary_sensors and sensor).

## Budget Impact

`uv_index_clear_sky` adds 1 variable to the weather request. With the current ~31 variables, `ceil(32/10) = 4` -- same bucket, no additional API call. Zero budget impact.

## Non-goals

- Changing existing alerts or their thresholds.
- Adding new domains or modifying the polling architecture.
- Adding variables beyond `uv_index_clear_sky` (further variables are covered by waves 2-4).
