## 0. Baseline

- [ ] 0.1 From `src/` run `dotnet build Njord.slnx` (expect 0 warnings) and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; confirm total = 815 and record wall-clock time
- [ ] 0.2 Run the suite 3 times and note any flaky tests (load baseline for Decision 9)
- [ ] 0.3 Record per-folder test counts by running `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.<Area>.*"` per folder (Domain, Persistence, Configuration, Diagnostics, Actors, Egress, Grpc, Pipeline, Ingest, Sensors, Mqtt, Enrichment, Health, Architecture); these numbers are the per-group acceptance counts below
- [ ] 0.4 Confirm with the user that CI (`st0o0/github-workflows` `dotnet test --solution`) picking up every test project from `Njord.slnx` is acceptable; do not edit any workflow

## 1. Shared infrastructure

- [ ] 1.1 `git mv src/Njord.Tests/xunit.runner.json src/xunit.runner.json` and change `src/Njord.Tests/Njord.Tests.csproj` to include `..\xunit.runner.json` as linked Content (`Link="xunit.runner.json"`, `PreserveNewest`)
- [ ] 1.2 Add `src/Njord.Tests.Shared/FailingRefProvider.cs` (namespace `Njord.Tests.Shared`, Egress/Pipeline arms only, no `Njord.Mqtt` usings); keep `src/Njord.Tests/Actors/FailingRefProvider.cs` unchanged for the host users (Mqtt, Enrichment)
- [ ] 1.3 Validate: `dotnet build Njord.slnx` 0 warnings; `dotnet run --project Njord.Tests/Njord.Tests.csproj` = 815

## 2. Njord.Domain.Tests (27 files, 279 methods)

- [ ] 2.1 Create `src/Njord.Domain.Tests/Njord.Domain.Tests.csproj` (Exe, `xunit.v3.mtp-v2`, `Microsoft.Testing.Extensions.CodeCoverage`, `Using Include="Xunit"`, linked `xunit.runner.json`, reference `Njord.Domain` only); add to the `/Tests/` folder of `src/Njord.slnx`
- [ ] 2.2 `git mv src/Njord.Tests/Domain/Weather src/Njord.Domain.Tests/Weather` (12), `.../Domain/Sensors` (1), `.../Domain/Analysis` (14); change namespaces `Njord.Tests.Domain.*` to `Njord.Domain.Tests.*`
- [ ] 2.3 Add `<InternalsVisibleTo Include="Njord.Domain.Tests" />` to `src/Njord.Domain/Njord.Domain.csproj`; add other libraries only if the build requires it
- [ ] 2.4 Validate: build 0 warnings; run `Njord.Domain.Tests` and `Njord.Tests`; sum = 815; commit `test: extract Njord.Domain.Tests`

## 3. Njord.Persistence.Tests (4 files + 5 snapshots)

- [ ] 3.1 Create `src/Njord.Persistence.Tests/Njord.Persistence.Tests.csproj` (adds `Verify.XunitV3`; references `Njord.Persistence`, `Njord.Domain`); add to `src/Njord.slnx`
- [ ] 3.2 Add `src/Njord.Persistence.Tests/ModuleInitializer.cs` (copy of `src/Njord.Tests/ModuleInitializer.cs`, namespace `Njord.Persistence.Tests`)
- [ ] 3.3 `git mv` `EnrichmentResultSerializationSpec`, `EnrichmentSnapshotDtoSerializationSpec`, `ForecastHistoryDtoSerializationSpec`, `ForecastSnapshotDtoSerializationSpec` (`.cs` and every `.verified.txt`, 5 snapshots) from `src/Njord.Tests/Persistence/` to `src/Njord.Persistence.Tests/`; `SchedulerDtoSerializationSpec.cs` and its `.verified.txt` stay until group 7
- [ ] 3.4 Add `InternalsVisibleTo Njord.Persistence.Tests` to `src/Njord.Persistence/Njord.Persistence.csproj` (and Domain if needed)
- [ ] 3.5 Validate: build 0 warnings; no `*.received.*` files; sum = 815; commit

## 4. Njord.Core.Tests (17 files)

