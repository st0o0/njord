## 0. Preconditions

- [x] 0.1 Confirm `extract-core-projects` and `extract-leaf-feature-projects` are applied: `src/Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Domain`, `Njord.Grpc`, `Njord.Ingest`, `Njord.Sensors` exist and the host references them
- [x] 0.2 Confirm `openspec/specs/architecture-zone-enforcement/spec.md` exists (`ls openspec/specs`), otherwise stop
- [x] 0.3 Verify what is left in `src/Njord/Pipeline` and `src/Njord/Egress` and that Core/Messages own `StreamSupervision`, `EgressEvent`, `EgressMessages`, `TopicSlug`, `HorizonProjection`, `RequestPipelineSource`/`PipelineSourceResponse` and the markers `IPipelineActor`/`IEgressActor`/`IModelStateActor`; list every unmet item at the top of this file and move that type down first. Result 2026-10-02: all met except `TopicSlug`/`HorizonProjection` (still in `src/Njord/Egress`, they stay there, see design.md); `IEgressActor`/`IPipelineActor`/`IModelStateActor` are in `src/Njord.Core/Actors/ActorKeys.cs`; Egress -> Pipeline residue is only an unused `using Njord.Pipeline;` in `ModelStateActor.cs`
- [x] 0.4 Baseline: from `src/` run `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; record test count (801 tests, build 0 errors/0 warnings; one run had 1 unidentified load-sensitive failure, rerun 801/0 failed)

## 1. Guards first (red)

- [x] 1.1 Add the failing rule "no type in `Njord.Egress` depends on a type in `Njord.Pipeline`" to `src/Njord.Tests/Architecture/ZoneArchitectureSpec.cs` (namespace-based while still one assembly); run it red if `ModelStateActor` still uses Pipeline types (it does not, so prove red once with a temporary reference to a Pipeline type, then remove it)
- [x] 1.2 Fix `src/Njord/Egress/ModelStateActor.cs` (lookup, messages and `StreamSupervision` already used markers/Messages/Core; removed the stray `using Njord.Pipeline;`); move any remaining type out of `src/Njord/Pipeline` to Core/Messages; rule 1.1 green; run suite
- [x] 1.3 Add `src/Njord.Tests/Configuration/PersistenceBeforeActorsSpec.cs` (sealed, plain `[Fact]`: synchronous, xUnit1069 forbids a Timeout without a token): the production actor-system setup applies `WithSqlPersistence`/persistence configuration before any actor registration. Mechanism: `NjordActorSystemSetup.ConfigureSystem` (extracted from `BuildSystem`) and `WithNjordActors` fails fast when the journal plugin is not yet configured. Akka.Hosting keeps actor starters and Hocon separate, so the order is otherwise unobservable. Proven red once by swapping the order, then restored
- [x] 1.4 Run the full suite; commit (split into two commits: `refactor: route Egress dependency on Pipeline through marker`, `test: guard persistence-before-actors registration order`); suite 804 tests green

## 2. Extract Njord.Pipeline

- [x] 2.1 Create `src/Njord.Pipeline/Njord.Pipeline.csproj` referencing only `Njord.Core`; add to `src/Njord.slnx`; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and `src/Njord/Njord.csproj`
- [x] 2.2 Move the remaining `src/Njord/Pipeline/*` files (`SchedulerActor`, `PipelineActor`, `BudgetTrackerActor`, `BudgetThrottleStage`, `IBudgetGate`, `IBudgetProvider`, `BudgetTrackerState`, `SchedulerState`, `ModelPollState`, `SchedulerDtoMapping`, `BudgetTrackerDtoMapping`; 11 files; the DTO mappings keep namespace `Njord.Persistence`) into the project; actor classes `public`
- [x] 2.3 Add `src/Njord.Pipeline/PipelineServiceCollectionExtensions.cs` (`AddNjordPipeline`: `IBudgetProvider` and `IBudgetGate<WeightedTarget>` registrations, moved from `NjordServiceSetup`); `RegisterPipelineActors` stays in the host `NjordActorSystemSetup`, after `.WithSqlPersistence(...)`
- [x] 2.4 Add `Njord.Pipeline` to the ArchUnit assembly set; adapt `src/Njord.Tests/Pipeline/*Spec.cs` usings; build + suite (guards 1.1/1.3 and `ActorKeyRegistrationSpec` green); commit `refactor: extract Njord.Pipeline project` (Dockerfile restore/COPY lines for `Njord.Pipeline` were added in this commit because the Docker restore breaks without them; 7.3 then only needs the Egress lines; packages `Servus.Akka` and `Akka.Persistence.Hosting` added to the csproj; the Pipeline reference-rule test is `LayerReferenceSpec.Pipeline_references_only_Core_and_below`; suite 805 tests)

## 3. Extract Njord.Egress

- [x] 3.1 Create `src/Njord.Egress/Njord.Egress.csproj` referencing only `Njord.Core`; slnx; `InternalsVisibleTo Njord.Tests`; project references from `Njord.Tests` and the host
- [x] 3.2 Move the remaining `src/Njord/Egress/*` files (`EgressActor`, `ModelStateActor`, local types) into the project; actor classes `public`; confirm there is no `using Njord.Pipeline`
- [x] 3.3 Add `src/Njord.Egress/EgressServiceCollectionExtensions.cs` (`AddNjordEgress`) if there are service registrations; `RegisterEgressActors` (`IEgressActor` `egress`, `IModelStateActor` `model-state`) stays in the host (Result: Egress has no service registrations, so `AddNjordEgress` was NOT created; `NjordServiceSetup` has nothing to move)
- [x] 3.4 Add `Njord.Egress` to the ArchUnit assembly set; adapt `src/Njord.Tests/Egress/*Spec.cs`; build + suite; commit `refactor: extract Njord.Egress` (4 files incl. `HorizonProjection`/`TopicSlug`, which keep namespace `Njord.Egress` and are used by host Mqtt/Enrichment; package `Servus.Akka` added; suite 805)

## 4. Architecture rules (assembly-based)

- [x] 4.1 In `src/Njord.Tests/Architecture/NjordArchitecture.cs` load all `Njord.*` production assemblies; in `ZoneArchitectureSpec.cs` add the lateral matrix (Pipeline, Egress, Grpc, Ingest, Sensors: only Core/Messages/Persistence/Domain; never each other, never the host) and the upward rule; red-prove each rule once with a temporary reference/type, then remove it
- [x] 4.2 In `ConventionArchitectureSpec.cs` evaluate the sealed rule over all `Njord.*` production assemblies, excluding compiler-generated `Program`; keep the old namespace zone rules (still needed for Mqtt/Enrichment in the host)
- [x] 4.3 Build + suite; commit `test: enforce library reference rules per assembly` (Result: library assemblies discovered from the host's references so new libraries are covered automatically; lateral Theory over the 5 feature libs + upward Theory over the 4 base libs in `ZoneArchitectureSpec`, `Egress_references_only_Core_and_below` in `LayerReferenceSpec`; red-proven once with a temporary Egress -> Pipeline reference (3 failures: namespace rule, lateral theory case, reference rule); the upward rule cannot be red-proven because the compiler rejects the cycle; suite 815)

## 5. Host cleanup

- [x] 5.1 Make `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs` call `AddNjordPipeline`/`AddNjordEgress`; remove the emptied `src/Njord/Pipeline` and `src/Njord/Egress` folders; confirm `Mqtt` and `Enrichment` in the host compile against the new libraries
- [x] 5.2 Build + suite; commit `refactor: host composes pipeline and egress libraries` (Result: no code change needed; `AddNjordPipeline` is already called from `NjordServiceSetup`, there is no `AddNjordEgress` (3.3), actor registration stays in `NjordActorSystemSetup`, `Mqtt`/`Enrichment` compile against the libraries, build 0 warnings, suite 815 from group 4; only empty leftover host folders were removed, so no separate commit)

## 6. Documentation and skills

- [x] 6.1 Update `AGENTS.md`: solution structure tree (`Njord.Pipeline`, `Njord.Egress`, still-in-host `Mqtt`/`Enrichment`), reference direction, guardrail "Ingest and Egress never reference each other" stated as enforced by project references + ArchUnit lateral rules
- [x] 6.2 Fix cited paths for Pipeline/Egress in `.claude/skills/njord-persistent-actor/SKILL.md` and `njord-actor-spec/SKILL.md` (and `njord-enrichment-feature` only where it cites moved files); verify every cited path with `ls`
- [x] 6.3 Commit `docs: update structure and skills for pipeline and egress libraries`

## 7. Validation

- [x] 7.1 From `src/`: `dotnet build Njord.slnx` (0 errors) (0 errors, 0 warnings)
- [x] 7.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` all pass, count >= baseline from 0.4 (815 tests, 0 failed; baseline 801/805)
- [x] 7.3 Dockerfile: add `COPY` + `dotnet restore` lines for `Njord.Pipeline` and `Njord.Egress` csprojs before the full source copy; if the Docker build is run locally use `docker build .` from the repo root; CI workflows unchanged (Egress COPY lines added; `docker build -t njord:split-check .` succeeded, image removed afterwards)
- [x] 7.4 `grep -rn "<ProjectReference" src/Njord.Pipeline src/Njord.Egress` shows only `Njord.Core`; `grep -rn "Njord.Mqtt\|Njord.Enrichment" src/Njord.Pipeline src/Njord.Egress` returns nothing (verified)
- [x] 7.5 Startup smoke: from `src/Njord/` run the service once with `Njord__Mqtt__Enabled=false`; it starts, actors resolve (no registry/marker errors), persistent actors recover, Ctrl+C stops cleanly (ran with ASPNETCORE_ENVIRONMENT=Development, Mqtt disabled; started, stopped cleanly on SIGTERM; only the 3 known AbruptTermination lines for egress-hub/pipeline-fetch-in/out)
- [x] 7.6 `openspec validate extract-pipeline-egress-projects` passes; `dotnet slopwatch` from the repo root if the manifest exists, otherwise note it as skipped (validate passes; `dotnet slopwatch` skipped: no `.slopwatch` manifest and the tool is not installed)
- [x] 7.7 Commits are Conventional Commits, no attribution trailers, nothing pushed (verified; nothing pushed)
