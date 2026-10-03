## 0. Preconditions

- [ ] 0.1 Confirm `extract-core-projects` and `extract-leaf-feature-projects` are applied: `src/Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Domain`, `Njord.Grpc`, `Njord.Ingest`, `Njord.Sensors` exist and the host references them
- [ ] 0.2 Confirm `zone-architecture-tests` is applied and archived (`openspec/specs/architecture-zone-enforcement/spec.md` exists), otherwise stop
- [ ] 0.3 Verify the "Remaining cross-library edges" table in design.md Decision 3 against the code (Core/Messages own `StreamSupervision`, `StreamConsumerActor`, `NjordHealthState`, `NjordMetrics`, `TopicSlug`, `HorizonProjection`, request/response messages, marker keys `IPipelineActor`/`IEgressActor`/`ISensorHubActor`); list every unmet row at the top of this file and move the missing type first
- [ ] 0.4 Baseline: from `src/` run `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; record test count

## 1. Golden masters (written against current code, must pass unchanged afterwards)

- [ ] 1.1 Add `src/Njord.Tests/Mqtt/EnrichmentDiscoverySnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify): for alerts, derived, trends, indices, history record `DeviceId` + `BuildDiscoveryPayload` with a fixed config (one location, all features enabled, fixed horizons/day offsets, fixed `DiscoveryContext` and version); approve the `.verified.txt` files
- [ ] 1.2 Add `src/Njord.Tests/Mqtt/EnrichmentStateSnapshotSpec.cs`: `ToStateMessages` for a fixed result per feature (topic, payload, retain); approve snapshots
- [ ] 1.3 Add a feature/presenter parity spec skeleton in `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` (adapt the existing spec; the presenter half is added in 3.4)
- [ ] 1.4 Run the full suite; commit as `test: add golden masters for enrichment discovery and state payloads`

## 2. Shared pieces in Core (no behavior change)

- [ ] 2.1 If stage 1 left `TopicSlug` or `HorizonProjection` in the Egress namespace/folder, move them to `src/Njord.Core` (keep type names); run suite
- [ ] 2.2 Add `src/Njord.Core/Enrichment/EnrichmentTypeNames.cs` (`Alerts`, `Derived`, `Trends`, `Indices`, `History`, `Consensus`) and `EnrichmentOptions.IsEnabled(string typeName)` (test first in `src/Njord.Tests/Configuration/EnrichmentOptionsSpec.cs`: each toggle maps to its type name, unknown name throws)
- [ ] 2.3 Switch the five features' `TypeName`/`Enabled` in `src/Njord/Enrichment/Features/*Enrichment.cs` to the constants and `IsEnabled`; run suite

## 3. Break the Enrichment↔Mqtt cycle (still one assembly)

- [ ] 3.1 Add the failing architecture rule "no type in `Njord.Enrichment` depends on `Njord.Mqtt`" to `src/Njord.Tests/Architecture/ZoneArchitectureSpec.cs` (red: it currently fails with the feature/MQTT edges)
- [ ] 3.2 Add `src/Njord/Mqtt/IEnrichmentPresenter.cs` and five internal sealed presenters (`AlertPresenter`, `DerivedPresenter`, `TrendPresenter`, `IndexPresenter`, `HistoryPresenter`) under `src/Njord/Mqtt/Presentation/`; move the bodies of `DeviceId`, `BuildDiscoveryPayload`, `ToStateMessages` from the features **verbatim**; each presenter derives its inputs from `IOptions<NjordOptions>` (horizons, `PreferenceResolver.Resolve`, `ParameterRegistry.Resolve`) per design Decision 2
- [ ] 3.3 Register presenters in the Mqtt service registration (`src/Njord/Configuration/NjordServiceSetup.cs`); change `src/Njord/Mqtt/DiscoveryActor.cs` and `src/Njord/Mqtt/MqttEgressActor.cs` to take `IEnumerable<IEnrichmentPresenter>` (`_featuresByType` → `_presentersByType`)
- [ ] 3.4 Slim `src/Njord/Enrichment/IEnrichmentFeature.cs` to `TypeName` + `Enabled`; delete the moved members, `using Njord.Mqtt` and the now unused builder calls from the five features; add presenter parity and "no MQTT surface" assertions to `EnrichmentFeatureContractSpec`; adapt `HistoryEnrichmentSpec` (uses `ToStateMessages`) to the presenter
- [ ] 3.5 Run golden masters (1.1, 1.2) — they must pass with unchanged `.verified.txt`; run the full suite; rule 3.1 is now green; commit `refactor: move enrichment MQTT presentation into Njord.Mqtt presenters`

## 4. Consensus presenter (still one assembly)

