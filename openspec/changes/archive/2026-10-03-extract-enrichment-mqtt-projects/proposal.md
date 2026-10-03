## Why

Stage 3b of the FunkArr-style split of the single `Njord` assembly, the second half of the former stage 3 (stage 3a: `extract-pipeline-egress-projects`). After 3a, `Enrichment` and `Mqtt` are the only features left in the host. They cannot move as-is because they depend on each other: enrichment features build MQTT discovery JSON and state messages (`IEnrichmentFeature.BuildDiscoveryPayload` / `ToStateMessages`), and `Mqtt` consumes `IEnrichmentFeature`. Breaking that cycle is the real work of this stage.

**Status: ready — decision 2026-10-03: MQTT stays.**

## What Changes

- **Break the Enrichment<->Mqtt cycle by inversion** (design.md, Decision 1): features keep only computation (`TypeName`, `Enabled`, `Compute`/`CreateFlow`); everything that knows about Home Assistant/MQTT (device id, discovery payload, state messages) moves into per-type `IEnrichmentPresenter` implementations owned by `Njord.Mqtt`. `Njord.Enrichment` ends up with no MQTT knowledge. (A critical review against the code confirmed this stays the best option over neutral descriptors or builders-in-Core.)
- Add golden-master Verify snapshots for all five enrichment discovery payloads and state messages **before** moving any code, so wire-format equality is proven rather than assumed.
- Five feature presenters (`AlertPresenter`, `DerivedPresenter`, `TrendPresenter`, `IndexPresenter`, `HistoryPresenter`) plus `ConsensusPresenter` wrapping the existing builders, byte-identical; delete the two hardwired consensus branches in `MqttEgressActor` and `DiscoveryActor`; `consensus` stays a pipeline result and not an `IEnrichmentFeature` (Decision 8). Its discovery stays unconditional, as today.
- Trim `IEnrichmentFeature` to compute-only (`TypeName`, `Enabled`).
- New projects `src/Njord.Mqtt/` (14 files incl. `Transport/` and presenters) and `src/Njord.Enrichment/` (14 files incl. `Features/`), each referencing only `Njord.Core` (and the chain below), never each other and never the host. `Njord.Pipeline`/`Njord.Egress` already exist (3a).
- Cross-library actor lookups use the FunkArr-style `IXxxActor` markers from stage 1 (`IEgressActor`, `IPipelineActor`, `ISensorHubActor`, `IMqttConnectionActor`, ...); registration stays central in the host `NjordActorSystemSetup` (`RegisterMqttActors` gated on `Mqtt.Enabled`, `RegisterEnrichmentActors`) after `.WithSqlPersistence(...)`. Libraries expose `AddNjordMqtt()` / `AddNjordEnrichment()`.
- Delta specs restate every affected requirement completely (Decision 9), which also removes the drift the base `enrichment-feature-registry` spec has accumulated in the touched requirements.
- ArchUnit: Enrichment and Mqtt follow the library reference rules of 3a; Enrichment has no Mqtt dependency (added red first).
- Update `AGENTS.md` and the `njord-*` skills (`njord-enrichment-feature` is rewritten for the presenter contract).
- Plan only: Dockerfile restore/COPY lines for the two new csprojs; CI unaffected.

## Capabilities

### New Capabilities

- `mqtt-enrichment-presentation`: the `IEnrichmentPresenter` contract in `Njord.Mqtt` (per-`TypeName` discovery payload and state messages), one presenter per feature plus `consensus`, no `TypeName`-specific dispatch outside the registry, and byte-identical payloads.

### Modified Capabilities

- `enrichment-feature-registry`: `IEnrichmentFeature` loses `DeviceId`, `BuildDiscoveryPayload` and `ToStateMessages`; `DiscoveryContext` and the device-envelope helper belong to `Njord.Mqtt`; `IActorEnrichment` and DI registration are restated to match the code (`CreateFlow`, 5 features).
- `egress-event`: `MqttEgressActor` dispatches `EnrichmentUpdate` (including `consensus`) through presenters, not features.
- `enrichment-model-envelope`: the indices discovery requirement refers to the indices presenter.
- `activity-indices`: the HDD/CDD discovery exclusion refers to the indices presenter.
- `architecture-zone-enforcement`: new requirement for the Enrichment/Mqtt library rules (the 3a requirements are not repeated).

## Impact

- Prerequisites: stage 3a (`extract-pipeline-egress-projects`, archived 2026-10-02) applied, plus stages 1 and 2 and the per-library test split (`split-test-projects`, archived 2026-10-03). `architecture-zone-enforcement` exists in `openspec/specs`.
- Code: `src/Njord/{Mqtt,Enrichment}`, `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs`, `src/Njord/Njord.csproj`, `src/Njord.Core/Enrichment`, `src/Njord.Architecture.Tests` (`ZoneArchitectureSpec.cs`, `NjordArchitecture.cs`), host-resident specs `src/Njord.Tests/{Mqtt,Enrichment,Configuration}`, `src/Njord.Core.Tests/Configuration`, `src/Njord.slnx`, `Dockerfile`, `AGENTS.md`, `.claude/skills/njord-*`.
- API budget: 0 requests/month; no polling is added or altered (free-tier limits 300k/month, 10k/day untouched).
- Wire/persistence formats unchanged: MQTT discovery and state payloads byte-identical (proven by snapshots), persistence IDs, DTO `[JsonProperty]` names, actor names (`enrichment`, `mqtt-*`) and metric names stay identical.

## Non-goals

- Any behavior or wire-format change, new enrichment features, new MQTT entities.
- Extracting Pipeline/Egress (3a) or creating Core/Domain/Messages/Persistence/Grpc/Ingest/Sensors (stages 1-2).
- Neutral discovery descriptor types and moving MQTT payload builders into Core (both rejected in design.md).
- New per-library test projects for `Njord.Mqtt`/`Njord.Enrichment`: their specs stay in the host-resident `src/Njord.Tests` project (`InternalsVisibleTo` plus project references); a later change may split them.
- Changing the consensus discovery behavior (open question below), analyzer/`BannedSymbols.txt` rollout, CI workflow changes.
