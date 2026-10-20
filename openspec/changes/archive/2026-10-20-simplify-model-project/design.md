## Context

Njord.Model holds ~15 types, several from the pre-redesign era. The v2 multi-domain architecture made `ModelCoverageRegistry`, `WeatherModel`, and `WeatherCondition` nearly dead. `ParameterDef`/`ParameterRegistry` are weather-client-specific but live in the shared vocabulary project.

## Goals / Non-Goals

**Goals:** Slim Njord.Model to pure vocabulary types. Relocate weather-client types to Njord.OpenMeteo. Remove dead types.
**Non-Goals:** Changing Ensemble request logic. Adding parameter registries for AQ/Flood.

## Decisions

### D1: ParameterDef/Registry/ResolvedParameterSet move to Njord.OpenMeteo as internal

They drive the Ensemble URL variable list. Making them internal in OpenMeteo scopes them correctly. `ParameterOptions.Resolve()` in Core/Configuration references `ParameterRegistry` — it needs to move or take the resolved set as input from OpenMeteo's setup.

### D2: ModelCoverageRegistry deleted, ServiceInfoMappings simplified

The catalog endpoint returns location names + enabled domains. No per-model capability data. `ServiceInfo` drops the `Models` field or keeps it as a simple string list from config (no coverage lookup).

### D3: WeatherModel removed, MQTT TopicScheme cleaned

MQTT already uses domain-based topics. Any residual `WeatherModel` reference in TopicScheme is removed. If `NjordOptions.Models` config stays (for optional individual model requests), it stores plain strings, not enum values.

### D4: Horizon kept if load-bearing in AlertDetails

If `Horizon.Hours(n)` is used in alert details (frost duration, hours_until), keep it as a thin TimeSpan wrapper. Otherwise replace with plain TimeSpan. Decision at implementation time.

### D5: WeatherCondition removed

The `WeatherCode` field on `WeatherPoint` is an `int` (WMO code). No enum needed.

## Risks / Trade-offs

- **[ParameterOptions.Resolve() references ParameterRegistry]** → Resolve moves to OpenMeteo or takes registry as parameter. Minor refactor.
- **[Models config uses WeatherModel enum]** → Switch to plain strings. Config doesn't change, just internal parsing.
