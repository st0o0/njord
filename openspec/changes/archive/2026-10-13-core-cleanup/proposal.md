# Core Cleanup — typed constants, relocate misplaced helpers

## Problem

`Njord.Core` has grown into a catch-all: data registries, serialization helpers,
and MQTT utilities live alongside options and actor infrastructure. Meanwhile,
bare string literals for enrichment types, consensus methods, and parameter
lookups are scattered across Grpc, Mqtt, and Alerts — no compile-time safety,
silent breakage on typos.

### Symptoms

- **ModelCoverageRegistry** (290 LOC of hardcoded geodata) sits in Core but
  references zero Core types — it describes weather-model metadata.
- **HorizonProjection** builds JSON payloads using only `Njord.Weather` types
  but lives in `Core/Egress/`.
- **TopicSlug** is used exclusively by `Njord.Mqtt` but lives in Core.
- Enrichment type names (`"consensus"`, `"alerts"`, `"derived"`, `"trends"`,
  `"indices"`) appear as bare strings 16+ times across Grpc and Mqtt.
- `ConsensusOptions.Method` is a `string` validated against a hand-rolled
  `HashSet<string>{"Mean", "Median", "TrimmedMean"}`.
- `ParameterRegistry` has typed static properties for 20 hourly params, but
  4 hourly and 10 daily params are missing — Alerts and Grpc use
  `GetByApiName("rain")` etc. instead.

## Proposed changes

### Move 1 — ModelCoverageRegistry → Njord.Weather

Move `ModelCoverageRegistry`, `ModelCoverage`, `BoundingBox`, `CoverageTier`
from `Njord.Core/Configuration/` to `Njord.Weather/`. They have zero Njord.Core
dependencies. Weather is at the bottom of the reference chain — every consumer
already sees it.

### Move 2 — HorizonProjection → Njord.Weather

Move `HorizonProjection` from `Core/Egress/` to `Njord.Weather/`. It imports
only `Njord.Weather` types and `System.Text.Json`. Both current consumers
(Mqtt, Grpc) already reference Weather.

### Move 3 — TopicSlug → Njord.Mqtt

Move `TopicSlug` from `Core/Egress/` to `Njord.Mqtt/`. Its only production
consumer is `Mqtt/TopicScheme.cs`. It has zero Njord dependencies.

### Enum 1 — EnrichmentKind

Introduce `EnrichmentKind` enum (`Consensus`, `Alerts`, `Derived`, `Trends`,
`Indices`) in `Njord.Core`. Add a `Kind` property to `PipelineEvent` base.
Replace all bare type-name strings in Grpc and Mqtt with `EnrichmentKind`
references (`.ToString()` where a string is needed for topics/proto fields).

### Enum 2 — ConsensusMethod

Replace `ConsensusOptions.Method` (`string`) with a `ConsensusMethod` enum
(`Mean`, `Median`, `TrimmedMean`). ASP.NET Core binds enums from JSON/env vars
natively. The validator simplifies to a null/defined check.

### Fix — Complete ParameterRegistry typed properties

Add typed static properties for the 14 missing parameters:
- 4 hourly: `Rain`, `SoilTemperature0cm`, `Visibility`, `WindDirection10m`
- 10 daily: `PrecipitationSum`, `UvIndexMax`, `SnowfallSum`,
  `Temperature2mMax`, `Temperature2mMin`, `WindSpeed10mMax`,
  `WindGusts10mMax`, `Sunrise`, `Sunset`, `WeatherCode`

Then replace all `GetByApiName("...")` calls in Alerts and Grpc with the typed
properties.

## What stays

- **BudgetCalculator** stays in Core — the `NjordOptionsValidator` calls it,
  and moving it to `Njord.Budget` would create a circular reference. It is
  55 LOC and pure.
- **Meter name strings** (`"njord_mqtt_dedup_total"` etc.) — project-local,
  convention-driven, don't cross boundaries. Not worth the abstraction.
- **Config defaults** (`"homeassistant"`, `"njord"`, API base URL) — legitimate
  defaults in options classes, not magic strings.

## Non-goals

- Externalizing ModelCoverageRegistry data into a JSON/YAML resource file.
  The data changes rarely, is typed, and is easier to review as code.
- Restructuring Core's options/validators hierarchy. The options layer is
  well-organized; the problem was misplaced non-options types.
- Introducing a new shared project for egress utilities. Two moves into
  existing projects (Weather, Mqtt) solve it without new assemblies.

## API-budget impact

None — no change to polling, request patterns, or API calls.

## Risks

- **Namespace changes break `using` statements** in consumers. Low risk —
  the compiler catches every broken reference immediately. No runtime
  surprises.
- **Test relocation** — test classes in `Njord.Core.Tests` for moved types
  need to move to the corresponding test project. Straightforward.
