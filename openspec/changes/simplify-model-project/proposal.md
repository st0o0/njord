## Why

Njord.Model was built for a weather-only, multi-model architecture. After the v2 redesign (Ensemble-based, multi-domain), several types are dead weight or misplaced. `ModelCoverageRegistry` (13KB of per-model capability data), `WeatherModel` enum, and `WeatherCondition` are barely used. `ParameterDef`/`ParameterRegistry` are weather-client-internal concerns that don't belong in the shared vocabulary project. The goal is a lean Model project containing only domain-neutral vocabulary types.

## What Changes

- **Remove:** `ModelCoverageRegistry` (13KB, used only by stale ServiceInfoMappings catalog logic), `WeatherModel` enum (only legacy MQTT model topic), `WeatherCondition` enum (1 prod usage, likely dead).
- **Move to Njord.OpenMeteo:** `ParameterDef`, `ParameterRegistry`, `ResolvedParameterSet` — these drive the Ensemble request URL and are weather-client-internal. AQ/Flood clients have their own hardcoded variables.
- **Move to Njord.Core/Configuration:** `ParameterSelection` (config concern, not vocabulary).
- **Simplify:** `Horizon` — evaluate replacing with plain `TimeSpan` (3 prod usages). Simplify `ServiceInfoMappings` to not use `ModelCoverageRegistry`. Remove model validation from `NjordOptionsValidator`. Clean up MQTT `TopicScheme` to not reference `WeatherModel`.
- **After this change, Njord.Model contains only:** `Location`, `DomainKind`, `TimeResolution`, `IHasValidTime`, `DomainForecast<T>`, `IDomainForecast`, `WeatherPoint`, `AirQualityPoint`, `FloodPoint`, `SolarPoint`, `MarinePoint`.

## Non-goals

- Changing the Ensemble request logic (just relocating its support types).
- Removing `Horizon` if it's load-bearing in alert details.
- API budget impact: zero.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `parameter-registry`: Types move from Njord.Model to Njord.OpenMeteo (internal).
- `service-configuration`: Model validation simplified, catalog simplified.

## Impact

- **Njord.Model**: Drops from ~15 types to ~11 pure vocabulary types.
- **Njord.OpenMeteo**: Gains ParameterDef, ParameterRegistry, ResolvedParameterSet as internal types.
- **Njord.Core**: Gains ParameterSelection in Configuration/.
- **Njord (host)**: ServiceInfoMappings simplified, NjordOptionsValidator simplified.
- **Njord.Mqtt**: TopicScheme updated (no WeatherModel reference).
- **Njord.Model.Tests**: Tests for moved types move to Njord.OpenMeteo.Tests.
