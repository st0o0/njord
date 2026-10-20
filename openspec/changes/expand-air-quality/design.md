## Context

`AirQualityParams` has 9 flags; `AirQualityClient` requests variables built by `AirQualityParamsMappings.ToApiNames`. Handlers declare `DomainRequirements` (required and optional param flags) and `EnrichmentResolution` unions them to decide what is requested. Pollen and `european_aqi` are already requested. The new variables are served by the same endpoint (`v1/air-quality`, CAMS Europe and CAMS Global models).

## Goals / Non-Goals

**Goals:** expose the additional Open-Meteo air-quality variables, make them region-aware, add one useful alert (dust).

**Non-Goals:** custom AQI, historical data, handlers beyond dust.

## Decisions

### D1: Flags, points and DTOs follow the existing pattern

One flag, one nullable `AirQualityPoint` member, one `HasFields` arm, one DTO property and one mapping arm per variable (`carbon_monoxide`, `sulphur_dioxide`, `dust`, `ammonia`, `olive_pollen`, `us_aqi`, `uv_index`, `uv_index_clear_sky`). All switches throw `ArgumentOutOfRangeException` by default and each is pinned by a spec per member (AGENTS.md enum rules). The flag enum reaches 17 members, still within `int`.

### D2: Region decides what is requested

`ammonia` and `olive_pollen` have CAMS Europe coverage only and `us_aqi` is meaningful only for US users. Requesting them elsewhere returns nulls and wastes payload. `AirQualityOptions.Region` (enum `AirQualityRegion { Global, Europe, UnitedStates }`, default `Global`, validated) filters the region-bound optional flags when the request is composed. The filter lives in one place (request composition for AirQuality, not in each handler) so handlers keep declaring what they need. A null value for a region-bound field is never an error. The enum has explicit wire text through an extension switch, not `ToString()`.

Rejected alternative: infer the region from coordinates. It is implicit and hard to test; an explicit option is simpler and operators know where they live.

### D3: Worldwide variables are optional, never required

CO, SO2, dust and UV are requested as optional flags whenever an AirQuality handler is enabled, so enabling `Pm25Alert` alone costs no extra call and does not fail when a variable is missing. Only `DustAlert` declares `Dust` as required.

### D4: DustAlert is the only new handler

`DustAlertHandler` follows `AirQualityAlertHandler` with a `DustAlertRule` and a new `AlertType.Dust` (three-step severity thresholds in `AlertOptions`, validated by `AlertOptionsValidator`; initial thresholds in ug/m3 are chosen in task 3.2 from CAMS dust guidance). Adding an `AlertType` is an additive wire change (proto enum, `ToWireName`, MQTT topic, ha-njord). `ConfigKey` `DustAlert` must exist in `EnrichmentOptions` (`EnrichmentConfigKeySpec`). Off by default.

A `USAqiHandler` is not added: `us_aqi` is passed through as a raw index and consumers can use it directly.

### D5: Wire exposure is additive

New members are nullable in the AirQuality payload messages (extend-only proto fields and MQTT JSON keys); existing consumers are unaffected. Field numbers and wire text are chosen in task 4.1 and pinned by golden masters.

## Risks / Trade-offs

- CAMS Europe variables are null outside Europe -> mitigated by `Region`.
- Another `AlertType` grows the ha-njord surface -> off by default, additive.
- Variable naming drift in the API -> task 1.1 verifies names and units and records them here.
