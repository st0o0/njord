## 0. Preconditions (blocked until the MQTT review on 2026-10-04)

- [ ] 0.1 Confirm the user's MQTT review outcome (2026-10-04): MQTT stays -> continue; MQTT removed -> stop and rewrite this change per design.md "Alternative if MQTT is removed"
- [ ] 0.2 Confirm `extract-pipeline-egress-projects` (3a) is applied: `src/Njord.Pipeline` and `src/Njord.Egress` exist, `src/Njord/Pipeline` and `src/Njord/Egress` are gone, `PersistenceBeforeActorsSpec` exists and is green; stages 1 and 2 are applied
- [ ] 0.3 Confirm `openspec/specs/architecture-zone-enforcement/spec.md` exists (`ls openspec/specs`)
- [ ] 0.4 Verify the "Remaining cross-library edges" table in design.md Decision 3 against the code (markers `IPipelineActor`/`IEgressActor`/`ISensorHubActor`/`IMqttConnectionActor`, `TopicSlug`, `HorizonProjection`, messages in Core/Messages); list every unmet row at the top of this file and move the missing type first
- [ ] 0.5 Baseline: from `src/` run `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; record test count

## 1. Golden masters (written against current code, must pass unchanged afterwards)

- [ ] 1.1 Add `src/Njord.Tests/Mqtt/EnrichmentDiscoverySnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify): for alerts, derived, trends, indices, history record `DeviceId` + `BuildDiscoveryPayload` with a fixed config (one location, all features enabled, fixed horizons/day offsets, fixed `DiscoveryContext` and version); approve the `.verified.txt` files
- [ ] 1.2 Add `src/Njord.Tests/Mqtt/EnrichmentStateSnapshotSpec.cs`: `ToStateMessages` for a fixed result per feature (topic, payload, retain); approve snapshots
- [ ] 1.3 Add a feature/presenter parity spec skeleton in `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` (adapt the existing spec; the presenter half is added in 3.4)
- [ ] 1.4 Run the full suite; commit `test: add golden masters for enrichment discovery and state payloads`

## 2. Shared pieces in Core (no behavior change)

- [ ] 2.1 Add `src/Njord.Core/Enrichment/EnrichmentTypeNames.cs` (`Alerts`, `Derived`, `Trends`, `Indices`, `History`, `Consensus`) and `EnrichmentOptions.IsEnabled(string typeName)` (test first in `src/Njord.Core.Tests/Configuration/EnrichmentOptionsValidationSpec.cs`: each toggle maps to its type name, unknown name throws)
- [ ] 2.2 Switch the five features' `TypeName`/`Enabled` in `src/Njord/Enrichment/Features/*Enrichment.cs` to the constants and `IsEnabled`; run suite

## 3. Break the Enrichment<->Mqtt cycle (still one assembly)

- [ ] 3.1 Add the failing architecture rule "no type in `Njord.Enrichment` depends on `Njord.Mqtt`" to `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` (red: it currently fails with the feature/MQTT edges)
- [ ] 3.2 Add `src/Njord/Mqtt/IEnrichmentPresenter.cs` and five internal sealed presenters (`AlertPresenter`, `DerivedPresenter`, `TrendPresenter`, `IndexPresenter`, `HistoryPresenter`) under `src/Njord/Mqtt/Presentation/`; move the bodies of `DeviceId`, `BuildDiscoveryPayload`, `ToStateMessages` from the features **verbatim**; each presenter derives its inputs from `IOptions<NjordOptions>` (horizons, `PreferenceResolver.Resolve`, `ParameterRegistry.Resolve`) per design Decision 2a
- [ ] 3.3 Register presenters in the Mqtt service registration (`src/Njord/Configuration/NjordServiceSetup.cs`); change `src/Njord/Mqtt/DiscoveryActor.cs` and `src/Njord/Mqtt/MqttEgressActor.cs` to take `IEnumerable<IEnrichmentPresenter>` (`_featuresByType` -> `_presentersByType`)
- [ ] 3.4 Slim `src/Njord/Enrichment/IEnrichmentFeature.cs` to `TypeName` + `Enabled`; delete the moved members, `using Njord.Mqtt` and the now unused builder calls from the five features; add presenter parity and "no MQTT surface" assertions to `EnrichmentFeatureContractSpec`; adapt `HistoryEnrichmentSpec` (uses `ToStateMessages`) to the presenter
- [ ] 3.5 Run golden masters (1.1, 1.2) with unchanged `.verified.txt`; run the full suite; rule 3.1 is now green; commit `refactor: move enrichment MQTT presentation into Njord.Mqtt presenters`

