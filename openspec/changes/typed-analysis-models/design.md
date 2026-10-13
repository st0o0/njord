## Context

See proposal.md for motivation. Facts that shape the approach:

- Horizon keys are built as strings in `Njord.Weather/HorizonProjection.cs`,
  `Njord.Compute/Analysis/ConsensusSnapshotFactory.cs`, `TimeSliceAggregator.cs`, and read
  in `AlertEvaluator` (`$"h{n}"`), `StatePayloadBuilder` (topic segments, `$"d{offset}"`)
  and `EnrichmentProtoMapper` (proto `horizon` fields).
- Analysis records carry Newtonsoft `[JsonProperty]` annotations, but no production code
  calls `JsonConvert` and no persistence DTO embeds them (`Njord.Persistence` holds only
  Budget and Pipeline events). The annotations are dead.
- The Verify golden masters the old docs refer to no longer exist. Only orphaned
  `*.verified.txt` files remain in `src/Njord.Mqtt.Tests` (four of them for MQTT
  discovery, which was removed). Today nothing protects the wire format during a refactoring.
- `StatePayloadBuilder` and `EnrichmentProtoMapper` are pure static functions, so they can
  be snapshot-tested directly without actors or a broker.

## Goals / Non-Goals

**Goals:**
- A wire-format safety net that the next three changes also rely on.
- Typed keys and typed categorical values everywhere between ingest and the output mappers.

**Non-Goals:**
- Splitting classes or moving logic (`restructure-compute`).
- Changing options classes; `SeverityThresholds` is introduced here as a compute-side type,
  and building it from configuration is wired in `split-options-per-feature`. Until then
  the alert code builds it from the existing arrays at the call site.

## Decisions

### D1: Golden masters first, in their own task group
They are written and approved against the current code before any model changes. Every
later task group must keep them green without re-approval. A re-approval is a review
signal, not a routine step.
*Alternative:* rely on the existing unit specs. They assert single values, not the full
payload, and would miss key or format drift.

### D2: `Horizon` as a `readonly record struct` with a kind and an offset
`Horizon.Hours(int)` and `Horizon.Days(int)`, plus `ToWireKey()` (`h12`/`d1`) and
`TryParseWireKey` for the rare inbound case. `ByHorizon` becomes
`IReadOnlyDictionary<Horizon, HorizonConsensus>`. It lives in `Njord.Weather` because
`HorizonProjection` already produces horizon-keyed data there.
*Alternative:* two separate types `HourOffset`/`DayOffset`. Rejected because several
dictionaries hold both kinds together (`HorizonProjection` output).

### D3: Alert details as a closed record hierarchy
`Alert` keeps `Type`, `Severity`, `Confidence`, `TriggerValue`, `Threshold`, `PeakValue`,
`HoursUntil`, `DurationHours`, and gains `AlertDetails Details`. `AlertDetails` is an
abstract record with one sealed subtype per alert type (`FrostDetails`, `HeatDetails`, …)
and a `NoDetails` singleton for `Alert.None`. The MQTT and gRPC mappers each own one
`switch` that turns details into the existing attribute keys.
*Alternative:* subclass `Alert` per type (`FrostAlert : Alert`). Rejected because
`Alert.None(type)` and the shared severity/confidence handling would need to be duplicated
or generic, while a details payload keeps `Alert` uniform.

### D4: Enums plus edge formatting extensions
`TrendDirection`, `Stability`, `WeatherCategory`, `DewPointComfort`, `WeatherCondition`
and `IndexKind` are enums. Their wire text lives in `ToWireName()` /
`ToDescription()` extension methods next to the enum (pure lookups, like the existing
`EnrichmentKind.ToWireName()`), and consumers call them. The WMO description table moves
from `DerivedComputer` to `WeatherCondition` extensions.

### D5: `LocationId` with case-insensitive equality
A `readonly record struct LocationId(string Name)` with overridden equality using
`StringComparer.OrdinalIgnoreCase`. It is introduced in analysis results and the
preference key. Messages and options keep plain strings until
`separate-messages-from-api`.

### D6: Drop `[JsonProperty]` and Newtonsoft from Compute and Weather
This removes noise and the dependency. `Njord.Weather` keeps Newtonsoft only if
`ModelCoverageRegistry` or the ingest DTOs need it (the task checks this).

## Risks / Trade-offs

- [Unknown score names now fail validation, where before they were probably ignored] →
  The validation error names the key. The release note lists it as a config-visible
  tightening.
- [Golden-master fixture misses a branch, so a drift slips through] → The fixture
  requirement lists every alert type, index kind, trend/derived field and a null value.
  The task checks coverage by counting the produced topics.
- [Large mechanical diff across many projects] → Task groups are ordered so each one
  compiles and passes the golden masters on its own.

## Migration Plan

Internal only. No config, topic, proto or persistence change, so there is no deployment
step or rollback concern beyond reverting the commit.