- [ ] 4.1 Create `src/Njord.Core.Tests/Njord.Core.Tests.csproj` (TestKit packages, `Microsoft.Extensions.TimeProvider.Testing`; references `Njord.Core`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [ ] 4.2 `git mv` the 10 Core configuration specs (`BudgetCalculatorSpec`, `EnrichmentOptionsValidationSpec`, `LocationOptionsSpec`, `ModelCoverageRegistrySpec`, `NjordOptionsSpec`, `NjordOptionsValidatorSpec`, `ParameterOptionsValidationSpec`, `PersistenceOptionsValidationSpec`, `RequestBudgetSpec`, `SensorOptionsValidationSpec`) to `src/Njord.Core.Tests/Configuration/`, the 6 `src/Njord.Tests/Diagnostics/*` files to `src/Njord.Core.Tests/Diagnostics/`, and `src/Njord.Tests/Actors/StreamConsumerActorSpec.cs` to `src/Njord.Core.Tests/Actors/`; fix namespaces
- [ ] 4.3 `ActorKeyRegistrationSpec`, `NjordActorSystemSetupSpec`, `NjordServiceSetupSpec`, `PersistenceBeforeActorsSpec` stay in `src/Njord.Tests/Configuration/` (host types)
- [ ] 4.4 Add `InternalsVisibleTo Njord.Core.Tests` to `src/Njord.Core/Njord.Core.csproj`
- [ ] 4.5 Validate: build 0 warnings; sum = 815; commit

## 5. Njord.Egress.Tests (4 files)

- [ ] 5.1 Create `src/Njord.Egress.Tests/Njord.Egress.Tests.csproj` (TestKit packages; references `Njord.Egress`, `Njord.Pipeline`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [ ] 5.2 `git mv` the 4 `.cs` files of `src/Njord.Tests/Egress/` (not `Snapshots/`, see design Risks) to `src/Njord.Egress.Tests/`; `ModelStateActorSpec` switches to `Njord.Tests.Shared.FailingRefProvider`; drop the `Njord.Tests.Actors` using
- [ ] 5.3 Add `InternalsVisibleTo Njord.Egress.Tests` to `src/Njord.Egress/Njord.Egress.csproj` (and Pipeline if needed)
- [ ] 5.4 Validate: build 0 warnings; sum = 815; commit

## 6. Njord.Grpc.Tests (12 files)

- [ ] 6.1 Create `src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` (TestKit packages; references `Njord.Grpc`, `Njord.Egress`, `Njord.Pipeline`, `Njord.Persistence`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [ ] 6.2 `git mv src/Njord.Tests/Grpc/*.cs src/Njord.Grpc.Tests/` (12, including `TestServerCallContext.cs`); `GrpcSnapshotConsumerTerminatedSpec` and `WeatherGrpcServiceSpec` use the Shared `FailingRefProvider`
- [ ] 6.3 Add `InternalsVisibleTo Njord.Grpc.Tests` to `src/Njord.Grpc/Njord.Grpc.csproj` (and Egress/Pipeline/Persistence if needed); decide the fate of the unused `Grpc.Net.Client` reference in `src/Njord.Tests.Shared` (drop only if the build proves it unused)
- [ ] 6.4 Validate: build 0 warnings; sum = 815; commit

## 7. Njord.Pipeline.Tests (18 files + 1 snapshot)

- [ ] 7.1 Create `src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` (TestKit packages, `Verify.XunitV3`; references `Njord.Pipeline`, `Njord.Ingest`, `Njord.Persistence`, `Njord.Tests.Shared`); add `ModuleInitializer.cs`; add to `src/Njord.slnx`
- [ ] 7.2 `git mv` all `src/Njord.Tests/Pipeline/*` except `PollPipelineSpec.cs` to `src/Njord.Pipeline.Tests/` (17, including `StopwatchGap.cs`), plus `src/Njord.Tests/Persistence/SchedulerDtoSerializationSpec.cs` and its `.verified.txt` (1 + 1); `SchedulerActorRefFailureSpec` uses the Shared `FailingRefProvider`
- [ ] 7.3 `PollPipelineSpec.cs` stays in `src/Njord.Tests/Pipeline/` (uses host `Njord.Mqtt`)
- [ ] 7.4 Add `InternalsVisibleTo Njord.Pipeline.Tests` to `src/Njord.Pipeline/Njord.Pipeline.csproj` (and Persistence/Ingest if needed)
- [ ] 7.5 Validate: build 0 warnings; no `*.received.*`; sum = 815; run the project 3 times and compare flakes with task 0.2; commit

## 8. Njord.Architecture.Tests (4 files)

- [ ] 8.1 Spike: `src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` (Exe, `TngTech.ArchUnitNET.xUnitV3`, `xunit.v3.mtp-v2`, CodeCoverage) referencing `Njord` and all other test projects; confirm the project builds and runs without double test execution; if not, switch to the fallback in design Decision 7
- [ ] 8.2 `git mv src/Njord.Tests/Architecture/*.cs src/Njord.Architecture.Tests/`; update `NjordArchitecture.TestsAssembly` to the set of test assemblies (one `typeof` per project) and rename `TestTypes` description; no rule is changed
- [ ] 8.3 Add to `src/Njord.slnx`; remove ArchUnit and now-unused references from `src/Njord.Tests/Njord.Tests.csproj`
- [ ] 8.4 Validate: build 0 warnings; the 17 architecture tests pass and the convention rule still sees every test class; sum = 815; commit

## 9. Host project cleanup

- [ ] 9.1 In `src/Njord.Tests/Njord.Tests.csproj` remove unused `PackageReference`/`ProjectReference` items (check by building); keep `Njord`, Verify, `Mvc.Testing`, TestKit packages
- [ ] 9.2 Remove `InternalsVisibleTo Njord.Tests` from any production csproj no leftover test needs (try removal, keep if the build fails)
- [ ] 9.3 Confirm the 6 remaining `src/Njord.Tests` areas (Mqtt, Enrichment, Health, Configuration, Ingest, Sensors) plus `Pipeline/PollPipelineSpec.cs` and `Actors/FailingRefProvider.cs` are all that is left
- [ ] 9.4 Validate: build 0 warnings; sum = 815; commit

## 10. Docs

- [ ] 10.1 `AGENTS.md` Solution structure: list the 8 test projects and Shared; Architecture guardrails line `src/Njord.Architecture.Tests/`; Build & test: one `dotnet run --project` line per test project, the `-class` form, the solution-wide `dotnet test --solution Njord.slnx` and the per-project loop with the summed 815, a note that host tests (Mqtt/Enrichment) live in `Njord.Tests` until `extract-enrichment-mqtt-projects`, and the load note from design Decision 9; update the `Njord.Tests` known-deviation path (`Domain/Analysis/ConsensusComputerSpec.cs` becomes `Njord.Domain.Tests/Analysis/...`, `Configuration/ModelCoverageRegistrySpec.cs` becomes `Njord.Core.Tests/Configuration/...`)
- [ ] 10.2 `README.md` line 97 test command; `.claude/skills/njord-actor-spec/SKILL.md` (command and example paths for `BudgetTrackerActorSpec`, `OpsGrpcServiceSpec`, `ModelStateActorSpec`, DTO specs, `ModuleInitializer` path, description "in src/Njord.Tests"), `.claude/skills/njord-enrichment-feature/SKILL.md` line 29 and `.claude/skills/njord-persistent-actor/SKILL.md` line 77 (`src/Njord.Persistence.Tests/`)
- [ ] 10.3 `grep -rn "Njord.Tests" AGENTS.md README.md .claude Dockerfile .github openspec/specs` shows no stale path; confirm the Dockerfile copies no test project; do not touch `.github/`
- [ ] 10.4 Update `extract-enrichment-mqtt-projects` task paths for `Architecture` and Core configuration specs (`src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`, `src/Njord.Core.Tests/Configuration/EnrichmentOptionsSpec.cs`) if it has not landed yet
- [ ] 10.5 Commit `docs: document per-library test projects`

## 11. Final validation

- [ ] 11.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings
- [ ] 11.2 Run every test project with `dotnet run --project <name>/<name>.csproj` (Domain, Persistence, Core, Egress, Grpc, Pipeline, Architecture, Njord.Tests) and `dotnet test --solution Njord.slnx`; summed total = 815 and no failures
- [ ] 11.3 Repeat the full run 3 times; compare flakes and wall-clock with task 0.1/0.2; report the numbers to the user together with the CI confirmation from task 0.4
- [ ] 11.4 `git status` shows no `*.received.*`, no changed `*.verified.*` content (`git diff -M --stat` only renames); `dotnet slopwatch` from the repo root passes
