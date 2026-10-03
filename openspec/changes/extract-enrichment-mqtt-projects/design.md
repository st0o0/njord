## Context

See proposal.md. This is stage 3b; stage 3a (`extract-pipeline-egress-projects`) has already extracted `Njord.Pipeline` and `Njord.Egress` (layout: `Domain <- Messages <- Core <- feature libs <- host`; feature libs reference only Core and below, never each other).

Facts from the current code (read, not built):

- `IEnrichmentFeature` (`src/Njord/Enrichment/IEnrichmentFeature.cs`) has `TypeName`, `Enabled`, `DeviceId`, `BuildDiscoveryPayload(DiscoveryContext, location)` and `ToStateMessages(result, baseTopic, location)`; its sub-interfaces (`IStatelessEnrichment`, `IStatefulEnrichment`, `IActorEnrichment`) add the compute side. The five features (`Features/Alert|Derived|History|Index|TrendEnrichment.cs`) contain **both** compute and Home Assistant JSON construction.
- `ToStateMessages` delegates to `Mqtt/StatePayloadBuilder.From*`; discovery uses `Mqtt/DiscoveryPayloadBuilder.BuildDeviceEnvelope` and `Mqtt/TopicScheme`.
- Consumers of the MQTT-facing members are only `Mqtt/DiscoveryActor.cs` and `Mqtt/MqttEgressActor.cs`. `EnrichmentActor` uses only `Enabled`, `Compute` and `CreateFlow`. `Grpc` never touches features.
- `consensus` is not an `IEnrichmentFeature` but is hardwired in `Mqtt` (`MqttEgressActor.MapToMqttMessages` arm; unconditional block in `DiscoveryActor`). `EnrichmentOptions.Consensus.Enabled` gates only the emission of consensus events and gRPC status.
- Wire-format guard today: one Verify snapshot (per-model device) plus assertion specs. The five **enrichment** device payloads and state messages are **not** snapshot-covered.
- After 3a, `Mqtt` and `Enrichment` still sit in the host and already use markers (`IEgressActor`, `IPipelineActor`, `ISensorHubActor`, `IMqttConnectionActor`).

## Goals / Non-Goals

**Goals:**
- `Njord.Enrichment` compiles without any MQTT/HA knowledge; `Njord.Mqtt` owns all MQTT presentation.
- Zero change in MQTT discovery/state bytes, actor names, persistence IDs, metrics; every step leaves build and suite green.

**Non-Goals:**
- New abstractions beyond what the cycle break needs; behavior or config schema changes; anything about Pipeline/Egress.

## Decisions

### 1. This is the blocked half of the old stage 3

Pipeline and Egress do not depend on Mqtt, so they were split off (3a). This change waits for the MQTT review on 2026-10-04 because removing MQTT would make the presenter work and `Njord.Mqtt` moot (see "Alternative if MQTT is removed").

### 2. Break the cycle with presenters owned by Mqtt (option c), not neutral descriptors (a) or builders-in-Core (b)

Options evaluated against the code:

| | (a) Neutral descriptors | (b) Payload builders to Core | (c) Presenters in `Njord.Mqtt` |
|---|---|---|---|
| Enrichment knows MQTT/HA? | No, but must emit an HA-shaped model | Yes (`MqttMessage`, `TopicScheme`, builders) | No |
| New contract types | A component-descriptor model rich enough for all five features (platform, unit, device class, precision, value path, per-component availability template, attributes template, per-horizon/day expansion) plus a state-document model | None | `IEnrichmentPresenter` (mirror of the removed members) |
| Risk to byte-identity | High: features use hand-built `JsonObject`s with feature-specific quirks (e.g. `DerivedEnrichment` horizon × param grid, `IndexEnrichment` envelope fields, `HistoryEnrichment` device_class); a generic renderer must reproduce every key order and template string | Low | Low: code moves verbatim |
| Effect on Core | Grows by a descriptor model that is HA in disguise | MQTT leaks into the lowest shared project | None |
| Consistency with `Grpc` | Different | Different | Same pattern: each egress protocol maps `EnrichmentUpdate.Result` itself |
| Cost of a new feature | One lib | One lib | Two libs (compute in Enrichment, presenter in Mqtt) — the intended boundary |

**Chosen: (c).** `Njord.Mqtt` already receives `EgressEvent.EnrichmentUpdate(TypeName, Result)`; it needs no knowledge of features, only of result types (in `Njord.Domain`) and options (in `Njord.Core`). The AGENTS.md guardrail "Ingest → Domain → Egress meet only in the domain model" then holds literally: Enrichment (compute) and Mqtt (presentation) are linked only by domain results and `EgressEvent`. Rejected (a) because it replaces one coupling (MQTT in features) with a second HA-shaped model and endangers byte-identity; rejected (b) because it cements the leak in Core.

