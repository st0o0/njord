## Why

A cross-codebase review against the sibling FunkArr project found that njord's MQTT
discovery-payload code has two compounding problems: (1) genuine **dead production code** —
`DiscoveryPayloadBuilder.BuildAlerts`/`BuildHistory`/`BuildIndices`/`BuildTrends`/`BuildDerived`
(~390 of the file's 613 lines) are called from nowhere in production (verified: `grep` for each
method name outside the file itself only finds calls from `DiscoveryPayloadBuilderSpec.cs`
tests — `MqttDiscoveryActor` and the presenter classes never call them), and (2) the actual
production code paths (`AlertPresenter`, `DerivedPresenter`, `IndexPresenter`,
`TrendPresenter`, `HistoryPresenter` in `src/Njord.Mqtt/Presentation/`) each independently
hand-roll the same ~10-field Home-Assistant-sensor `JsonObject` shape (40 near-identical
inline literals across the five files) instead of reusing the generic `BuildComponent`
helper that already exists in `DiscoveryPayloadBuilder` and is used correctly by `Build`
(model grid) and `ConsensusPresenter`. FunkArr solves the equivalent "map many similar
items" problem (`SabnzbdResponseMapper.BuildQueueSlot`/`BuildHistorySlot`) with one record +
one small pure mapping function per shape — no copied literals, no dead alternates.

## What Changes

- **Remove dead code**: delete `DiscoveryPayloadBuilder.BuildAlerts`, `BuildHistory`,
  `BuildIndices`, `BuildTrends` (lines ~155-434) and the corresponding test cases in
  `Njord.Mqtt.Tests/DiscoveryPayloadBuilderSpec.cs` that exercise only those four methods.
  **BREAKING** (internal API only — these are `public static` but unused outside this
  project and not part of any external contract).
- **Introduce a shared sensor-component spec**: add a `SensorComponentSpec` record (key,
  name, stateTopic, valueTemplate, platform, unit?, deviceClass?, availability
  topic(s)) and a `ToComponent(SensorComponentSpec)` builder function to
  `DiscoveryPayloadBuilder`, generalizing the existing `BuildComponent` helper so it covers
  the shapes currently hand-rolled in the five presenters (binary_sensor platform, no
  `ParameterDef`-typed unit/device-class, variable availability templates).
- **Rewire the five presenters** (`AlertPresenter`, `DerivedPresenter`, `IndexPresenter`,
  `TrendPresenter`, `HistoryPresenter`) to build a list of `SensorComponentSpec` values (the
  per-feature data that currently drives each hand-rolled block — parameter/key/name/unit
  tuples already exist as local arrays in `IndexPresenter`/`TrendPresenter`/`HistoryPresenter`)
  and map them through `ToComponent`, instead of writing `new JsonObject { ... }` per
  component.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `mqtt-enrichment-presentation`: the existing "Presented payloads are byte-identical to the
  pre-split output" requirement already covers the behavior-preservation constraint for this
  change (same Verify snapshots must still pass) — no change to that requirement's text. One
  new requirement is added: presenter implementations SHALL build sensor components through
  a shared spec/builder rather than inline `JsonObject` literals, and `DiscoveryPayloadBuilder`
  SHALL NOT expose unused public methods.

## Impact

- `src/Njord.Mqtt/DiscoveryPayloadBuilder.cs` (shrinks from 613 lines; dead methods removed,
  `BuildComponent` generalized into `ToComponent(SensorComponentSpec)`)
- `src/Njord.Mqtt/Presentation/AlertPresenter.cs`
- `src/Njord.Mqtt/Presentation/DerivedPresenter.cs`
- `src/Njord.Mqtt/Presentation/IndexPresenter.cs`
- `src/Njord.Mqtt/Presentation/TrendPresenter.cs`
- `src/Njord.Mqtt/Presentation/HistoryPresenter.cs`
- `src/Njord.Mqtt.Tests/DiscoveryPayloadBuilderSpec.cs` (remove dead-method test cases, add
  `ToComponent`/`SensorComponentSpec` cases)
- Presenter-level golden-master/snapshot tests (if any, per `dotnet-skills:snapshot-testing`
  convention in AGENTS.md) must be re-verified to confirm byte-identical payloads.

## Non-goals

- No change to the Home Assistant discovery payload *content* (topics, unique IDs, field
  values) for any enrichment feature — this is a pure internal deduplication.
- Not touching `DiscoveryPayloadBuilder.Build`/`BuildConsensus`/`BuildDeviceEnvelope`, which
  are already correctly used and already share `BuildComponent` (soon `ToComponent`).
- Not addressing the `Njord.Grpc` adapter-layer duplication (separate change,
  `grpc-thin-adapters`).

## API budget impact

None. This change touches only MQTT discovery-payload construction; it does not affect
Open-Meteo polling behavior or request volume.
