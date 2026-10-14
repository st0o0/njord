## Why

`ConsensusOptions.Enabled` is documented, persisted, editable over the admin API and listed as
an active analysis, but `ForecastPipeline` never reads it: a `ConsensusComputed` event is
emitted for every forecast even when consensus is switched off. The `hourly-consensus` spec
already requires the toggle to control the consensus output, and AGENTS.md promises that every
analysis feature is independently toggleable, so the running service contradicts both.

## What Changes

- `ForecastPipeline` reads `ConsensusOptions.Enabled` at construction (like every analyzer
  reads its own `Enabled`) and emits `PipelineEvent.ConsensusComputed` only when it is true.
- The `ConsensusSnapshot` is still built for every forecast: alerts, derived values, trends and
  indices keep consuming it, and trends keep receiving the previous snapshot.
- With consensus disabled, no consensus state reaches MQTT, `GetEnrichments` carries no
  `consensus` field and `StreamEnrichments` emits no `consensus` events.
- The toggle stays restart-required, as `ConfigReloadPolicy` already classifies it, matching
  the other feature toggles. Consensus parameters (method, trim percent, quorum) stay hot.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `hourly-consensus`: the "Consensus output is independently toggleable" requirement gains
  scenarios for the other features still being emitted, trends keeping their previous
  snapshot, and the toggle being read at startup (restart-required).

## Impact

- Code: `src/Njord.Pipeline/ForecastPipeline.cs` (new `IOptions<ConsensusOptions>`
  constructor parameter, conditional emission); pipeline specs in `src/Njord.Pipeline.Tests`.
- Behaviour: with `Njord:Enrichment:Consensus:Enabled = false`, Home Assistant (ha-njord over
  gRPC) and MQTT no longer receive consensus data. With the default (`true`) nothing changes.
- No proto, persistence, MQTT topic or golden-master changes.
- API budget: none. Polling is untouched (0 additional requests/month against the 300k
  free-tier limit).

## Non-goals

- Making feature `Enabled` toggles hot-reloadable; all five stay restart-required.
- Clearing consensus MQTT states that a broker still retains from before the toggle was
  switched off.
- Skipping the consensus computation itself; the other analyses depend on it.
- Changing what `ActiveAnalyses` or the config snapshot report for the toggle.
