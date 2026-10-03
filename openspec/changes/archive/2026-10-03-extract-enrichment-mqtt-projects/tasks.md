## 0. Preconditions

> Drift found while applying (2026-10-03, verified against the tree): (1) `src/Njord/Enrichment` has 14 files, not 13 (Mqtt: 14 as stated). (2) No Verify snapshot covered any MQTT payload before this change (design/tasks said one per-model snapshot existed). (3) `TopicSlug` and `HorizonProjection` were still in `Njord.Egress`, not Core, although `Njord.Mqtt` (`TopicScheme`, `StatePayloadBuilder`, `MqttEgressActor`) and `HistoryEnrichment` use them: moved to Core in the new task 2.0. (4) Only `DiscoveryActor` and `MqttEgressActor` call the MQTT members of `IEnrichmentFeature` (plus `NjordServiceSetup` for registration and test specs); `EnrichmentActor` uses compute members only. (5) `using Njord.Egress` / `using Njord.Pipeline` in several Mqtt/Enrichment files reference no public type other than the two above; they become unused once those move (part of 5.x/6.x cleanup). (6) Baseline is 825 tests (Domain 286, Persistence 10, Core 97, Egress 27, Grpc 74, Pipeline 111, Architecture 25, host 195); 837 after the 12 golden-master tests of group 1. Golden-master specs are plain `[Fact]` (synchronous, no awaited calls, xUnit1069 forbids a `Timeout` without the token).


> Drift found in part B (2026-10-03): (7) `IEnrichmentPresenter` is public (the public actors `DiscoveryActor`/`MqttEgressActor` take it in their constructors); the presenters are internal sealed in `Njord.Mqtt.Presentation`. (8) `HistoryPresenter` takes the model list from `IOptions<NjordOptions>.Models` and `DerivedPresenter` the horizons; `IndexPresenter` needs no preferences for discovery (they were only used by `Compute`), so it only reads enablement. (9) `ConsensusPresenter` reads `ctx.Mqtt`/`ctx.PollInterval`/`ctx.Version` from `DiscoveryContext` (identical to the values the actor used) and `ForecastDays` and `ResolvedParameterSet` from its constructor. (10) The `HistoryEnrichmentSpec` discovery test moved to `Mqtt/Presentation/HistoryPresenterSpec`. (11) The old 3.4 note about `HistoryEnrichmentSpec` using `ToStateMessages` was wrong: it used `BuildDiscoveryPayload`.

- [x] 0.1 Confirm stage 3a is applied and archived: `ls openspec/changes/archive | grep extract-pipeline-egress-projects` (present, 2026-10-02); `src/Njord.Pipeline` and `src/Njord.Egress` exist, `src/Njord/Pipeline` and `src/Njord/Egress` are gone, `src/Njord.Tests/Configuration/PersistenceBeforeActorsSpec.cs` and `ActorKeyRegistrationSpec.cs` exist and are green; stages 1 and 2 and `split-test-projects` are archived
- [x] 0.2 Confirm `openspec/specs/architecture-zone-enforcement/spec.md` exists (`ls openspec/specs`)
- [x] 0.3 Check the actual current host contents: `src/Njord/Mqtt` (DiscoveryActor, DiscoveryContext, DiscoveryPayloadBuilder, MqttConnectionActor, MqttEgressActor, MqttEgressTuning, MqttMessage, MqttSinkResponse, RequestMqttSink, StatePayloadBuilder, TopicScheme, `Transport/`) and `src/Njord/Enrichment` (EnrichmentActor, ForecastHistory*, IActorEnrichment, IEnrichmentFeature, IStatefulEnrichment, IStatelessEnrichment, `Features/`); note any drift (file counts in this change: 14 Mqtt, 13 Enrichment) at the top of this file
- [x] 0.4 Verify the "Remaining cross-library edges" table in design.md Decision 3 against the code (markers in `src/Njord.Core/Actors/ActorKeys.cs`: `IPipelineActor`/`IEgressActor`/`ISensorHubActor`/`IMqttConnectionActor`/`IMqttEgressActor`/`IDiscoveryActor`/`IEnrichmentActor`; `TopicSlug`, `HorizonProjection`, messages in Core/Messages); list every unmet row at the top of this file and move the missing type first
- [x] 0.5 Baseline: from `src/` run `dotnet build Njord.slnx` and all test projects (`dotnet test --solution Njord.slnx`, or the loop over `Njord.*Tests` in AGENTS.md); expected 825 tests (Domain 286, Persistence 10, Core 97, Egress 27, Grpc 74, Pipeline 111, Architecture 25, host `Njord.Tests` 195); record the actual count

