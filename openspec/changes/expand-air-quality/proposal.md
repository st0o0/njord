## Why

njord requests 10 variables from the Open-Meteo Air Quality API, but the endpoint offers considerably more at the same call cost. Carbon monoxide, sulphur dioxide, Saharan dust, ammonia, olive pollen, the US AQI and UV index are all served by the same `v1/air-quality` request. Users outside Europe get little from `european_aqi`, Mediterranean users miss olive pollen, and dust events (a common respiratory trigger in southern and central Europe) are invisible.

## What Changes

- `AirQualityParams` (`src/Njord.Forecast/AirQuality/`) gains flags: `CarbonMonoxide`, `SulphurDioxide`, `Dust`, `Ammonia`, `OlivePollen`, `UsAqi`, `UvIndex`, `UvIndexClearSky`.
- `AirQualityPoint` gains matching nullable fields and `HasFields` arms.
- `AirQualityHourlyData` (`src/Njord.OpenMeteo/AirQualityResponse.cs`) gains the matching JSON properties; `AirQualityParamsMappings` and `AirQualityResponseMappings` gain one arm per new member.
- New `AirQualityOptions.Region` (`Global` default, `Europe`, `UnitedStates`) in `Njord:AirQuality`. It decides which region-bound variables are requested: Europe adds `ammonia` and `olive_pollen` (CAMS Europe coverage only), UnitedStates adds `us_aqi`. `Global` requests none of them. Existing configs keep their current requests apart from the worldwide optional variables.
- New enrichment `DustAlert` (`IDomainHandler` in `src/Njord.AirQuality/`, new `AlertType.Dust` with three-step thresholds in `AlertOptions`), off by default. It requires `Dust` and triggers the AirQuality domain.
- The new raw values flow through the existing AirQuality wire (gRPC and MQTT per-domain payloads) as nullable members; the US AQI needs no handler.
- Config Builder, `docker-compose.example.yml` and `appsettings.Example.json` mirror the `Region` setting and the new toggle.

## API-budget impact

None for the call count. Air quality is one call (weight 1) per location per poll cycle regardless of the number of variables, so `BudgetCalculator` output is unchanged (for example 10 locations at a 60 min interval stay at about 7.2k calls/month, 2.4 % of 300k). Responses grow by a few kilobytes at most, so politeness toward the free service is unaffected. Task 1.1 re-verifies on the Open-Meteo docs that no variable-count weighting applies to the air-quality endpoint.

## Non-goals

- Custom AQI calculation (`european_aqi` and `us_aqi` are passed through as served).
- Historical air-quality data.
- Unit conversion of pollutant concentrations.
- Dedicated handlers for CO, SO2, ammonia, UV or the US AQI (raw values only in this change).
- Changing the behavior of existing configs (`Region` defaults to `Global`).

## Capabilities

### New Capabilities

- `air-quality-expansion`: new air-quality variables, `Region` option, `DustAlert` enrichment.

### Modified Capabilities

- `enrichment-config`: `DustAlert` toggle added.

## Impact

- **Njord.Forecast** (`src/Njord.Forecast/AirQuality/`): `AirQualityParams`, `AirQualityPoint`.
- **Njord.OpenMeteo** (`src/Njord.OpenMeteo/`): `AirQualityResponse.cs`, `Mapping/AirQualityParamsMappings.cs`, `Mapping/AirQualityResponseMappings.cs`.
- **Njord.Core** (`src/Njord.Core/Configuration/`): `AirQualityOptions` (+ validator), `EnrichmentOptions`, `AlertOptions` (dust thresholds); `Njord.Messages/Alerts` `AlertType.Dust`.
- **Njord.AirQuality**: `DustAlertRule`, handler registration in `ServiceCollectionExtensions`.
- **Njord.Grpc / Njord.Mqtt / protos**: nullable members and the new alert type wire name; ha-njord receives the new alert type as an additive change.
- **Docs**: Config Builder (`docs/.vitepress/theme/builder`, `docs/data/enrichment.json`), `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`.