New/changed contracts:

```csharp
// Njord.Enrichment  (compute only)
public interface IEnrichmentFeature { string TypeName { get; } bool Enabled { get; } }
// IStatelessEnrichment / IStatefulEnrichment / IActorEnrichment: unchanged compute members

// Njord.Mqtt
public interface IEnrichmentPresenter
{
    string TypeName { get; }
    bool Enabled { get; }
    string DeviceId(string location);
    string BuildDiscoveryPayload(DiscoveryContext ctx, string location);
    IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location);
}
// AlertPresenter, DerivedPresenter, TrendPresenter, IndexPresenter, HistoryPresenter (internal sealed)

// Njord.Core
public static class EnrichmentTypeNames { const string Alerts = "alerts"; ... }   // + Consensus
public static bool EnrichmentOptions.IsEnabled(string typeName)                  // single enablement lookup
```

`IEnrichmentPresenter` has the same shape the feature had, so bodies move verbatim (`BuildDiscoveryPayload`, `ToStateMessages`, `DeviceId`). `DiscoveryContext`, `MqttMessage`, `TopicScheme`, `DiscoveryPayloadBuilder`, `StatePayloadBuilder` stay in `Njord.Mqtt` — nothing MQTT moves down.

### 2a. Presenter inputs: re-derive from options and Domain, do not share feature instances

Discovery needs data the features derive from configuration: `DerivedEnrichment` horizons, `IndexEnrichment` resolved preferences (`PreferenceResolver.Resolve`, in `Njord.Domain.Analysis`), `HistoryEnrichment` parameters (`ParameterRegistry.Resolve`, `Njord.Domain.Weather`). All three inputs live in `Domain`/`Core`, so each presenter takes `IOptions<NjordOptions>` (and the same domain helpers) and derives its entity plan at construction. This keeps the entity set static and config-derived (AGENTS.md guardrail): nothing in a presenter depends on received results. The duplicated derivation is small and covered by Decision 4's parity and snapshot checks. Alternative (a shared "entity plan" object in Core) rejected: it would be a descriptor model by another name.

Enablement has one source: `EnrichmentOptions.IsEnabled(typeName)` in Core; features and presenters both read it, so they cannot diverge.

### 3. Remaining cross-library edges are resolved by stage 1/2/3a types

| Edge today | Resolved by | Where it is checked |
|---|---|---|
| Enrichment/Mqtt -> `PipelineActor`, `EgressActor`, `SensorHubActor`, `MqttConnectionActor` | markers `IPipelineActor`, `IEgressActor`, `ISensorHubActor`, `IMqttConnectionActor` (stage 1); the Mqtt class stays inside `Njord.Mqtt` | compile |
| `EgressEvent`, `Ack`, egress/pipeline request/response messages | `Njord.Messages` (stage 1) | compile |
| `StreamSupervision`, `StreamConsumerActor`, `NjordHealthState`, `NjordMetrics`, `TopicSlug`, `HorizonProjection`, options | `Njord.Core` (stage 1) | compile |
| Enrichment -> `ForecastHistoryActor` state/persistence mapping | mapping in `Njord.Enrichment`, DTOs in `Njord.Persistence` (stage 1) | compile |
| Mqtt -> `IEnrichmentFeature` | removed by Decision 2 (presenters) | ArchUnit + project refs |

If any row is not satisfied when this stage starts, the corresponding task adds the missing move first; no lateral project reference is an acceptable shortcut.

### 4. Byte-identical output is proven by golden masters recorded before any move

Because only the per-model device payload has a Verify snapshot, task group 1 adds snapshot specs against the **current** code:
- `EnrichmentDiscoverySnapshotSpec` (Njord.Tests/Mqtt): `DeviceId` + `BuildDiscoveryPayload` for all five features, fixed config (one location, all features enabled, fixed horizons/day offsets, fixed `DiscoveryContext`), via `Verify`.
- `EnrichmentStateSnapshotSpec`: `ToStateMessages` for a fixed result per feature (topic, payload, retain).
- A feature/presenter parity spec (Decision 1, requirement "Presenter set matches the feature set plus consensus").
- `consensus` snapshots are recorded in group 4 against the still-hardwired builders, before the `ConsensusPresenter` exists (Decision 8).

These snapshots are committed first and must pass **unchanged** after every later task. The existing specs that must also stay green without edits to expectations: `DiscoveryPayloadBuilderSpec` (incl. its approved snapshot), `StatePayloadBuilderSpec`, `DiscoveryActorSpec`, `MqttEgressActorSpec`, `EnrichmentFeatureContractSpec` (adapted only where it calls the removed members), `Features/*EnrichmentSpec`.