## 1. Golden masters (written against current code, must pass unchanged afterwards)

- [x] 1.1 Add `src/Njord.Tests/Mqtt/EnrichmentDiscoverySnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify): for alerts, derived, trends, indices, history record `DeviceId` + `BuildDiscoveryPayload` with a fixed config (one location, all features enabled, fixed horizons/day offsets, fixed `DiscoveryContext` and version); approve the `.verified.txt` files. Shared inputs live in `src/Njord.Tests/Mqtt/EnrichmentGoldenMasterFixtures.cs` (fixed `FakeTimeProvider`, one location `lucerne`, two models, all features enabled)
- [x] 1.2 Add `src/Njord.Tests/Mqtt/EnrichmentStateSnapshotSpec.cs`: `ToStateMessages` for a fixed result per feature (topic, payload, retain); approve snapshots
- [x] 1.3 Add a feature/presenter parity spec skeleton in `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` (the existing spec already covers the feature half: unique type names, consensus not in the registry, enablement; the presenter half is added in 3.4)
- [x] 1.4 Run the full suite; commit `test: add golden master snapshots for enrichment payloads` (also contains the consensus snapshots of 4.2, recorded early against the current builders)

## 2. Shared pieces in Core (no behavior change)

- [x] 2.0 Move `TopicSlug` and `HorizonProjection` (+ their specs `TopicSlugSpec`, `HorizonProjectionSpec`) from `src/Njord.Egress` to `src/Njord.Core` (now under `src/Njord.Core/Egress/` and `src/Njord.Core.Tests/Egress/`; namespace `Njord.Egress` kept, so no call site changes); `git mv`, no behavior change
- [x] 2.1 Add `src/Njord.Core/Enrichment/EnrichmentTypeNames.cs` (namespace `Njord.Enrichment`) (`Alerts`, `Derived`, `Trends`, `Indices`, `History`, `Consensus`) and `EnrichmentOptions.IsEnabled(string typeName)` (test first in `src/Njord.Core.Tests/Configuration/EnrichmentOptionsValidationSpec.cs`: each toggle maps to its type name, unknown name throws)
- [x] 2.2 Switch the five features' `TypeName`/`Enabled` in `src/Njord/Enrichment/Features/*Enrichment.cs` to the constants and `IsEnabled`; run suite

## 3. Break the Enrichment<->Mqtt cycle (still one assembly)

- [x] 3.1 Add the failing architecture rule "no type in `Njord.Enrichment` depends on `Njord.Mqtt`" to `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` (red: it currently fails with the feature/MQTT edges)
- [x] 3.2 Add `src/Njord/Mqtt/IEnrichmentPresenter.cs` and five internal sealed presenters (`AlertPresenter`, `DerivedPresenter`, `TrendPresenter`, `IndexPresenter`, `HistoryPresenter`) under `src/Njord/Mqtt/Presentation/`; move the bodies of `DeviceId`, `BuildDiscoveryPayload`, `ToStateMessages` from the features **verbatim**; each presenter derives its inputs from `IOptions<NjordOptions>` (horizons, `PreferenceResolver.Resolve`, `ParameterRegistry.Resolve`) per design Decision 2a
- [x] 3.3 Register presenters in the Mqtt service registration (`src/Njord/Configuration/NjordServiceSetup.cs`); change `src/Njord/Mqtt/DiscoveryActor.cs` and `src/Njord/Mqtt/MqttEgressActor.cs` to take `IEnumerable<IEnrichmentPresenter>` (`_featuresByType` -> `_presentersByType`)
- [x] 3.4 Slim `src/Njord/Enrichment/IEnrichmentFeature.cs` to `TypeName` + `Enabled`; delete the moved members, `using Njord.Mqtt` and the now unused builder calls from the five features; add presenter parity and "no MQTT surface" assertions to `EnrichmentFeatureContractSpec`; adapt `HistoryEnrichmentSpec` (uses `ToStateMessages`) to the presenter
- [x] 3.5 Run golden masters (1.1, 1.2) with unchanged `.verified.txt`; run the full suite; rule 3.1 is now green; commit `refactor: move enrichment MQTT presentation into Njord.Mqtt presenters`

## 4. Consensus presenter (still one assembly)

- [x] 4.1 Re-verify the hardwiring: `grep -n '"consensus"' src/Njord/Mqtt/MqttEgressActor.cs src/Njord/Mqtt/DiscoveryActor.cs` shows the `EnrichmentUpdate { TypeName: "consensus", Result: ConsensusResult }` case and the unconditional consensus block per location; note any drift at the top of this file
- [x] 4.2 Add `src/Njord.Tests/Mqtt/ConsensusSnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify) against the **current** builders: `TopicScheme.EnrichmentDeviceId`/`ConfigTopic`, `DiscoveryPayloadBuilder.BuildConsensus` for a fixed config and `StatePayloadBuilder.FromConsensus` for a fixed `ConsensusResult` (topic, payload, retain); approve the `.verified.txt` files first
- [x] 4.3 Add characterization specs to `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` (pass on the current code): consensus discovery is published for every location even when `EnrichmentOptions.Consensus.Enabled` is `false`, and the publish order per location is per-model devices, consensus, then alerts, derived, trends, indices, history
- [x] 4.4 Add `src/Njord/Mqtt/Presentation/ConsensusPresenter.cs` (internal sealed `IEnrichmentPresenter`): `TypeName` = `EnrichmentTypeNames.Consensus`; `Enabled` constant `true` with a comment that consensus discovery is unconditional today; `DeviceId`/`BuildDiscoveryPayload` wrap `TopicScheme.EnrichmentDeviceId` and `DiscoveryPayloadBuilder.BuildConsensus`, taking `ResolvedParameterSet` and `IOptions<NjordOptions>` via the constructor as `DiscoveryActor` does today; `ToStateMessages` wraps `StatePayloadBuilder.FromConsensus` and returns an empty list for a non-`ConsensusResult`; register it **first** so the publish order stays as in 4.3
- [x] 4.5 Remove the consensus special cases: the `TypeName: "consensus"` arm in `MqttEgressActor.MapToMqttMessages` and the hardwired block in `DiscoveryActor`; unknown `TypeName` still yields no messages
- [x] 4.6 Extend the parity spec in `EnrichmentFeatureContractSpec`: presenter set = feature set + `consensus`, and `consensus` is still not in `IEnumerable<IEnrichmentFeature>`
- [x] 4.7 Run 4.2 and 4.3 (unchanged snapshots and expectations) and the full suite; commit `refactor: present consensus through the presenter registry`

## 5. Extract Njord.Mqtt

- [x] 5.1 Create `src/Njord.Mqtt/Njord.Mqtt.csproj` (Core only; MQTTnet via `dotnet add package`, never edit versions in csproj); slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and `src/Njord/Njord.csproj`
- [x] 5.2 Move `src/Njord/Mqtt/*` incl. `Transport/` and `Presentation/` (actors, builders, `TopicScheme`, `DiscoveryContext`, `MqttMessage`, presenters) into the project; actor classes `public`
- [x] 5.3 Add `AddNjordMqtt` (services, presenters); `RegisterMqttActors` (`IMqttConnectionActor`, `IMqttEgressActor`, `IDiscoveryActor`; names `mqtt-connection`, `mqtt-egress`, `mqtt-discovery`) stays in the host, gated on `Mqtt.Enabled`, after `.WithSqlPersistence(...)`
- [x] 5.4 Add to the ArchUnit assembly set (`src/Njord.Architecture.Tests/NjordArchitecture.cs`) and `LayerReferenceSpec`; adapt `src/Njord.Tests/Mqtt/*Spec.cs`; golden masters pass unchanged; build + suite; commit `refactor: extract Njord.Mqtt`

## 6. Extract Njord.Enrichment

- [x] 6.1 Create `src/Njord.Enrichment/Njord.Enrichment.csproj` (Core only, no reference to `Njord.Mqtt`); slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and the host
- [x] 6.2 Move `src/Njord/Enrichment/*` incl. `Features/`, `ForecastHistoryActor` and its state/messages/persistence mapping into the project; actor classes `public`
- [x] 6.3 Add `AddNjordEnrichment` (registers the five `IEnrichmentFeature` singletons); `RegisterEnrichmentActors` (`IEnrichmentActor`, name `enrichment`) stays in the host
- [x] 6.4 Add to the ArchUnit assembly set (`src/Njord.Architecture.Tests/NjordArchitecture.cs`) and `LayerReferenceSpec`; adapt `src/Njord.Tests/Enrichment/**/*Spec.cs`; build + suite; commit `refactor: extract Njord.Enrichment`

## 7. Host cleanup and architecture rules

> Applied 2026-10-03: the host setup classes were already shells; `ConsensusSnapshotFactory` (a Domain class, only consumed by `EnrichmentActor`) is now registered in `AddNjordEnrichment` instead of the host. The namespace rules (Ingest/Egress-side/Domain/Egress-Pipeline/Enrichment-Mqtt) and the `Ingest`/`Domain`/`EgressSide` providers were dropped: the assembly lateral/base rules cover them (test total 862 -> 859: -5 facts, +2 explicit rules). Red-proof: `Enrichment_does_not_depend_on_mqtt_or_grpc` and `Mqtt_does_not_depend_on_enrichment` were shown red with a temporary project reference plus probe type, then reverted. The compiler does not forbid these references in the tree as it stands, so no rule is unprovable.

- [x] 7.1 Reduce `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs` to shells calling the per-library extensions; remove emptied folders; `src/Njord/Njord.csproj` references all feature libraries
- [x] 7.2 In `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` add Enrichment and Mqtt to the lateral matrix from 3a plus the rules "Enrichment does not depend on Mqtt/Grpc" and "Mqtt does not depend on Enrichment" (red-prove each with a temporary reference, then remove); drop the superseded namespace zone rules (the last in-host feature has moved)
- [x] 7.3 Build + suite; commit `refactor: host composes all feature libraries`

## 8. Documentation and skills

- [x] 8.1 Update `AGENTS.md`: solution structure tree (`Njord.Mqtt`, `Njord.Enrichment`; no feature folders left in the host), guardrail text for the presenter boundary (Enrichment computes, Mqtt presents)
- [x] 8.2 Rewrite `.claude/skills/njord-enrichment-feature/SKILL.md` for the feature + presenter pair (parity spec, `EnrichmentTypeNames`, snapshot specs); fix cited paths in `.claude/skills/njord-persistent-actor/SKILL.md` and `njord-actor-spec/SKILL.md`; verify every cited path with `ls`
- [x] 8.3 Commit `docs: update structure and skills for enrichment/mqtt libraries`

## 9. Validation

> Results 2026-10-03: build 0 warnings/0 errors (also `--no-incremental`); `dotnet test --solution Njord.slnx` 859 passed / 0 failed in 3 consecutive runs (862 baseline minus 5 superseded namespace zone facts plus 2 explicit rules, see group 7); `dotnet format whitespace` clean; slopwatch 0 issues; `git diff df7d9c4 HEAD -- '*.verified.txt'` empty, no `*.received.*`. 9.3: the `"consensus"` string literals left in `Njord.Mqtt` are topic segments in `DiscoveryPayloadBuilder`/`StatePayloadBuilder` (no dispatch branch). 9.6: default Development run (MQTT disabled) 25 s, SIGTERM, 0 ERR, no leftover process. The run with an unreachable broker (127.0.0.1:1) starts and stops cleanly but logs one ERR (`MqttClientNotConnectedException` from `SelectAsync`, stream stops) -- identical on the pre-split commit 21abe7c, so not a regression. No MQTT-enabled check against a real broker: not run. `docker build` of the Dockerfile succeeded (image removed). 9.5: all symbols in the delta specs grepped; one drift fixed (`EgressEvent` lives in `Njord.Messages`, namespace `Njord.Messages.Egress`, not `Njord.Egress`).

- [x] 9.1 From `src/`: `dotnet build Njord.slnx` (0 errors)
- [x] 9.2 From `src/`: all test projects pass (`dotnet test --solution Njord.slnx` or the AGENTS.md loop), total count >= baseline from 0.5, golden-master snapshots unchanged (`git diff --stat -- '*.verified.txt'` shows only the new files from 1.1/1.2/4.2)
- [x] 9.3 `grep -rn "Njord.Mqtt" src/Njord.Enrichment` returns nothing; `grep -rn '"consensus"' src/Njord.Mqtt` finds only the presenter and type-name constants, no dispatch branch; `grep -rn "<ProjectReference" src/Njord.*/*.csproj` shows feature libraries referencing only `Njord.Core`
- [x] 9.4 Dockerfile: add `COPY` + `dotnet restore` lines for `Njord.Mqtt` and `Njord.Enrichment` csprojs before the full source copy; CI workflows unchanged
- [x] 9.5 Rewrite check: every `MODIFIED` requirement in this change's delta specs restates the full text and all scenarios, and every symbol named in them (types, members, projects, namespaces) exists in the code (`grep` each identifier); drift found is fixed in the delta
- [x] 9.6 Startup smoke: from `src/Njord/` run once with `Njord__Mqtt__Enabled=false` and once with an unreachable broker host; it starts, actors resolve, Ctrl+C stops cleanly
- [x] 9.7 `openspec validate extract-enrichment-mqtt-projects` passes; `dotnet slopwatch` from the repo root if the manifest exists, otherwise note it as skipped
- [x] 9.8 Commits are Conventional Commits, no attribution trailers, nothing pushed
