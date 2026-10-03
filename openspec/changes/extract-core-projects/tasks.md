## 1. Safety net before any move

- [ ] 1.1 Baseline: from `src/` run `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; record the test count in this file's notes (build must have 0 errors; `fix-test-warnings` is applied)

## 2. Njord.Domain

- [ ] 2.1 Create `src/Njord.Domain/Njord.Domain.csproj` (`Microsoft.NET.Sdk`, no references, `InternalsVisibleTo Include="Njord.Tests"`), add it to `src/Njord.slnx`, reference it from `src/Njord/Njord.csproj`
- [ ] 2.2 `git mv` `src/Njord/Domain/Weather/*` (13 files), `src/Njord/Domain/Analysis/*` (all files) and `src/Njord/Domain/Sensors/{SensorKind,SensorReading,SensorSnapshot}.cs` into `src/Njord.Domain/` keeping folder names and namespaces; leave `Domain/Sensors/SensorHubMessages.cs` for task 4
- [ ] 2.3 `git mv` `src/Njord/Configuration/{AlertOptions,HistoryOptions,IndexOptions,IndexPreferences,LocationIndexOverride,LocationOptions}.cs` to `src/Njord.Domain/Options/` (namespace stays `Njord.Configuration`); if one of them needs another `Configuration` type, stop and record it in design.md instead of widening the move
- [ ] 2.3a Add a package only where the compiler requires it (`dotnet add src/Njord.Domain package <name>`), never a version in the csproj
- [ ] 2.4 Add `Njord.Domain` to the assemblies loaded by `src/Njord.Tests/Architecture/NjordArchitecture.cs` so the existing zone specs still see Domain types; build and run the full suite (green)

## 3. Njord.Persistence (DTOs only)

- [ ] 3.1 Grep consumers of each `*Mapping` class and confirm the target homes in design.md Decision 1; then `git mv`/split so the mapping classes live outside the DTO files, still in the host and still in namespace `Njord.Persistence`: `SchedulerDtoMapping` + `BudgetTrackerDtoMapping` → `src/Njord/Pipeline/`, `ForecastSnapshotMapping` + `EnrichmentSnapshotMapping` → `src/Njord/Grpc/`, `ForecastHistoryDtoMapping` → `src/Njord/Enrichment/`; build and run the full suite (green)
- [ ] 3.2 Create `src/Njord.Persistence/Njord.Persistence.csproj` (no project references; `dotnet add src/Njord.Persistence package Newtonsoft.Json` if not already transitive), add to `src/Njord.slnx`, reference it from the host; `git mv` the five DTO files from `src/Njord/Persistence/` (namespace unchanged)
- [ ] 3.3 Verify no `.verified.*` file changed and no `.received.*` file exists (`git status --short` over `src/Njord.Tests`); run `Persistence/*SerializationSpec` classes, then the full suite (green)

## 4. Njord.Messages

- [ ] 4.1 Hoist co-located/nested messages to their own files inside the host first (build + suite green): `BudgetResponse`, `BudgetUsageResult`, `BudgetResponseFailed`, `BudgetTrackerActor.RecordApiCall`, `BudgetTrackerActor.QueryBudgetUsage` → `src/Njord/Pipeline/BudgetMessages.cs` as top-level records; `PushResult` → `src/Njord/Sensors/` message file; `PollPhase` out of `Pipeline/ModelPollState.cs` into its own file
- [ ] 4.2 Create `src/Njord.Messages/Njord.Messages.csproj` (references `Njord.Domain`; `dotnet add src/Njord.Messages package Akka.Streams`), add to `src/Njord.slnx` and reference from the host, with `InternalsVisibleTo Njord.Tests`
- [ ] 4.3 `git mv` into `src/Njord.Messages/`: `Egress/{EgressMessages,EgressEvent}.cs` → `Egress/`; `Pipeline/{SchedulerMessages,WeightedTarget,BudgetMessages}.cs` and the `PollPhase` file → `Pipeline/`; `Domain/Sensors/SensorHubMessages.cs` and the `PushResult` file → `Sensors/`; `Actors/SharedMessages.cs` → `Common/`; `Grpc/SnapshotMessages.cs` → `Snapshots/`; apply namespaces `Njord.Messages.{Egress,Pipeline,Sensors,Common,Snapshots}` and update `using` directives in `src/Njord/` and `src/Njord.Tests/`
- [ ] 4.4 Build and run the full suite (green); confirm no message record kept inside an actor class is referenced from another folder (grep)

## 5. Njord.Core

- [ ] 5.1 Create `src/Njord.Core/Njord.Core.csproj` (references `Njord.Domain`, `Njord.Messages`, `Njord.Persistence`; packages added via `dotnet add src/Njord.Core package ...` driven by compile errors; prefer package references over a `FrameworkReference` to `Microsoft.AspNetCore.App`), add to `src/Njord.slnx`, reference from the host, `InternalsVisibleTo Njord.Tests`
- [ ] 5.2 `git mv` `src/Njord/Configuration/*` except `NjordServiceSetup.cs`, `NjordActorSystemSetup.cs`, `NjordApplicationSetup.cs` into `src/Njord.Core/Configuration/` (namespaces unchanged); the three Setup files stay in the host
- [ ] 5.3 `git mv` `src/Njord/Diagnostics/*` → `src/Njord.Core/Diagnostics/`, `src/Njord/Health/NjordHealthState.cs` → `src/Njord.Core/Health/` (health checks stay in the host), `src/Njord/Ingest/IOpenMeteoClient.cs` → `src/Njord.Core/Ingest/` (namespace `Njord.Ingest` kept)
- [ ] 5.4 `git mv` `src/Njord/Actors/StreamConsumerActor.cs` and `src/Njord/Pipeline/StreamSupervision.cs` → `src/Njord.Core/Actors/`; set `StreamSupervision` namespace to `Njord.Actors` and update its callers (Egress, Enrichment, Mqtt, Grpc, Pipeline)
- [ ] 5.5 Build and run the full suite (green); `Njord.Tests.Shared` (`FakeOpenMeteoClient`, `TestPersistenceConfig`) still compiles without csproj changes

## 6. Wiring, enforcement, docs

- [ ] 6.1 Update `src/Njord.Tests/Architecture/NjordArchitecture.cs` to load all five production assemblies and make `ProductionTypes` span them; keep the zone and sealed/`Spec` rules and the `Timeout = 60_000` comment
- [ ] 6.2 Add `src/Njord.Tests/Architecture/LayerReferenceSpec.cs` (red first by adding a temporary forbidden reference, then removed): `Njord.Domain` references no other Njord assembly, `Njord.Persistence` none, `Njord.Messages` only `Njord.Domain`, `Njord.Core` only those three
- [ ] 6.3 Update `Dockerfile`: copy each new csproj before `dotnet restore Njord/Njord.csproj` (`Njord.Domain`, `Njord.Persistence`, `Njord.Messages`, `Njord.Core`), copy the project folders before `dotnet publish`; do not change workflows
- [ ] 6.4 Update `AGENTS.md` "Solution structure" (new projects, reference direction) and the guardrail line about enforced zones; do not change decision text
- [ ] 6.5 Update cited paths in `.claude/skills/njord-persistent-actor/SKILL.md`, `.claude/skills/njord-enrichment-feature/SKILL.md`, `.claude/skills/njord-actor-spec/SKILL.md`; `grep -rnE "src/Njord/(Domain|Persistence|Configuration|Diagnostics|Actors)" AGENTS.md CLAUDE.md .claude/skills` must return only intentional hits

- [ ] 6.6 In `AGENTS.md` next to the persistence rule add one line: while the project is 0.x, moving or renaming persistence DTO types is allowed as a breaking change (`refactor!:`), but `[JsonProperty]` names and `Version` semantics stay extend-only

## 7. Validation

- [ ] 7.1 From `src/`: `dotnet build Njord.slnx` (0 errors, no new warnings) and `dotnet run --project Njord.Tests/Njord.Tests.csproj` (all green, count ≥ the baseline from 1.1 plus the new specs)
- [ ] 7.2 `git status --short -- 'src/Njord.Tests/**/*.verified.*'` shows no modification and no `*.received.*` exists
- [ ] 7.3 `docker build -t njord:split-check .` from the repo root if Docker is available (otherwise note it and rely on CI)
- [ ] 7.4 `openspec validate extract-core-projects` passes
- [ ] 7.5 One Conventional Commit per task group (for example `refactor: extract Njord.Domain project`); the commit that moves the persistence DTOs (group 3) is `refactor!: extract Njord.Persistence project` with a `BREAKING CHANGE:` footer saying persisted scheduler/budget/snapshot data may need to be reset; no push; no attribution trailers

## 8. Follow-up (not part of this change)

- [ ] 8.1 `extract-leaf-feature-projects` (stage 2: `Grpc` with protos, `Ingest`, `Sensors`) and `extract-pipeline-egress-projects` (stage 3: `Pipeline`, `Egress`, `Enrichment`/`Mqtt` descriptor refactor, per-project `AddNjordX()` setup, compiler-enforced zones)