- [ ] 4.1 Re-verify the hardwiring against the code at this point: `grep -n '"consensus"' src/Njord/Mqtt/MqttEgressActor.cs src/Njord/Mqtt/DiscoveryActor.cs` shows the `EnrichmentUpdate { TypeName: "consensus", Result: ConsensusResult }` case and the unconditional consensus block per location; note any drift at the top of this file
- [ ] 4.2 Add `src/Njord.Tests/Mqtt/ConsensusSnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify) against the **current** builders: `TopicScheme.EnrichmentDeviceId`/`ConfigTopic`, `DiscoveryPayloadBuilder.BuildConsensus` for a fixed config (one location, fixed `ResolvedParameterSet`, fixed horizons/forecast days, fixed `MqttOptions`, poll interval and version) and `StatePayloadBuilder.FromConsensus` for a fixed `ConsensusResult` (topic, payload, retain); approve the `.verified.txt` files first
- [ ] 4.3 Add characterization specs to `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` (pass on the current code): consensus discovery is published for every location even when `EnrichmentOptions.Consensus.Enabled` is `false`, and the publish order per location is per-model devices, consensus, then alerts, derived, trends, indices, history
- [ ] 4.4 Add `src/Njord/Mqtt/Presentation/ConsensusPresenter.cs` (internal sealed `IEnrichmentPresenter`): `TypeName` = `EnrichmentTypeNames.Consensus`; `Enabled` constant `true` with a comment that consensus discovery is unconditional today (changing it is a behavior change, see design Open Questions); `DeviceId`/`BuildDiscoveryPayload` wrap `TopicScheme.EnrichmentDeviceId` and `DiscoveryPayloadBuilder.BuildConsensus` and take `ResolvedParameterSet` and `IOptions<NjordOptions>` through the constructor exactly as `DiscoveryActor` receives them today; `ToStateMessages` wraps `StatePayloadBuilder.FromConsensus` and returns an empty list for a result that is not a `ConsensusResult`; register it **first** in the presenter registration so the publish order stays as in 4.3
- [ ] 4.5 Remove the consensus special cases: the `TypeName: "consensus"` switch arm in `MqttEgressActor.MapToMqttMessages` and the hardwired consensus block in `DiscoveryActor` (the generic presenter path now handles it); unknown `TypeName` still yields no messages
- [ ] 4.6 Extend the presenter parity spec in `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs`: presenter set = feature set + `consensus`, and `consensus` is still not in `IEnumerable<IEnrichmentFeature>`
- [ ] 4.7 Run 4.2 and 4.3 (unchanged `.verified.txt`, unchanged expectations) and the full suite; commit `refactor: present consensus through the presenter registry`

## 5. Extract Njord.Egress

- [ ] 5.1 Create `src/Njord.Egress/Njord.Egress.csproj` referencing only `Njord.Core` (+ transitive Messages/Domain); add to `src/Njord.slnx`; `InternalsVisibleTo Njord.Tests`
- [ ] 5.2 Move `src/Njord/Egress/{EgressActor,ModelStateActor}.cs` (and any remaining local types) into the project; replace `GetActorAsync<PipelineActor>()`/`<EgressActor>()` lookups with marker keys
- [ ] 5.3 Add `src/Njord.Egress/EgressServiceCollectionExtensions.cs` (`AddNjordEgress`) and `EgressActorSetup.cs` (`WithEgressActors`: names `egress`, `model-state`); call from the host setup files
- [ ] 5.4 Add `Njord.Egress` to the ArchUnit assembly set; adapt `src/Njord.Tests/Egress/*Spec.cs` references; build + suite; commit `refactor: extract Njord.Egress`

## 6. Extract Njord.Pipeline

- [ ] 6.1 Create `src/Njord.Pipeline/Njord.Pipeline.csproj` (Core only); slnx; `InternalsVisibleTo Njord.Tests`
- [ ] 6.2 Move `src/Njord/Pipeline/*` (`SchedulerActor`, `PipelineActor`, `BudgetTrackerActor`, `BudgetThrottleStage`, `IBudgetGate`, `IBudgetProvider`, states, `WeightedTarget`, `ModelPollState`, Pipeline persistence mapping) into the project; `StreamSupervision`, `Ack`, health and metrics come from Core/Messages
- [ ] 6.3 Move the backoff registration helper (`RegisterWithBackoff`, backoff constants) from `src/Njord/Configuration/NjordActorSystemSetup.cs` into a Core extension; add `AddNjordPipeline` and `WithPipelineActors` (`scheduler`, `budget-tracker` with backoff, `pipeline`); host calls them **after** `.WithSqlPersistence(...)` (add a Hosting TestKit spec asserting the persistent actors start with persistence configured)
- [ ] 6.4 Add to ArchUnit set; adapt `src/Njord.Tests/Pipeline/*Spec.cs`; build + suite; commit `refactor: extract Njord.Pipeline`

## 7. Extract Njord.Mqtt

- [ ] 7.1 Create `src/Njord.Mqtt/Njord.Mqtt.csproj` (Core only; MQTTnet package via `dotnet add package`, never edit versions in csproj); slnx; `InternalsVisibleTo Njord.Tests`
- [ ] 7.2 Move `src/Njord/Mqtt/*` incl. `Transport/` and `Presentation/` (actors, builders, `TopicScheme`, `DiscoveryContext`, `MqttMessage`, presenters) into the project; replace `GetActorAsync<EgressActor>()` with the marker key
- [ ] 7.3 Add `AddNjordMqtt` and `WithMqttActors` (`mqtt-connection`, `mqtt-egress`, `mqtt-discovery`, gated on `Mqtt.Enabled` inside the extension); host calls them
- [ ] 7.4 Add to ArchUnit set; adapt `src/Njord.Tests/Mqtt/*Spec.cs`; golden masters pass unchanged; build + suite; commit `refactor: extract Njord.Mqtt`

## 8. Extract Njord.Enrichment

- [ ] 8.1 Create `src/Njord.Enrichment/Njord.Enrichment.csproj` (Core only, no reference to `Njord.Mqtt`); slnx; `InternalsVisibleTo Njord.Tests`
- [ ] 8.2 Move `src/Njord/Enrichment/*` incl. `Features/`, `ForecastHistoryActor` and its state/messages/persistence mapping into the project; replace `GetActorAsync<PipelineActor|EgressActor|SensorHubActor>()` with marker keys
- [ ] 8.3 Add `AddNjordEnrichment` (registers the five `IEnrichmentFeature` singletons) and `WithEnrichmentActors` (`enrichment`); host calls them
- [ ] 8.4 Add to ArchUnit set; adapt `src/Njord.Tests/Enrichment/**/*Spec.cs`; build + suite; commit `refactor: extract Njord.Enrichment`

## 9. Host cleanup and architecture rules

- [ ] 9.1 Reduce `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs` to shells that call the per-library extensions; remove emptied folders; `src/Njord/Njord.csproj` references all feature libraries
- [ ] 9.2 Rewrite `src/Njord.Tests/Architecture/NjordArchitecture.cs`/`ZoneArchitectureSpec.cs`/`ConventionArchitectureSpec.cs` per the `architecture-zone-enforcement` delta: load all `Njord.*` production assemblies; lateral-reference matrix (red-prove each with a temporary reference/type, then remove); sealed rule over all assemblies excluding compiler-generated `Program`; keep the old namespace zone rules only until here, then drop the superseded ones
- [ ] 9.3 Build + suite; commit `refactor: host composes feature libraries`

## 10. Documentation and skills

- [ ] 10.1 Update `AGENTS.md`: solution structure tree (all new projects), reference direction, guardrail "Ingest and Egress never reference each other" stated as enforced by project references + ArchUnit lateral rules
- [ ] 10.2 Rewrite `.claude/skills/njord-enrichment-feature/SKILL.md` for the feature + presenter pair (parity spec, `EnrichmentTypeNames`, snapshot specs); fix cited paths in `.claude/skills/njord-persistent-actor/SKILL.md` and `njord-actor-spec/SKILL.md`; verify every cited path with `ls`
- [ ] 10.3 Plan-only note recorded in the commit message: Dockerfile adds `COPY` + `dotnet restore` lines for `Njord.Egress`, `Njord.Pipeline`, `Njord.Mqtt`, `Njord.Enrichment` csprojs before the full source copy; apply it here if the Docker build is run locally (`docker build .` from the repo root), CI workflows unchanged
- [ ] 10.4 Rewrite affected requirements completely: every `MODIFIED` requirement in this change's delta specs restates the full requirement text and all its scenarios (no partial edits), and every symbol named in them (types, members, projects, namespaces) is verified to exist in the code after the stage (`grep` each identifier; drift found is fixed in the delta, not carried over)
- [ ] 10.5 Commit `docs: update structure and skills for pipeline/egress/mqtt/enrichment libraries`

## 11. Validation

- [ ] 11.1 From `src/`: `dotnet build Njord.slnx` (0 errors)
- [ ] 11.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` — all pass, count ≥ baseline from 0.4, golden-master snapshots unchanged (`git diff --stat -- '*.verified.txt'` shows only the new files from 1.1/1.2)
- [ ] 11.3 `grep -rn "Njord.Mqtt" src/Njord.Enrichment` returns nothing; `grep -rn '"consensus"' src/Njord.Mqtt` finds only the presenter and type-name constants, no dispatch branch; `grep -rn "<ProjectReference" src/Njord.*/*.csproj` shows feature libraries referencing only `Njord.Core` (and Core/Messages/Persistence/Domain chain), never each other
- [ ] 11.4 Startup smoke: from `src/Njord/` run the service once with `Njord__Mqtt__Enabled=false` and once with an unreachable broker host; it starts, actors resolve (no registry/marker errors in logs), Ctrl+C stops it cleanly
- [ ] 11.5 `openspec validate extract-pipeline-egress-projects` passes; `dotnet slopwatch` if the manifest exists (see `openspec-hygiene`/`AGENTS.md`), otherwise note it as skipped
- [ ] 11.6 Commits are Conventional Commits, no attribution trailers, nothing pushed