## 4. Consensus presenter (still one assembly)

- [ ] 4.1 Re-verify the hardwiring: `grep -n '"consensus"' src/Njord/Mqtt/MqttEgressActor.cs src/Njord/Mqtt/DiscoveryActor.cs` shows the `EnrichmentUpdate { TypeName: "consensus", Result: ConsensusResult }` case and the unconditional consensus block per location; note any drift at the top of this file
- [ ] 4.2 Add `src/Njord.Tests/Mqtt/ConsensusSnapshotSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, Verify) against the **current** builders: `TopicScheme.EnrichmentDeviceId`/`ConfigTopic`, `DiscoveryPayloadBuilder.BuildConsensus` for a fixed config and `StatePayloadBuilder.FromConsensus` for a fixed `ConsensusResult` (topic, payload, retain); approve the `.verified.txt` files first
- [ ] 4.3 Add characterization specs to `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` (pass on the current code): consensus discovery is published for every location even when `EnrichmentOptions.Consensus.Enabled` is `false`, and the publish order per location is per-model devices, consensus, then alerts, derived, trends, indices, history
- [ ] 4.4 Add `src/Njord/Mqtt/Presentation/ConsensusPresenter.cs` (internal sealed `IEnrichmentPresenter`): `TypeName` = `EnrichmentTypeNames.Consensus`; `Enabled` constant `true` with a comment that consensus discovery is unconditional today; `DeviceId`/`BuildDiscoveryPayload` wrap `TopicScheme.EnrichmentDeviceId` and `DiscoveryPayloadBuilder.BuildConsensus`, taking `ResolvedParameterSet` and `IOptions<NjordOptions>` via the constructor as `DiscoveryActor` does today; `ToStateMessages` wraps `StatePayloadBuilder.FromConsensus` and returns an empty list for a non-`ConsensusResult`; register it **first** so the publish order stays as in 4.3
- [ ] 4.5 Remove the consensus special cases: the `TypeName: "consensus"` arm in `MqttEgressActor.MapToMqttMessages` and the hardwired block in `DiscoveryActor`; unknown `TypeName` still yields no messages
- [ ] 4.6 Extend the parity spec in `EnrichmentFeatureContractSpec`: presenter set = feature set + `consensus`, and `consensus` is still not in `IEnumerable<IEnrichmentFeature>`
- [ ] 4.7 Run 4.2 and 4.3 (unchanged snapshots and expectations) and the full suite; commit `refactor: present consensus through the presenter registry`

## 5. Extract Njord.Mqtt

- [ ] 5.1 Create `src/Njord.Mqtt/Njord.Mqtt.csproj` (Core only; MQTTnet via `dotnet add package`, never edit versions in csproj); slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and `src/Njord/Njord.csproj`
- [ ] 5.2 Move `src/Njord/Mqtt/*` incl. `Transport/` and `Presentation/` (actors, builders, `TopicScheme`, `DiscoveryContext`, `MqttMessage`, presenters) into the project; actor classes `public`
- [ ] 5.3 Add `AddNjordMqtt` (services, presenters); `RegisterMqttActors` (`IMqttConnectionActor`, `IMqttEgressActor`, `IDiscoveryActor`; names `mqtt-connection`, `mqtt-egress`, `mqtt-discovery`) stays in the host, gated on `Mqtt.Enabled`, after `.WithSqlPersistence(...)`
- [ ] 5.4 Add to the ArchUnit assembly set; adapt `src/Njord.Tests/Mqtt/*Spec.cs`; golden masters pass unchanged; build + suite; commit `refactor: extract Njord.Mqtt`

## 6. Extract Njord.Enrichment

- [ ] 6.1 Create `src/Njord.Enrichment/Njord.Enrichment.csproj` (Core only, no reference to `Njord.Mqtt`); slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and the host
- [ ] 6.2 Move `src/Njord/Enrichment/*` incl. `Features/`, `ForecastHistoryActor` and its state/messages/persistence mapping into the project; actor classes `public`
- [ ] 6.3 Add `AddNjordEnrichment` (registers the five `IEnrichmentFeature` singletons); `RegisterEnrichmentActors` (`IEnrichmentActor`, name `enrichment`) stays in the host
- [ ] 6.4 Add to the ArchUnit assembly set; adapt `src/Njord.Tests/Enrichment/**/*Spec.cs`; build + suite; commit `refactor: extract Njord.Enrichment`

## 7. Host cleanup and architecture rules

- [ ] 7.1 Reduce `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs` to shells calling the per-library extensions; remove emptied folders; `src/Njord/Njord.csproj` references all feature libraries
- [ ] 7.2 In `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` add Enrichment and Mqtt to the lateral matrix from 3a plus the rules "Enrichment does not depend on Mqtt/Grpc" and "Mqtt does not depend on Enrichment" (red-prove each with a temporary reference, then remove); drop the superseded namespace zone rules (the last in-host feature has moved)
- [ ] 7.3 Build + suite; commit `refactor: host composes all feature libraries`

## 8. Documentation and skills

- [ ] 8.1 Update `AGENTS.md`: solution structure tree (`Njord.Mqtt`, `Njord.Enrichment`; no feature folders left in the host), guardrail text for the presenter boundary (Enrichment computes, Mqtt presents)
- [ ] 8.2 Rewrite `.claude/skills/njord-enrichment-feature/SKILL.md` for the feature + presenter pair (parity spec, `EnrichmentTypeNames`, snapshot specs); fix cited paths in `.claude/skills/njord-persistent-actor/SKILL.md` and `njord-actor-spec/SKILL.md`; verify every cited path with `ls`
- [ ] 8.3 Commit `docs: update structure and skills for enrichment/mqtt libraries`

## 9. Validation

- [ ] 9.1 From `src/`: `dotnet build Njord.slnx` (0 errors)
- [ ] 9.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` all pass, count >= baseline from 0.5, golden-master snapshots unchanged (`git diff --stat -- '*.verified.txt'` shows only the new files from 1.1/1.2/4.2)
- [ ] 9.3 `grep -rn "Njord.Mqtt" src/Njord.Enrichment` returns nothing; `grep -rn '"consensus"' src/Njord.Mqtt` finds only the presenter and type-name constants, no dispatch branch; `grep -rn "<ProjectReference" src/Njord.*/*.csproj` shows feature libraries referencing only `Njord.Core`
- [ ] 9.4 Dockerfile: add `COPY` + `dotnet restore` lines for `Njord.Mqtt` and `Njord.Enrichment` csprojs before the full source copy; CI workflows unchanged
- [ ] 9.5 Rewrite check: every `MODIFIED` requirement in this change's delta specs restates the full text and all scenarios, and every symbol named in them (types, members, projects, namespaces) exists in the code (`grep` each identifier); drift found is fixed in the delta
- [ ] 9.6 Startup smoke: from `src/Njord/` run once with `Njord__Mqtt__Enabled=false` and once with an unreachable broker host; it starts, actors resolve, Ctrl+C stops cleanly
- [ ] 9.7 `openspec validate extract-enrichment-mqtt-projects` passes; `dotnet slopwatch` from the repo root if the manifest exists, otherwise note it as skipped
- [ ] 9.8 Commits are Conventional Commits, no attribution trailers, nothing pushed
