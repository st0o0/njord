## Why

The most valuable weather intelligence comes from combining multiple domains and variables. Single-domain alerts cover individual hazards, but the highest-impact scenarios (wildfires, pollen explosions, storm surges, severe thunderstorms) emerge from the intersection of conditions across Weather, AirQuality, Flood, and Marine data. These cross-domain features depend on the variable infrastructure added in Waves 1-2 (soil moisture, CIN, lifted index, showers) and exploit njord's multi-domain architecture to deliver insights no single API call provides.

## What Changes

- **`FireRisk` alert** (Lifestyle category): wildfire risk index combining high temperature (>30C), low humidity (<30%), strong wind (>20 km/h), low soil moisture (<0.10 m3/m3), and prolonged dry spell (no precipitation for 48h+). Three severity levels (Yellow/Orange/Red) via a weighted scoring model. Requires Weather domain (temperature, humidity, wind, precipitation, soil moisture from Wave 2). Requires Ensemble for confidence. Default disabled.
- **`PollenBlast` alert** (AirQuality category): pollen explosion prediction when warm (>15C) + dry + windy conditions follow a rain period. Uses trend data (PollDepth=8) to detect the rain-to-dry transition that triggers pollen release. Cross-domain Weather + AirQuality. No Ensemble required. Default disabled.
- **`ThunderstormEnhanced`** -- enhancement to the existing `ThunderstormAlert`: incorporates `convective_inhibition` (CIN < -50 J/kg enables convection) and `lifted_index` (< -2 = unstable atmosphere) from Wave 2 variables. Strengthens or weakens severity based on atmospheric stability indicators. Modified feature, not new.
- **`GrowingDegree` forecast** (Lifestyle category): Growing Degree Days (GDD) accumulation above a configurable base temperature (default 5C). Daily sum of max(mean_temp - base, 0) accumulated over the forecast horizon. Agricultural and gardening planning tool. Weather only, no Ensemble. Default disabled.
- **`CoastalFlood` alert** (Marine category): storm surge risk combining sea_level_height_msl deviation above a configurable threshold with high wave_height and strong onshore winds. Cross-domain Marine + Weather. Requires Ensemble for confidence. Default disabled.

## Capabilities

### New Capabilities
- `fire-risk`: Wildfire risk alert from multi-variable weather scoring (temperature, humidity, wind, soil moisture, precipitation history)
- `pollen-blast`: Pollen explosion prediction from weather transition detection combined with pollen concentration data
- `growing-degree`: Growing Degree Days accumulation forecast for agricultural planning
- `coastal-flood`: Storm surge risk alert combining marine conditions with weather data

### Modified Capabilities
- `thunderstorm-enhanced`: Existing ThunderstormAlert gains atmospheric stability modifiers (CIN + lifted index) for more precise prediction

## Impact

- **Njord.Features**: 4 new feature rules in `Alerts/FireRisk/`, `Alerts/PollenBlast/`, `Alerts/CoastalFlood/`, `Forecasts/GrowingDegree/` with config classes. Modified `Alerts/Thunderstorm/ThunderstormAlert.cs` and `ThunderstormConfig.cs`.
- **Njord.Features.Tests**: corresponding test files for all new and modified features.
- **Njord.Core/Domain/FeatureInputs.cs**: new cross-domain input types (`WeatherAqEnsembleInput` for PollenBlast, `MarineWeatherEnsembleInput` for CoastalFlood).
- **Njord.Features/ServiceCollectionExtensions.cs**: registration of new features.
- **protos/njord/v2/weather.proto**: new enrichment event oneof cases for the 4 new features; updated thunderstorm result fields.
- **Njord.Grpc/Mapping/**: proto mapping for new feature results.
- **Njord.Messages/Pipeline/**: new result records for each feature.
- **docs/data/features.json**: 4 new feature entries, updated thunderstorm entry.
- **docs/.vitepress/theme/builder/**: Config Builder updates for new features.
- **ha-njord**: new enrichment event handling for the 4 new features, updated thunderstorm event.

## Budget Impact

No additional API calls. All required variables are already fetched by the Weather, AirQuality, and Marine domain clients after Waves 1-2 add the missing params. These features only add analysis logic in the FeatureEngine pipeline.

## Non-goals

- Machine learning models or statistical forecasting beyond simple scoring.
- Historical data analysis or climate trends.
- Precipitation radar or nowcasting integration.
- Fire weather indices from external fire agencies (FWI).
