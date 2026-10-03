## Context

See proposal.md for motivation and `extract-core-projects` / `extract-leaf-feature-projects` for the layout this stage completes (`Domain ← Messages ← Core ← feature libs ← host`; feature libs reference only Core and below, never each other).

Facts from the current code (read, not built):

- `IEnrichmentFeature` (`src/Njord/Enrichment/IEnrichmentFeature.cs`) has `TypeName`, `Enabled`, `DeviceId`, `BuildDiscoveryPayload(DiscoveryContext, location)` and `ToStateMessages(result, baseTopic, location)`; its sub-interfaces (`IStatelessEnrichment`, `IStatefulEnrichment`, `IActorEnrichment`) add the compute side. The five features (`Features/Alert|Derived|History|Index|TrendEnrichment.cs`) contain **both** compute and Home Assistant JSON construction (`JsonObject` components with `unique_id`, `state_topic`, `value_template`, availability templates, `expire_after`, `device_class`, `unit_of_measurement`).
- `ToStateMessages` delegates to `Mqtt/StatePayloadBuilder.FromAlerts|FromDerived|FromTrends|FromIndices|FromHistory`; discovery uses `Mqtt/DiscoveryPayloadBuilder.BuildDeviceEnvelope` and `Mqtt/TopicScheme`.
- Consumers of the MQTT-facing members are only `Mqtt/DiscoveryActor.cs` (`feature.DeviceId`, `feature.BuildDiscoveryPayload`) and `Mqtt/MqttEgressActor.cs` (`feature.ToStateMessages`, via `_featuresByType`). `EnrichmentActor` uses only `Enabled`, `Compute` and `CreateFlow`. `Grpc` never touches features: it renders enrichment results itself.
- `consensus` is not an `IEnrichmentFeature` (no `ConsensusEnrichment` exists; the base spec states consensus is a pipeline stage) but is hardwired in `Mqtt`: `MqttEgressActor.MapToMqttMessages` has an `EnrichmentUpdate { TypeName: "consensus", Result: ConsensusResult }` arm calling `StatePayloadBuilder.FromConsensus`, and `DiscoveryActor` publishes the consensus device per location through `TopicScheme.EnrichmentDeviceId(location, "consensus")` + `DiscoveryPayloadBuilder.BuildConsensus` with no `Enabled` check. `EnrichmentOptions.Consensus.Enabled` exists and gates only the emission of consensus events (`EnrichmentActor`, `consensusEgressEnabled`) and gRPC status reporting.
- Cross-feature actor lookups use actor classes: `Context.GetActorAsync<EgressActor|PipelineActor|SensorHubActor|MqttConnectionActor>()` (`ModelStateActor`, `EnrichmentActor`, `DiscoveryActor`, `MqttEgressActor`).
- Wire-format guard today: one Verify snapshot (`Njord.Tests/Egress/Snapshots/DiscoveryPayloadBuilderSpec.The_device_payload_matches_the_approved_snapshot.verified.txt`, the per-model device) plus assertion specs (`Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs`, `StatePayloadBuilderSpec.cs`, `Njord.Tests/Enrichment/Features/*EnrichmentSpec.cs`, `EnrichmentFeatureContractSpec.cs`). The five **enrichment** device payloads and state messages are **not** snapshot-covered.
- `NjordActorSystemSetup` registers everything centrally: backoff actors (`scheduler`, `budget-tracker`, snapshot actors), resolvable actors (`egress`, `model-state`, `pipeline`, `enrichment`, `sensor-hub`, `grpc-snapshot-consumer`, and `mqtt-*` when `Mqtt.Enabled`) after `.WithSqlPersistence(...)`.

## Goals / Non-Goals

**Goals:**
- `Njord.Enrichment` compiles without any MQTT/HA knowledge; `Njord.Mqtt` owns all MQTT presentation.
- Zero change in MQTT discovery/state bytes, actor names, persistence IDs, metrics.
- Every migration step leaves `dotnet build` and the full test suite green.

**Non-Goals:**
- New abstractions beyond what the cycle break needs; no behavior or config schema changes.

## Decisions