### 5. Order inside the stage: cycle first (still one assembly), then extract

1. Golden masters (Decision 4).
2. `EnrichmentTypeNames`/`IsEnabled` in Core; features use it.
3. Presenters in `Njord.Mqtt` (copy bodies), switch `DiscoveryActor`/`MqttEgressActor`, slim `IEnrichmentFeature`, register presenters. The ArchUnit rule "Njord.Enrichment must not depend on Njord.Mqtt" is added **first** (red), then green. Cycle broken while still one assembly, so every step is cheap to revert.
4. Consensus presenter (Decision 8): snapshots first, then `ConsensusPresenter`, then remove the hardwired branches.
5. Extract `Njord.Mqtt` (14 files), then `Njord.Enrichment` (13). They no longer reference each other, so the order only keeps the largest MQTT test surface moving while Enrichment is intact.
6. Host cleanup, ArchUnit rules, docs/skills, Dockerfile plan.

Per library recipe: new csproj (plain SDK; `FrameworkReference Microsoft.AspNetCore.App` only if needed), move files, `AddNjordX()` service extension, actor classes `public` (registration stays central in the host), `InternalsVisibleTo Njord.Tests`, project reference from `Njord.Tests` and host, slnx entry, ArchUnit set, build + tests.

### 6. Actor registration stays central in the host; persistence stays first

As in stage 1 Decision 6 and 3a: `RegisterMqttActors` (gated on `Mqtt.Enabled`) and `RegisterEnrichmentActors` stay in the host `NjordActorSystemSetup` after `.WithSqlPersistence(...)`. The 3a order-guard spec (`PersistenceBeforeActorsSpec`) and `ActorKeyRegistrationSpec` must stay green; `ForecastHistoryActor` is persistent, so the order matters here too.

### 7. ArchUnit

Compiler stops cycles and upward references, not lateral ones. 3a added the assembly matrix; this stage adds Enrichment and Mqtt to it plus the specific rules "Enrichment does not depend on Mqtt/Grpc" and "Mqtt does not depend on Enrichment". The old namespace-based zone rules (Ingest vs Egress/Mqtt/Grpc) are dropped here, once the last in-host feature has moved.

### 8. Consensus is presented through the presenter registry too

