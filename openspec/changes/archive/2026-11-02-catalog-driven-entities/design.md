## Context

ha-njord registers domain sensors using a hardcoded `DOMAIN_SENSORS` tuple in `sensor.py`. Every sensor defined for a domain is created when that domain has data, regardless of whether njord actually requests the underlying API variable. Combined with non-optional proto fields defaulting to 0.0, this produces sensors displaying false readings (PM10=0.0, wave_period=0.0) when no feature requires those variables.

njord already knows exactly which variables it fetches -- the resolved `DomainRequirements` (the OR of all enabled features' requirements) drives the API requests. This information just never reaches ha-njord.

## Goals / Non-Goals

**Goals:**
- Expose resolved domain schemas in GetCatalog so ha-njord knows which fields carry data
- Make proto fields nullable (`optional`) so absent data is distinguishable from real 0.0
- ha-njord creates domain sensors only for fields in the catalog schema

**Non-Goals:**
- Changing which features require which variables (separate decision per feature)
- Restructuring the WeatherEntity or enrichment entity registration
- Adding a live schema-update mechanism (catalog is read at connect time; config changes require restart)

## Decisions

### D1: Domain schema goes into LocationInfo, not a separate RPC

**Choice:** Add `repeated DomainSchema resolved_domains` to `LocationInfo` in `common.proto`.

**Alternative:** A new `GetSchema` RPC returning resolved requirements.

**Rationale:** The domain schema is per-location metadata, same as the model list. It's small (a few dozen strings), changes only on restart (config-driven), and is needed exactly when the catalog is read. A separate RPC adds latency and complexity for no benefit.

### D2: Params-to-proto-field-name mapping as a static dictionary per domain

**Choice:** One `FrozenDictionary<*Params, string>` per domain in a new `ParamFieldNames` static class in `Njord.Grpc`, mapping each flag to its proto field name.

**Alternative:** Derive field names from the `*Params` enum member names via naming convention (PascalCase to snake_case).

**Rationale:** The mapping is not always a simple case conversion (`Pm25` -> `"pm25"`, not `"pm_25"`; `BirchPollen` -> `"birch_pollen"`). An explicit dictionary is safer and serves as documentation. It lives next to the proto mapping code.

### D3: All domain sample numeric fields become optional

**Choice:** Mark every numeric field in `ForecastSample`, `AirQualitySample`, `MarineSample`, `FloodSample`, `SeasonalSample`, and `EnsembleSample` as `optional` except `valid_at` (always present) and `weather_code` (always requested as a base param).

**Alternative:** Only make the currently-problematic fields optional (pm10, wave_period, etc.).

**Rationale:** Any field could become unrequested if its feature is disabled. Making all fields optional is the correct proto3 contract -- the server sets only what it has. This is a one-time breaking change while we're still 0.x.

### D4: ha-njord sensor registration driven by catalog schema fields

**Choice:** Replace the static `DOMAIN_SENSORS` tuple with a `DOMAIN_SENSOR_REGISTRY` dict keyed by `(domain, field_name)`. At setup time, iterate the catalog's `resolved_domains` and create sensors only for fields present in the registry.

**Alternative:** Keep `DOMAIN_SENSORS` and filter at registration time.

**Rationale:** The registry pattern makes the relationship explicit: "if this field appears in the schema, create this sensor." Adding a new sensor is still a one-line addition to the registry.

### D5: Proto field names as the lingua franca

**Choice:** The domain schema carries proto field names (snake_case, matching the proto message field names). ha-njord matches them against sensor descriptions. The C# `*Params` flags map to these names in `ParamFieldNames`.

**Rationale:** Proto field names are the stable wire contract between the two repos. They already match ha-njord's `DomainSensorDescription.field` values. No new naming convention needed.

## Risks / Trade-offs

- **Breaking proto change** -> Mitigation: njord and ha-njord deploy together; both at 0.x so breaking changes are acceptable. The proto field number does not change, only the presence annotation.
- **ParamFieldNames dictionary maintenance** -> Mitigation: Architecture tests can verify that every `*Params` flag has a mapping. New params added for new features automatically need an entry.
- **Catalog size increase** -> Negligible: a few hundred bytes of field name strings per location. Catalog is fetched once at connect time.

## Open Questions

None -- the explore session resolved the key decisions.