### 1. Break the cycle with presenters owned by Mqtt (option c), not neutral descriptors (a) or builders-in-Core (b)

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

### 2. Presenter inputs: re-derive from options and Domain, do not share feature instances

Discovery needs data the features derive from configuration: `DerivedEnrichment` horizons, `IndexEnrichment` resolved preferences (`PreferenceResolver.Resolve`, in `Njord.Domain.Analysis`), `HistoryEnrichment` parameters (`ParameterRegistry.Resolve`, `Njord.Domain.Weather`). All three inputs live in `Domain`/`Core`, so each presenter takes `IOptions<NjordOptions>` (and the same domain helpers) and derives its entity plan at construction. This keeps the entity set static and config-derived (AGENTS.md guardrail): nothing in a presenter depends on received results. The duplicated derivation is small and covered by Decision 4's parity and snapshot checks. Alternative (a shared "entity plan" object in Core) rejected: it would be a descriptor model by another name.

Enablement has one source: `EnrichmentOptions.IsEnabled(typeName)` in Core; features and presenters both read it, so they cannot diverge.

### 3. Remaining cross-library edges are resolved by stage 1/2 types

| Edge today | Resolved by | Where it is checked |
|---|---|---|
| Egress/Enrichment/Mqtt → `PipelineActor` (`GetActorAsync`) | marker key `IPipelineActor` in Core | Library extraction tasks (groups 5–8) |
| Enrichment/Mqtt/Grpc → `EgressActor` | marker key `IEgressActor` in Core | same |
| Enrichment → `SensorHubActor` | marker key `ISensorHubActor` in Core (stage 2 introduces it) | same |
| Mqtt actors → `MqttConnectionActor` | stays inside `Njord.Mqtt` | n/a |
| `RequestPipelineSource`, `PipelineSourceResponse`, `RequestEgressSink/Source`, `EgressSinkResponse`, `EgressSourceResponse`, `EgressEvent`, `Ack`, scheduler/budget query messages | `Njord.Messages` (stage 1) | compile |
| `StreamSupervision`, `StreamConsumerActor`, `NjordHealthState`, `NjordMetrics`, `TopicSlug`, `HorizonProjection`, `IOpenMeteoClient` | `Njord.Core` (stage 1); `TopicSlug`/`HorizonProjection` must be outside `Njord.Egress` (verify; move in task 2.1 if stage 1 left them) | compile |
| Pipeline/Enrichment/Egress → options (`NjordOptions`, `LocationOptions`, …) | `Njord.Core` | compile |
| Pipeline → Persistence DTOs + mapping (`SchedulerDtoMapping`, `ModelPollState`) | mapping lives in the owning lib (`Njord.Pipeline`), DTOs in `Njord.Persistence` (stage 1) | compile |
| Mqtt → `IEnrichmentFeature` | removed by Decision 1 (presenters) | ArchUnit + project refs |

If any row is not actually satisfied when this stage starts (stage 1/2 deviated), the corresponding task adds the missing move first; no lateral project reference is an acceptable shortcut.

### 4. Byte-identical output is proven by golden masters recorded before any move

Because only the per-model device payload has a Verify snapshot, task group 1 adds snapshot specs against the **current** code:
- `EnrichmentDiscoverySnapshotSpec` (Njord.Tests/Mqtt): `DeviceId` + `BuildDiscoveryPayload` for all five features, fixed config (one location, all features enabled, fixed horizons/day offsets, fixed `DiscoveryContext`), via `Verify`.
- `EnrichmentStateSnapshotSpec`: `ToStateMessages` for a fixed result per feature (topic, payload, retain).
- A feature/presenter parity spec (Decision 1, requirement "Presenter set matches the feature set plus consensus").
- `consensus` snapshots are recorded in group 4 against the still-hardwired builders, before the `ConsensusPresenter` exists (Decision 8).

These snapshots are committed first and must pass **unchanged** after every later task. The existing specs that must also stay green without edits to expectations: `DiscoveryPayloadBuilderSpec` (incl. its approved snapshot), `StatePayloadBuilderSpec`, `DiscoveryActorSpec`, `MqttEgressActorSpec`, `EnrichmentFeatureContractSpec` (adapted only where it calls the removed members), `Features/*EnrichmentSpec`.

