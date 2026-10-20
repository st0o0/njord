## Context

The config carries dead properties (Models, Horizons) and over-exposed internal complexity (Parameters Groups/Extra/Exclude). After enrichment-driven config, EnabledDomains is gone. This change completes the simplification.

## Goals / Non-Goals

**Goals:** Minimal user config (Location names + MQTT), internalize parameter system, remove dead properties.
**Non-Goals:** Changing enrichment toggles, polling, or persistence config.

## Decisions

### D1: WeatherOptions replaces ParameterOptions + ForecastDays

New `WeatherOptions` with `IncludeSolar: bool` (default false) and `ForecastDays: int` (default 4). The weather client reads these and builds its own variable list. AQ/Flood/Marine clients have their variables hardcoded already — only weather needs the solar toggle.

### D2: WeatherClient publishes its variable list

`WeatherClient` exposes a `IReadOnlyList<string> HourlyVariables` property. MQTT reads this instead of `ResolvedParameterSet`. Budget weight = `Math.Ceiling(HourlyVariables.Count / 10.0)`. This breaks the ParameterRegistry dependency chain.

### D3: Catalog shows "Ensemble Mean" only

`ServiceInfo.Models` becomes empty or contains one entry "ensemble_mean". No per-model coverage data. `CatalogModel` record simplified or removed.

### D4: StorageOptions merged into PersistenceOptions

`PersistenceOptions.DataPath` replaces `StorageOptions.PersistencePath`. One section instead of two. `Njord:Storage:PersistencePath` → `Njord:Persistence:DataPath`.

### D5: LocationOptions simplified

Only `Name`, `Latitude` (optional, geocoding fills it), `Longitude` (optional). No `Models` property.

## Risks / Trade-offs

- **[Breaking: Models config removed]** → Users with `Njord:Models` env vars get a startup warning that the key is ignored.
- **[Breaking: Parameters config removed]** → Users with custom Groups/Extra/Exclude lose that capability. Mitigation: IncludeSolar covers the main use case. Power users can request variable additions as issues.
- **[MQTT variable list change]** → If WeatherClient's hardcoded list differs from what users had configured, MQTT topics change. Mitigation: the hardcoded list matches the "Weather" group default.
