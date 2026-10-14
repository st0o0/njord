## Context

`ForecastPipeline` builds a `ConsensusSnapshot` per forecast inside its `StatefulSelectMany`
closure, passes it to the four analyzers and unconditionally emits
`PipelineEvent.ConsensusComputed`. The four analyzers each read their own `Enabled` from
`IOptions<T>` at construction and return `null` when disabled; the pipeline only emits
non-null results. Hot analysis parameters travel as `AnalysisSettings` (zipped onto every
forecast, replaced by `AnalysisSettingsChanged` from `ConfigManager`); `ConsensusSettings`
carries only method, trim percent and quorum. `ConfigReloadPolicy` already classifies
`ConsensusOptions.Enabled` as restart-required, like every other feature toggle, and
`ConfigManager` already reports it that way.

Downstream, consensus is consumed only through the event:
- `ForecastReadModel` stores the latest event per kind; an absent kind stays `null`
  (`LatestAnalysisResult.Consensus`), already covered by `ForecastReadModelSpec`.
- `GetEnrichments` mapping omits `consensus`/`consensus_updated_at` when it is `null`
  (covered by `AnalysisProtoMappingsSpec`); `StreamEnrichments` maps events one by one.
- `MqttStatePublisher` publishes consensus state only on a `ConsensusComputed` event; there is
  no discovery payload and no static consensus device set to keep in sync.

So suppressing the event is sufficient: no consumer emits stale or empty consensus.

## Goals / Non-Goals

**Goals:**
- One source of truth for the consensus toggle, read the same way as the other four.
- Unchanged analysis results for every other feature when consensus output is off.

**Non-Goals:**
- A shared "feature toggle" abstraction across analyzers.

## Decisions

### Suppress the output, keep the computation

The snapshot is the input of alerts, derived, trends and indices, and trends need the previous
snapshot. The pipeline therefore keeps building the snapshot and updating `previous` on every
forecast and only skips adding the `ConsensusComputed` event. Alternative: skip
`ConsensusBuilder.Create` when disabled — rejected, it would silently disable all analyses.

### Read the toggle at construction, restart-required

`ForecastPipeline` takes `IOptions<ConsensusOptions>` and captures `Enabled` once, mirroring
`AlertAnalyzer`/`IndexAnalyzer`/`TrendAnalyzer`/`DerivedAnalyzer`. This matches the existing
`ConfigReloadPolicy` classification and the `options-hot-reload` requirement that feature
toggles are restart-required.

Alternatives considered:
- Add `Enabled` to `ConsensusSettings` so it travels with `AnalysisSettingsChanged`: it would
  make this one toggle hot while policy and `ConfigManager` report it as restart-required, and
  diverge from the other four toggles.
- Move the flag into `ConsensusBuilder`: the builder lives in `Njord.Compute`, which must not
  reference `Njord.Core` options, and its result is needed regardless of the flag.

### Tests

Pipeline-level specs in `src/Njord.Pipeline.Tests` drive a fake gateway queue (same harness as
`ForecastPipelineSettingsSpec`) with all four analyzers enabled and consensus disabled, and
assert the event kinds per forecast; one spec asserts the default still emits consensus; one
sends an `AnalysisSettingsChanged` to prove the runtime path does not re-enable it. A
`ConfigManagerSpec` case asserts the consensus toggle is reported as restart-required.

## Risks / Trade-offs

- [Retained consensus states from before the switch stay on the broker] → out of scope;
  documented in the proposal's non-goals.
- [New constructor parameter breaks direct constructions] → only two specs construct the
  pipeline directly; DI resolution via `resolver.Props<ForecastPipeline>()` picks up the
  already registered `IOptions<ConsensusOptions>`.

## Migration Plan

None. The default (`Enabled = true`) keeps the current behaviour; rollback is a revert.