### 5. Order inside the stage: cycle first (still one assembly), then leaf-most library first

1. Golden masters (Decision 4).
2. Introduce `EnrichmentTypeNames`/`IsEnabled` in Core; make features use it.
3. Introduce presenters in `Njord.Mqtt` (copy bodies), switch `DiscoveryActor`/`MqttEgressActor` to presenters, slim `IEnrichmentFeature`, register presenters in DI. Add the ArchUnit rule "Njord.Enrichment must not depend on Njord.Mqtt" **first** (red), then green. Cycle broken while still one assembly, so every step is cheap to revert.
4. Consensus presenter (Decision 8), still one assembly: snapshots first, then `ConsensusPresenter`, then remove the hardwired branches.
5. Extract `Njord.Egress` (6 files; depends only on Messages/Core), then `Njord.Pipeline` (12), `Njord.Mqtt` (14), `Njord.Enrichment` (13). Mqtt before Enrichment is irrelevant to references (they no longer reference each other) but keeps the largest MQTT test surface moving while Enrichment is still intact.
6. Host cleanup (setup shells, empty folders, `Njord.csproj`), docs/skills, spec deltas, Dockerfile plan.

Per library the same recipe: new csproj (`Sdk` plain, `FrameworkReference Microsoft.AspNetCore.App` only if needed), move files, `AddNjordX()`/`WithXActors()` extension, replace actor-class lookups by marker keys, `InternalsVisibleTo Njord.Tests`, slnx entry, ArchUnit rules, build + tests.

### 6. Akka.Hosting registration is split per library; persistence stays in the host

`WithXActors(AkkaConfigurationBuilder)` per library registers its actors (names unchanged). Backoff registration (`RegisterWithBackoff`, `MinBackoff`/`MaxBackoff`/`RandomFactor`) is used by `scheduler` and `budget-tracker` (Pipeline) and the snapshot actors (Grpc, stage 2): the helper moves to Core as an extension so both libraries share it. The host's `NjordActorSystemSetup` keeps provider/connection-string resolution and `.WithSqlPersistence(...)`, then calls the `WithXActors` extensions **after** it, preserving today's ordering (journal configured before persistent actors start). `Mqtt.Enabled` gating stays inside `WithMqttActors`, reading options.

### 7. ArchUnit: compiler stops cycles and upward references; lateral references need a rule

A project reference graph prevents cycles and upward dependencies at compile time but not `Pipeline → Egress`. ArchUnit therefore loads all `Njord.*` assemblies and asserts the dependency matrix from the new requirement "Feature libraries do not reference each other". Convention rules (sealed, `Spec`) iterate all assemblies. The old namespace-based zone rules (`Njord.Ingest` vs `Njord.Egress`/`Njord.Mqtt`/`Njord.Grpc`) are superseded by assembly-based rules; they stay until the last library is extracted so every intermediate state is covered.

## Risks / Trade-offs

- [Features and presenters drift (new feature gets only one half)] → parity spec fails the build; `njord-enrichment-feature` skill documents both halves.
- [Byte-level regression in moved discovery/state code] → golden masters recorded first; bodies move verbatim, no refactoring in the same step.
- [Spec drift in the base specs: `enrichment-feature-registry` documents `IActorEnrichment.Materialize` (code: `CreateFlow`), `DiscoveryContext.Location` (absent in code), `ToStateMessages(result, baseTopic)` (code: `(result, baseTopic, location)`), a `ConsensusEnrichment` scenario (no such class) and a "6 enrichment features" scenario title (5 are registered)] → Decision 9: affected requirements are restated completely and every named symbol is verified, so the drift in the touched requirements disappears with this change; drift in untouched requirements/specs is out of scope and stays for a separate audit.
- [`ConsensusPresenter.Enabled` constant `true` preserves a questionable existing behavior (consensus discovery is published even when `Consensus.Enabled` is `false`)] → intentional: this stage must not change behavior; recorded as an open question and pinned by a characterization spec.
- [Actor lookup by marker key misregistered → runtime-only failure] → per-library specs resolve each marker from a test `ActorRegistry`/Hosting TestKit; startup smoke in the validation section.
- [`WithXActors` ordering vs `WithSqlPersistence`] → keep the host as the only caller and assert order in a Hosting TestKit spec with persistence.
- [Large diff] → one library per commit, each green.
- [`architecture-zone-enforcement` delta is MODIFIED against a spec that only exists after `zone-architecture-tests` is archived] → archive that change first (stated prerequisite).
- Trade-off: adding an enrichment feature now touches two libraries. Accepted: it is the boundary the split is for.

