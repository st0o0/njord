## Why

Stage 3 of the FunkArr-style split of the single `Njord` assembly. After `extract-core-projects` (stage 1: `Njord.Domain`, `Njord.Persistence`, `Njord.Messages`, `Njord.Core`) and `extract-leaf-feature-projects` (stage 2: `Njord.Grpc`, `Njord.Ingest`, `Njord.Sensors`), four entangled features remain in the host: `Pipeline`, `Egress`, `Enrichment`, `Mqtt`. They cannot move as-is because `Enrichment` and `Mqtt` depend on each other: enrichment features build MQTT discovery JSON and state messages (`IEnrichmentFeature.BuildDiscoveryPayload` / `ToStateMessages`), and `Mqtt` consumes `IEnrichmentFeature`. Breaking that cycle is the real work of this stage; the rest is mechanical moves.

## What Changes

- **Break the Enrichment↔Mqtt cycle by inversion** (design.md, Decision 1): features keep only computation (`TypeName`, `Enabled`, `Compute`/`CreateFlow`); everything that knows about Home Assistant/MQTT (device id, discovery payload, state messages) moves into per-type `IEnrichmentPresenter` implementations owned by `Njord.Mqtt`. `Njord.Enrichment` ends up with no MQTT knowledge.
- New projects `src/Njord.Egress/`, `src/Njord.Pipeline/`, `src/Njord.Mqtt/`, `src/Njord.Enrichment/`, each referencing only `Njord.Core`, never each other and never the host.
- Move `src/Njord/Egress/*` (6 files), `src/Njord/Pipeline/*` (12), `src/Njord/Mqtt/*` (14 incl. `Transport/`) and `src/Njord/Enrichment/*` (13 incl. `Features/`) into the new projects.
- Cross-library actor lookups use the marker keys introduced in stage 1/2 (`ActorRegistry.Get<TMarker>()`) instead of actor classes (`EgressActor`, `PipelineActor`, `SensorHubActor`, `MqttConnectionActor`).
- Each library exposes `AddNjordX()` (services) and `WithXActors()` (Akka.Hosting); host setup files only call them. `WithSqlPersistence` and persistence-provider selection stay in the host and run before the persistent actors register.
- Add golden-master Verify snapshots for all five enrichment discovery payloads and state messages **before** moving any code, so wire-format equality is proven rather than assumed.
- Present `consensus` through the same presenter registry (`ConsensusPresenter` wrapping the existing builders, byte-identical) and delete the two hardwired consensus branches in `MqttEgressActor` and `DiscoveryActor`; `consensus` stays a pipeline result and is not an `IEnrichmentFeature` (design.md, Decision 8). Its discovery stays unconditional, as today.
- Delta specs restate every affected requirement completely (design.md, Decision 9), which also removes the drift the base `enrichment-feature-registry` spec has accumulated in the touched requirements (`CreateFlow`, `ToStateMessages(..., location)`, `DiscoveryContext`, 5 registered features).
- Rewrite ArchUnit rules per assembly: lateral references between feature libraries are forbidden (the compiler stops cycles and upward references, not lateral ones); convention rules (sealed, `Spec` suffix) run over all `Njord.*` assemblies.
- Update `AGENTS.md` (structure and guardrails) and the `njord-*` skills (the `njord-enrichment-feature` skill is rewritten for the presenter contract).
- Plan only (executed as tasks, no behavior change): Dockerfile restore/COPY lines for the new csprojs; CI is unaffected (solution-level build/test).

## Capabilities

### New Capabilities

- `mqtt-enrichment-presentation`: the `IEnrichmentPresenter` contract in `Njord.Mqtt` (per-`TypeName` discovery payload and state messages), one presenter per feature plus `consensus`, no `TypeName`-specific dispatch outside the registry, and byte-identical payloads.

### Modified Capabilities

- `enrichment-feature-registry`: `IEnrichmentFeature` loses `DeviceId`, `BuildDiscoveryPayload` and `ToStateMessages`; `DiscoveryContext` and the device-envelope helper belong to `Njord.Mqtt` and are consumed by presenters; `IActorEnrichment` and the DI-registration requirement are restated to match the code (`CreateFlow`, 5 features).
- `egress-event`: `MqttEgressActor` dispatches `EnrichmentUpdate` (including `consensus`) through presenters, not features.
- `enrichment-model-envelope`: the indices discovery requirement refers to the indices presenter.
- `activity-indices`: the HDD/CDD discovery exclusion refers to the indices presenter.
- `architecture-zone-enforcement`: dependency rules run per assembly with lateral-reference checks; sealed convention covers all `Njord.*` production assemblies.

## Impact

- Prerequisites: `extract-core-projects` and `extract-leaf-feature-projects` applied (Core owns options, `StreamSupervision`, `NjordHealthState`, `NjordMetrics`, `StreamConsumerActor`, `TopicSlug`, `HorizonProjection`, actor marker keys; Messages owns `EgressEvent`, `Request*/…Response` messages and Pipeline/Budget/Sensor messages). `zone-architecture-tests` applied and archived (its spec is the base for the `architecture-zone-enforcement` delta).
- Code: `src/Njord/{Egress,Pipeline,Mqtt,Enrichment}`, `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs`, `src/Njord/Njord.csproj`, `src/Njord.Tests/{Architecture,Egress,Pipeline,Mqtt,Enrichment}`, `src/Njord.Tests.Shared`, `src/Njord.slnx`, `Dockerfile`, `AGENTS.md`, `.claude/skills/njord-*`.
- API budget: none; no polling is added or altered (0 requests/month against the 300k free-tier limit).
- Wire/persistence formats unchanged: MQTT discovery and state payloads byte-identical (proven by snapshots), persistence IDs, DTO `[JsonProperty]` names, actor names (`scheduler`, `budget-tracker`, `egress`, `model-state`, `pipeline`, `enrichment`, `mqtt-*`) and metric names stay identical.

## Non-goals

- Any behavior or wire-format change, new enrichment features, new MQTT entities.
- Creating `Core/Domain/Messages/Persistence` (stage 1) or extracting `Grpc/Ingest/Sensors` (stage 2).
- Neutral discovery descriptor types (rejected in design.md) and moving MQTT payload builders into Core (rejected).
- Splitting the test project into per-library test projects (decision from stage 2 stands; revisit afterwards).
- Analyzer/`BannedSymbols.txt` rollout and CI workflow changes.
