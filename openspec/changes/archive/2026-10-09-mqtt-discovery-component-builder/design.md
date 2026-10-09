## Context

`DiscoveryPayloadBuilder.cs` (613 lines) has a generic `BuildComponent(uniqueId, name,
ParameterDef parameter, valueTemplate, stateTopic, availabilityValueTemplate,
availabilityTopic, expireAfterSeconds)` helper, but it is typed around `ParameterDef`
(weather parameters with a `Unit`/`DeviceClass`) and a dual-availability template (base +
state-topic availability), which doesn't fit the five enrichment presenters' simpler shape
(single availability topic, optional unit/device-class as plain strings, optional
`binary_sensor` platform). Rather than reuse it, each presenter — and four now-dead methods
in `DiscoveryPayloadBuilder` itself (`BuildAlerts`/`BuildHistory`/`BuildIndices`/
`BuildTrends`) — independently hand-rolled the same component shape. Verified via `grep`:
`BuildAlerts`/`BuildHistory`/`BuildIndices`/`BuildTrends` have zero callers outside
`DiscoveryPayloadBuilderSpec.cs`.

## Goals / Non-Goals

**Goals:**
- Delete the ~280 lines of dead code in `DiscoveryPayloadBuilder` and their dead tests.
- Introduce one component-spec shape that covers both the existing `BuildComponent` use
  cases (model grid, consensus) and the five presenters' simpler shape, so there is exactly
  one way to build a Home Assistant sensor/binary_sensor component in this codebase.
- Zero change to any produced discovery payload — verified via existing Verify snapshots.

**Non-Goals:**
- Not changing `BuildDeviceEnvelope` or the overall device-envelope shape.
- Not changing any topic scheme (`TopicScheme.*`).
- Not touching `StatePayloadBuilder` (state-message building is a separate, already-clean
  path per `HistoryPresenter.ToStateMessages`).

## Decisions

1. **`SensorComponentSpec` record** (new, `Njord.Mqtt`):
   ```csharp
   public sealed record SensorComponentSpec(
       string Key,
       string Name,
       string StateTopic,
       string ValueTemplate,
       string Platform = "sensor",            // "sensor" | "binary_sensor"
       string? Unit = null,
       string? DeviceClass = null,
       string? JsonAttributesTopic = null,
       IReadOnlyList<(string Topic, string? ValueTemplate)>? ExtraAvailability = null);
   ```
   and `DiscoveryPayloadBuilder.ToComponent(SensorComponentSpec spec, string availabilityTopic,
   int expireAfterSeconds) -> JsonObject`, which produces exactly the JSON shape currently
   hand-written in each presenter (base `availability` entry + any `ExtraAvailability`
   entries, `availability_mode = "all"`).
   Alternative considered: keep `BuildComponent` as-is and add a second overload. Rejected —
   `BuildComponent`'s `ParameterDef`-typed signature is specific to the weather-model grid;
   a single spec record that both the model grid and the five presenters can construct (the
   model grid wraps a `ParameterDef` into a `SensorComponentSpec` at the call site) is
   simpler than maintaining two parallel component builders.
2. **Model grid and consensus builder migrate to the same spec** so there is only one
   component builder in the file; `BuildComponent` is removed in favor of `ToComponent`.
3. **Each presenter builds a `List<SensorComponentSpec>` from its existing local data**
   (`IndexPresenter`/`TrendPresenter`/`HistoryPresenter` already have `(key, name, unit)`-
   shaped tuples driving their current hand-rolled loops — these become the spec list
   directly) **and maps it through `ToComponent`**.
4. **Delete dead code first, then introduce the shared builder, then migrate presenters one
   at a time** — each presenter migration is independently verifiable against its existing
   Verify snapshot before moving to the next, minimizing the risk of a silent payload change.

## Risks / Trade-offs

- [Risk] `SensorComponentSpec`'s `ExtraAvailability` needs to cover `DerivedPresenter`'s
  two-entry availability array (base + state-topic-with-template), which is richer than the
  other four presenters' single-entry availability. → Mitigation: model it as
  `IReadOnlyList<(string Topic, string? ValueTemplate)>` from the start so one spec shape
  covers all six call sites (model grid, consensus, + 5 presenters... — model grid/consensus
  already use the two-entry form via `BuildComponent`).
- [Risk] Deleting `BuildAlerts`/`BuildHistory`/`BuildIndices`/`BuildTrends` removes test
  coverage that (incidentally) exercised a code path no production code uses — real coverage
  for alerts/history/indices/trends discovery payloads comes from each presenter's own spec
  file plus Verify snapshots; confirm those exist and are run before deleting the dead
  methods' tests. → Mitigation: tasks below start with confirming presenter-level snapshot
  coverage exists before any deletion.

## Migration Plan

Pure refactor, single deploy. Order: (1) confirm presenter snapshot coverage, (2) add
`SensorComponentSpec`/`ToComponent`, (3) migrate presenters one at a time with snapshot
verification after each, (4) delete the four dead methods + their dead tests last, once
nothing references them. Trivial revert at any step since no persisted/wire format changes.

## Open Questions

None.