### 8. Consensus is presented through the presenter registry too

`consensus` is not a feature (computed by the pipeline, not by an `IEnrichmentFeature`), but its MQTT presentation is the same kind of thing as the five feature presentations and today lives as two special cases in `Njord.Mqtt`. Decision: add `ConsensusPresenter : IEnrichmentPresenter` in `Njord.Mqtt` that wraps the existing builders unchanged (`TopicScheme.EnrichmentDeviceId`, `DiscoveryPayloadBuilder.BuildConsensus`, `StatePayloadBuilder.FromConsensus`), register it in the same registry (first, to keep today's publish order), and delete the `"consensus"` arm in `MqttEgressActor` and the hardwired block in `DiscoveryActor`. `Njord.Mqtt` then has no `TypeName`-specific dispatch. Computation stays in the pipeline; `consensus` stays out of `IEnumerable<IEnrichmentFeature>`; gRPC rendering is unchanged.

Consequences and constraints:
- Parity becomes "presenter set = feature set + `consensus`" (spec requirement updated).
- `ConsensusPresenter.Enabled` is constant `true`: the discovery loop skips presenters with `Enabled == false`, and consensus discovery is unconditional today. Deriving `Enabled` from `EnrichmentOptions.Consensus.Enabled` would stop publishing (and leave stale retained) consensus config when the toggle is off — a behavior change, out of scope. The characterization spec in task 4.3 pins the current behavior.
- Byte-identity is proven the same way as for the five features: snapshots over the current builders are approved **before** the presenter exists (task 4.2), and must pass unchanged afterwards. The publish order (per-model devices, consensus, then features in registration order) is pinned by task 4.3.
- The presenter needs `ResolvedParameterSet` and options; it receives them by constructor injection exactly as `DiscoveryActor` does today.
- Alternative rejected: leave consensus special-cased. It would keep a `TypeName` switch in `Njord.Mqtt` next to a registry for the other five and make the registry look complete when it is not.

### 9. Delta specs restate affected requirements completely

The base spec `enrichment-feature-registry` has drifted from the code (see Risks). To keep that from carrying over, every requirement this change touches is written as a complete `MODIFIED` requirement (full text, all scenarios matching the new contract: features compute only; presenters in `Njord.Mqtt` own `DeviceId`/discovery/state), never a partial edit. Concretely the `enrichment-feature-registry` delta restates the base contract, `IActorEnrichment` (now `CreateFlow`), `DiscoveryContext`, DI registration (5 features) and the device-envelope helper. After the stage, every symbol named in a delta (types, members, projects, namespaces) is verified by grep (task 10.4); drift found there is fixed in the delta. Requirements this change does not touch (e.g. `IStatelessEnrichment`, `IStatefulEnrichment`, `TopicScheme` methods) were compared with the code and match, so they are left alone.

## Migration Plan

Docs/refactor only, no runtime behavior change; rollback per commit via `git revert`. Dockerfile (plan, executed in task 10.3): add `COPY` + `restore` lines for each new csproj before the full copy so layer caching still works; CI is solution-based and needs no change.

## Open Questions

- Should the consensus device config be withdrawn (empty retained payload) when `EnrichmentOptions.Consensus.Enabled` is `false`, like disabled features, instead of being published regardless? Kept unchanged here (no behavior change); candidate for a separate change.
- Whether presenters should live in a subfolder `Njord.Mqtt/Presentation/` or beside the builders — cosmetic, decided during implementation (tasks assume `Presentation/`).
