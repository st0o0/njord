## 0. Preconditions

- [ ] 0.1 Confirm `extract-core-projects` and `extract-leaf-feature-projects` are applied: `src/Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Domain`, `Njord.Grpc`, `Njord.Ingest`, `Njord.Sensors` exist and the host references them
- [ ] 0.2 Confirm `openspec/specs/architecture-zone-enforcement/spec.md` exists (`ls openspec/specs`), otherwise stop
- [ ] 0.3 Verify what is left in `src/Njord/Pipeline` and `src/Njord/Egress` and that Core/Messages own `StreamSupervision`, `EgressEvent`, `EgressMessages`, `TopicSlug`, `HorizonProjection`, `RequestPipelineSource`/`PipelineSourceResponse` and the markers `IPipelineActor`/`IEgressActor`/`IModelStateActor`; list every unmet item at the top of this file and move that type down first
- [ ] 0.4 Baseline: from `src/` run `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; record test count

## 1. Guards first (red)

- [ ] 1.1 Add the failing rule "no type in `Njord.Egress` depends on a type in `Njord.Pipeline`" to `src/Njord.Tests/Architecture/ZoneArchitectureSpec.cs` (namespace-based while still one assembly); run it red if `ModelStateActor` still uses Pipeline types
- [ ] 1.2 Fix `src/Njord/Egress/ModelStateActor.cs`: lookup via `IPipelineActor`, messages from `Njord.Messages`, `StreamSupervision` from `Njord.Core`; move any remaining type out of `src/Njord/Pipeline` to Core/Messages; rule 1.1 green; run suite
- [ ] 1.3 Add `src/Njord.Tests/Configuration/PersistenceBeforeActorsSpec.cs` (sealed, `[Fact(Timeout = 5000)]`): the production actor-system setup applies `WithSqlPersistence`/persistence configuration before any actor registration; prove it red once by temporarily swapping the order in `src/Njord/Configuration/NjordActorSystemSetup.cs`, then restore
- [ ] 1.4 Run the full suite; commit `test: guard egress-pipeline independence and persistence-before-actors order`

## 2. Extract Njord.Pipeline

- [ ] 2.1 Create `src/Njord.Pipeline/Njord.Pipeline.csproj` referencing only `Njord.Core`; add to `src/Njord.slnx`; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and `src/Njord/Njord.csproj`
- [ ] 2.2 Move the remaining `src/Njord/Pipeline/*` files (`SchedulerActor`, `PipelineActor`, `BudgetTrackerActor`, `BudgetThrottleStage`, `IBudgetGate`, `IBudgetProvider`, `BudgetTrackerState`, `SchedulerState`, `SchedulerMessages`, `WeightedTarget`, `ModelPollState`, persistence mapping) into the project; actor classes `public`
- [ ] 2.3 Add `src/Njord.Pipeline/PipelineServiceCollectionExtensions.cs` (`AddNjordPipeline`) for its services; `RegisterPipelineActors` stays in the host `NjordActorSystemSetup`, after `.WithSqlPersistence(...)`
- [ ] 2.4 Add `Njord.Pipeline` to the ArchUnit assembly set; adapt `src/Njord.Tests/Pipeline/*Spec.cs` usings; build + suite (guards 1.1/1.3 and `ActorKeyRegistrationSpec` green); commit `refactor: extract Njord.Pipeline`

## 3. Extract Njord.Egress

- [ ] 3.1 Create `src/Njord.Egress/Njord.Egress.csproj` referencing only `Njord.Core`; slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and the host
- [ ] 3.2 Move the remaining `src/Njord/Egress/*` files (`EgressActor`, `ModelStateActor`, local types) into the project; actor classes `public`; confirm there is no `using Njord.Pipeline`
- [ ] 3.3 Add `src/Njord.Egress/EgressServiceCollectionExtensions.cs` (`AddNjordEgress`) if there are service registrations; `RegisterEgressActors` (`IEgressActor` `egress`, `IModelStateActor` `model-state`) stays in the host
- [ ] 3.4 Add `Njord.Egress` to the ArchUnit assembly set; adapt `src/Njord.Tests/Egress/*Spec.cs`; build + suite; commit `refactor: extract Njord.Egress`

## 4. Architecture rules (assembly-based)

- [ ] 4.1 In `src/Njord.Tests/Architecture/NjordArchitecture.cs` load all `Njord.*` production assemblies; in `ZoneArchitectureSpec.cs` add the lateral matrix (Pipeline, Egress, Grpc, Ingest, Sensors: only Core/Messages/Persistence/Domain; never each other, never the host) and the upward rule; red-prove each rule once with a temporary reference/type, then remove it
- [ ] 4.2 In `ConventionArchitectureSpec.cs` evaluate the sealed rule over all `Njord.*` production assemblies, excluding compiler-generated `Program`; keep the old namespace zone rules (still needed for Mqtt/Enrichment in the host)
- [ ] 4.3 Build + suite; commit `test: enforce library reference rules per assembly`

## 5. Host cleanup

- [ ] 5.1 Make `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs` call `AddNjordPipeline`/`AddNjordEgress`; remove the emptied `src/Njord/Pipeline` and `src/Njord/Egress` folders; confirm `Mqtt` and `Enrichment` in the host compile against the new libraries
- [ ] 5.2 Build + suite; commit `refactor: host composes pipeline and egress libraries`

## 6. Documentation and skills

- [ ] 6.1 Update `AGENTS.md`: solution structure tree (`Njord.Pipeline`, `Njord.Egress`, still-in-host `Mqtt`/`Enrichment`), reference direction, guardrail "Ingest and Egress never reference each other" stated as enforced by project references + ArchUnit lateral rules
- [ ] 6.2 Fix cited paths for Pipeline/Egress in `.claude/skills/njord-persistent-actor/SKILL.md` and `njord-actor-spec/SKILL.md` (and `njord-enrichment-feature` only where it cites moved files); verify every cited path with `ls`
- [ ] 6.3 Commit `docs: update structure and skills for pipeline and egress libraries`

## 7. Validation

- [ ] 7.1 From `src/`: `dotnet build Njord.slnx` (0 errors)
- [ ] 7.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` all pass, count >= baseline from 0.4
- [ ] 7.3 Dockerfile: add `COPY` + `dotnet restore` lines for `Njord.Pipeline` and `Njord.Egress` csprojs before the full source copy; if the Docker build is run locally use `docker build .` from the repo root; CI workflows unchanged
- [ ] 7.4 `grep -rn "<ProjectReference" src/Njord.Pipeline src/Njord.Egress` shows only `Njord.Core`; `grep -rn "Njord.Mqtt\|Njord.Enrichment" src/Njord.Pipeline src/Njord.Egress` returns nothing
- [ ] 7.5 Startup smoke: from `src/Njord/` run the service once with `Njord__Mqtt__Enabled=false`; it starts, actors resolve (no registry/marker errors), persistent actors recover, Ctrl+C stops cleanly
- [ ] 7.6 `openspec validate extract-pipeline-egress-projects` passes; `dotnet slopwatch` from the repo root if the manifest exists, otherwise note it as skipped
- [ ] 7.7 Commits are Conventional Commits, no attribution trailers, nothing pushed