`consensus` is not a feature (computed by the pipeline, not by an `IEnrichmentFeature`), but its MQTT presentation is the same kind of thing as the five feature presentations and today lives as two special cases in `Njord.Mqtt`. Decision: add `ConsensusPresenter : IEnrichmentPresenter` in `Njord.Mqtt` that wraps the existing builders unchanged (`TopicScheme.EnrichmentDeviceId`, `DiscoveryPayloadBuilder.BuildConsensus`, `StatePayloadBuilder.FromConsensus`), register it in the same registry (first, to keep today's publish order), and delete the `"consensus"` arm in `MqttEgressActor` and the hardwired block in `DiscoveryActor`. `Njord.Mqtt` then has no `TypeName`-specific dispatch. Computation stays in the pipeline; `consensus` stays out of `IEnumerable<IEnrichmentFeature>`; gRPC rendering is unchanged.

Consequences and constraints:
- Parity becomes "presenter set = feature set + `consensus`" (spec requirement updated).
- `ConsensusPresenter.Enabled` is constant `true`: the discovery loop skips presenters with `Enabled == false`, and consensus discovery is unconditional today. Deriving `Enabled` from `EnrichmentOptions.Consensus.Enabled` would stop publishing (and leave stale retained) consensus config when the toggle is off — a behavior change, out of scope. The characterization spec in task 4.3 pins the current behavior.
- Byte-identity is proven the same way as for the five features: snapshots over the current builders are approved **before** the presenter exists (task 4.2), and must pass unchanged afterwards. The publish order (per-model devices, consensus, then features in registration order) is pinned by task 4.3.
- The presenter needs `ResolvedParameterSet` and options; it receives them by constructor injection exactly as `DiscoveryActor` does today.
- Alternative rejected: leave consensus special-cased. It would keep a `TypeName` switch in `Njord.Mqtt` next to a registry for the other five and make the registry look complete when it is not.

### 9. Delta specs restate affected requirements completely

The base spec `enrichment-feature-registry` has drifted from the code (see Risks). To keep that from carrying over, every requirement this change touches is written as a complete `MODIFIED` requirement (full text, all scenarios matching the new contract: features compute only; presenters in `Njord.Mqtt` own `DeviceId`/discovery/state), never a partial edit. Concretely the `enrichment-feature-registry` delta restates the base contract, `IActorEnrichment` (now `CreateFlow`), `DiscoveryContext`, DI registration (5 features) and the device-envelope helper. After the stage, every symbol named in a delta (types, members, projects, namespaces) is verified by grep (task 9.5); drift found there is fixed in the delta. Requirements this change does not touch (e.g. `IStatelessEnrichment`, `IStatefulEnrichment`, `TopicScheme` methods) were compared with the code and match, so they are left alone.

## Risks / Trade-offs

- [Features and presenters drift (new feature gets only one half)] -> parity spec fails the build; `njord-enrichment-feature` skill documents both halves.
- [Byte-level regression in moved discovery/state code] -> golden masters recorded first; bodies move verbatim, no refactoring in the same step.
- [Spec drift in the base specs: `enrichment-feature-registry` documents `IActorEnrichment.Materialize` (code: `CreateFlow`), `DiscoveryContext.Location` (absent in code), `ToStateMessages(result, baseTopic)` (code: `(result, baseTopic, location)`), a `ConsensusEnrichment` scenario (no such class) and a "6 enrichment features" scenario title (5 are registered)] -> Decision 9: affected requirements restated completely and symbols verified.
- [`ConsensusPresenter.Enabled` constant `true` preserves a questionable behavior (consensus discovery published even when `Consensus.Enabled` is `false`)] -> intentional, no behavior change; pinned by a characterization spec; open question below.
- [Actor lookup by marker misregistered -> runtime-only failure] -> `ActorKeyRegistrationSpec`; startup smoke.
- [Applied too early: MQTT removed after the review] -> status gate in proposal.md; see the alternative below.
- [Large diff] -> one library per commit, each green.
- Trade-off: adding an enrichment feature now touches two libraries. Accepted: it is the boundary the split is for.

## Migration Plan

Refactor only, no runtime behavior change; rollback per commit via `git revert`. Dockerfile (task 9.4): add `COPY` + `restore` lines for `Njord.Mqtt` and `Njord.Enrichment` csprojs before the full source copy; CI is solution-based and needs no change.

## Alternative if MQTT is removed

Decision for the user after the review on 2026-10-04. If MQTT goes, do not apply the plan above; rewrite this change (`/opsx:update`) as follows:

- No `IEnrichmentPresenter`, no presenters, no `ConsensusPresenter`, no golden masters, no `Njord.Mqtt`. Delete `src/Njord/Mqtt` (incl. `Transport/`), the MQTTnet package reference (`dotnet remove package`), `RegisterMqttActors`, `Mqtt` options/validators and `Mqtt.Enabled` handling, `IMqttConnectionActor`/`IMqttEgressActor`/`IDiscoveryActor` markers, Mqtt metrics and health checks, `Njord.Tests/Mqtt`.
- Cut the MQTT members (`DeviceId`, `BuildDiscoveryPayload`, `ToStateMessages`, `DiscoveryContext`, envelope helper) from `IEnrichmentFeature` and the five features (`Features/*Enrichment.cs`); `EnrichmentUpdate` events keep flowing to gRPC only.
- Extract only `Njord.Enrichment` (13 files); no cycle remains, so there is no inversion step and no Mqtt/Enrichment ArchUnit special rule beyond the 3a matrix.
- Adapt AGENTS.md: the decisions "HA device cut: ... one device per enabled enrichment feature", "Entity grid per model device" and "MQTT egress: ..." are removed or rewritten, and the project description (publishes Home Assistant entities via MQTT Discovery) changes with it; the `njord-enrichment-feature` skill loses the discovery/state-message parts.
- Specs: REMOVED/rewritten instead of MODIFIED for `mqtt-egress`, `mqtt-actor-topology`, `optional-mqtt-egress`, `discovery-toggle`, `delta-publishing`, `publisher-protocol`, `egress-event` (MqttEgressActor scenarios), `enrichment-feature-registry` (drop DiscoveryContext/envelope requirements; `IEnrichmentFeature` = `TypeName` + `Enabled`), `enrichment-model-envelope` and `activity-indices` (discovery requirements dropped); each needs a check of which main specs exist (`ls openspec/specs`). This is a larger, separate change (likely `remove-mqtt-egress`) that should land first, followed by a much smaller "extract Njord.Enrichment" change.
- Product impact to confirm with the user first: without MQTT there is no Home Assistant entity output at all (gRPC only).

## Open Questions

- Should the consensus device config be withdrawn (empty retained payload) when `EnrichmentOptions.Consensus.Enabled` is `false`, like disabled features, instead of being published regardless? Kept unchanged here (no behavior change); candidate for a separate change.
- Whether presenters live in `Njord.Mqtt/Presentation/` or beside the builders: cosmetic, tasks assume `Presentation/`.
- MQTT stays or goes: decided on 2026-10-04 (see "Alternative if MQTT is removed").
