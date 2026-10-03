## 0. Baseline

- [x] 0.1 From `src/` run `dotnet build Njord.slnx` (expect 0 warnings) and `dotnet run --project Njord.Tests/Njord.Tests.csproj`; confirm total = 825 (baseline measured 2026-10-03: 825 tests, build ~55 s, run ~13-16 s) and record wall-clock time
- [x] 0.2 Run the suite 3 times and note any flaky tests (load baseline for Decision 9)
- [x] 0.3 Record per-folder test counts by running `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.<Area>.*"` per folder (Domain 286, Persistence 19, Configuration 92, Diagnostics 22, Actors 8, Egress 27, Grpc 74, Pipeline 108, Ingest 15, Sensors 6, Mqtt 82, Enrichment 49, Health 12, Architecture 25; sum 825); these numbers are the per-group acceptance counts below
- [x] 0.4 Confirm with the user that CI (`st0o0/github-workflows` `dotnet test --solution`) picking up every test project from `Njord.slnx` is acceptable; do not edit any workflow (confirmed by the user)

## 1. Shared infrastructure

- [x] 1.1 `git mv src/Njord.Tests/xunit.runner.json src/xunit.runner.json` and change `src/Njord.Tests/Njord.Tests.csproj` to include `..\xunit.runner.json` as linked Content (`Link="xunit.runner.json"`, `PreserveNewest`)
- [x] 1.2 Add `src/Njord.Tests.Shared/FailingRefProvider.cs` (public, namespace `Njord.Tests.Shared`, Egress/Pipeline arms only, no `Njord.Mqtt` usings); keep `src/Njord.Tests/Actors/FailingRefProvider.cs` unchanged for the host users (Mqtt, Enrichment); host specs importing both namespaces use a `using FailingRefProvider = Njord.Tests.Actors.FailingRefProvider;` alias, the Egress/Grpc/Pipeline specs drop `using Njord.Tests.Actors;`
- [x] 1.3 Validate: `dotnet build Njord.slnx` 0 warnings; `dotnet run --project Njord.Tests/Njord.Tests.csproj` = 825

## 2. Njord.Domain.Tests (27 files, 286 tests)

- [x] 2.1 Create `src/Njord.Domain.Tests/Njord.Domain.Tests.csproj` (Exe, `xunit.v3.mtp-v2`, `Microsoft.Testing.Extensions.CodeCoverage`, `Microsoft.Extensions.TimeProvider.Testing` (FakeTimeProvider in 8 specs), `Using Include="Xunit"`, linked `xunit.runner.json`, reference `Njord.Domain` only); add to the `/Tests/` folder of `src/Njord.slnx`
- [x] 2.2 `git mv src/Njord.Tests/Domain/Weather src/Njord.Domain.Tests/Weather` (12), `.../Domain/Sensors` (1), `.../Domain/Analysis` (14); change namespaces `Njord.Tests.Domain.*` to `Njord.Domain.Tests.*`
- [x] 2.3 Add `<InternalsVisibleTo Include="Njord.Domain.Tests" />` to `src/Njord.Domain/Njord.Domain.csproj`; add other libraries only if the build requires it
- [x] 2.4 Validate: build 0 warnings; run `Njord.Domain.Tests` and `Njord.Tests`; sum = 825; commit `test: extract Njord.Domain.Tests`

## 3. Njord.Persistence.Tests (3 files + 3 snapshots, 10 tests)

- [x] 3.1 Create `src/Njord.Persistence.Tests/Njord.Persistence.Tests.csproj` (adds `Verify.XunitV3`; references `Njord.Persistence`, `Njord.Domain`, `Njord.Grpc` because `EnrichmentSnapshotMapping` (namespace `Njord.Persistence`) lives in the Grpc assembly); add to `src/Njord.slnx`
- [x] 3.2 Add `src/Njord.Persistence.Tests/ModuleInitializer.cs` (copy of `src/Njord.Tests/ModuleInitializer.cs`, namespace `Njord.Persistence.Tests`)
- [x] 3.3 `git mv` `EnrichmentResultSerializationSpec`, `EnrichmentSnapshotDtoSerializationSpec`, `ForecastSnapshotDtoSerializationSpec` (`.cs` and every `.verified.txt`, 3 snapshots) from `src/Njord.Tests/Persistence/` to `src/Njord.Persistence.Tests/`; `ForecastHistoryDtoSerializationSpec` (+2 snapshots) stays in `src/Njord.Tests/Persistence/` because `ForecastHistoryDtoMapping` is a host type (`Njord/Enrichment`; goes with `extract-enrichment-mqtt-projects`); `SchedulerDtoSerializationSpec.cs` and its `.verified.txt` stay until group 7
- [x] 3.4 Add `InternalsVisibleTo Njord.Persistence.Tests` to `src/Njord.Persistence/Njord.Persistence.csproj` (and Domain if needed)
- [x] 3.5 Validate: build 0 warnings; no `*.received.*` files; sum = 825; commit

## 4. Njord.Core.Tests (18 files, 97 tests)

- [x] 4.1 Create `src/Njord.Core.Tests/Njord.Core.Tests.csproj` (`Akka.Hosting.TestKit`, `Microsoft.Extensions.TimeProvider.Testing`, `Servus.Akka`; references `Njord.Core`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [x] 4.2 `git mv` the 10 Core configuration specs (`BudgetCalculatorSpec`, `EnrichmentOptionsValidationSpec`, `LocationOptionsSpec`, `ModelCoverageRegistrySpec`, `NjordOptionsSpec`, `NjordOptionsValidatorSpec`, `ParameterOptionsValidationSpec`, `PersistenceOptionsValidationSpec`, `RequestBudgetSpec`, `SensorOptionsValidationSpec`) to `src/Njord.Core.Tests/Configuration/`, the 6 `src/Njord.Tests/Diagnostics/*` files to `src/Njord.Core.Tests/Diagnostics/`, and `src/Njord.Tests/Actors/StreamConsumerActorSpec.cs` plus `RetryBackoffSpec.cs` (added after the change was written) to `src/Njord.Core.Tests/Actors/`; fix namespaces
- [x] 4.3 `ActorKeyRegistrationSpec`, `NjordActorSystemSetupSpec`, `NjordServiceSetupSpec`, `PersistenceBeforeActorsSpec` and `StreamShutdownTaskSpec` (added later) stay in `src/Njord.Tests/Configuration/` (host types)
- [x] 4.4 Add `InternalsVisibleTo Njord.Core.Tests` to `src/Njord.Core/Njord.Core.csproj`
- [x] 4.5 Validate: build 0 warnings; sum = 825; commit

## 5. Njord.Egress.Tests (5 files, 27 tests)

- [x] 5.1 Create `src/Njord.Egress.Tests/Njord.Egress.Tests.csproj` (TestKit packages; references `Njord.Egress`, `Njord.Pipeline`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [x] 5.2 `git mv` the 5 `.cs` files of `src/Njord.Tests/Egress/` (including `EgressActorShutdownSpec`) (not `Snapshots/`, see design Risks) to `src/Njord.Egress.Tests/`; `ModelStateActorSpec` switches to `Njord.Tests.Shared.FailingRefProvider`; drop the `Njord.Tests.Actors` using
- [x] 5.3 Add `InternalsVisibleTo Njord.Egress.Tests` to `src/Njord.Egress/Njord.Egress.csproj` (and Pipeline if needed)
- [x] 5.4 Validate: build 0 warnings; sum = 825; commit

## 6. Njord.Grpc.Tests (12 files)

- [x] 6.1 Create `src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` (TestKit packages; references `Njord.Grpc`, `Njord.Egress`, `Njord.Pipeline`, `Njord.Persistence`, `Njord.Tests.Shared`); add to `src/Njord.slnx`
- [x] 6.2 `git mv src/Njord.Tests/Grpc/*.cs src/Njord.Grpc.Tests/` (12, including `TestServerCallContext.cs`); `GrpcSnapshotConsumerTerminatedSpec` and `WeatherGrpcServiceSpec` use the Shared `FailingRefProvider`
- [x] 6.3 Add `InternalsVisibleTo Njord.Grpc.Tests` to `src/Njord.Grpc/Njord.Grpc.csproj` (and Egress/Pipeline/Persistence if needed); decide the fate of the unused `Grpc.Net.Client` reference in `src/Njord.Tests.Shared` (dropped: the build proved it unused); Grpc.Tests also needs `Akka.Persistence.TestKit(.Xunit)` (recovery specs)
- [x] 6.4 Validate: build 0 warnings; sum = 825; commit

## 7. Njord.Pipeline.Tests (18 files + 1 snapshot, 111 tests)

- [x] 7.1 Create `src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` (TestKit packages, `Verify.XunitV3`; references `Njord.Pipeline`, `Njord.Ingest`, `Njord.Persistence`, `Njord.Tests.Shared`); add `ModuleInitializer.cs`; add to `src/Njord.slnx`
- [x] 7.2 `git mv` all `src/Njord.Tests/Pipeline/*` except `PollPipelineSpec.cs` to `src/Njord.Pipeline.Tests/` (17, including `StopwatchGap.cs`), plus `src/Njord.Tests/Persistence/SchedulerDtoSerializationSpec.cs` and its `.verified.txt` (1 + 1); `SchedulerActorRefFailureSpec` uses the Shared `FailingRefProvider`
- [x] 7.3 `PollPipelineSpec.cs` stays in `src/Njord.Tests/Pipeline/` (uses host `Njord.Mqtt`)
- [x] 7.4 Add `InternalsVisibleTo Njord.Pipeline.Tests` to `src/Njord.Pipeline/Njord.Pipeline.csproj` (and Persistence/Ingest if needed)
- [x] 7.5 Validate: build 0 warnings; no `*.received.*`; sum = 825; run the project 3 times and compare flakes with task 0.2; commit

## 8. Njord.Architecture.Tests (5 files, 25 tests)

- [x] 8.1 Spike: `src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` (Exe, `TngTech.ArchUnitNET.xUnitV3`, `xunit.v3.mtp-v2`, CodeCoverage) referencing `Njord` and all other test projects; confirm the project builds and runs without double test execution; if not, switch to the fallback in design Decision 7
- [x] 8.2 `git mv src/Njord.Tests/Architecture/*.cs src/Njord.Architecture.Tests/`; replace `NjordArchitecture.TestsAssembly` by `TestAssemblies` (every `Njord.*Tests.dll` plus Shared next to the binary, used by the ArchUnit loader, `TestTypes`, and `DisabledTestArchitectureSpec`); no rule is changed
- [x] 8.3 Add to `src/Njord.slnx`; remove ArchUnit and now-unused references from `src/Njord.Tests/Njord.Tests.csproj`
- [x] 8.4 Validate: build 0 warnings; the 25 architecture tests pass and the convention rule still sees every test class; sum = 825; commit

## 9. Host project cleanup

- [x] 9.1 In `src/Njord.Tests/Njord.Tests.csproj` remove unused `PackageReference`/`ProjectReference` items (check by building); keep `Njord`, Verify, `Mvc.Testing`, TestKit packages
- [x] 9.2 Remove `InternalsVisibleTo Njord.Tests` from any production csproj no leftover test needs (try removal, keep if the build fails)
- [x] 9.3 Confirm the 6 remaining `src/Njord.Tests` areas (Mqtt, Enrichment, Health, Configuration, Ingest, Sensors) plus `Pipeline/PollPipelineSpec.cs` and `Actors/FailingRefProvider.cs` are all that is left
- [x] 9.4 Validate: build 0 warnings; sum = 825; commit

## 10. Docs

- [x] 10.1 `AGENTS.md` Solution structure: list the 8 test projects and Shared; Architecture guardrails line `src/Njord.Architecture.Tests/`; Build & test: one `dotnet run --project` line per test project, the `-class` form, the solution-wide `dotnet test --solution Njord.slnx` and the per-project loop with the summed 825, a note that host tests (Mqtt/Enrichment) live in `Njord.Tests` until `extract-enrichment-mqtt-projects`, and the load note from design Decision 9; update the `Njord.Tests` known-deviation path (`Domain/Analysis/ConsensusComputerSpec.cs` becomes `Njord.Domain.Tests/Analysis/...`, `Configuration/ModelCoverageRegistrySpec.cs` becomes `Njord.Core.Tests/Configuration/...`)
- [x] 10.2 `README.md` line 97 test command; `.claude/skills/njord-actor-spec/SKILL.md` (command and example paths for `BudgetTrackerActorSpec`, `OpsGrpcServiceSpec`, `ModelStateActorSpec`, DTO specs, `ModuleInitializer` path, description "in src/Njord.Tests"), `.claude/skills/njord-enrichment-feature/SKILL.md` line 29 and `.claude/skills/njord-persistent-actor/SKILL.md` line 77 (`src/Njord.Persistence.Tests/`)
- [x] 10.3 `grep -rn "Njord.Tests" AGENTS.md README.md .claude Dockerfile .github openspec/specs` shows no stale path; confirm the Dockerfile copies no test project; do not touch `.github/` (main specs under openspec/specs still say `Njord.Tests` in requirement text; left unchanged, specs are not synced)
- [x] 10.4 Update `extract-enrichment-mqtt-projects` task paths for `Architecture` and Core configuration specs (`src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`, `src/Njord.Core.Tests/Configuration/EnrichmentOptionsSpec.cs`) if it has not landed yet
- [x] 10.5 Commit `docs: document per-library test projects`

## 11. Final validation

- [x] 11.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings
- [x] 11.2 Run every test project with `dotnet run --project <name>/<name>.csproj` (Domain, Persistence, Core, Egress, Grpc, Pipeline, Architecture, Njord.Tests) and `dotnet test --solution Njord.slnx`; summed total = 825 and no failures
- [x] 11.3 Repeat the full run 3 times; compare flakes and wall-clock with task 0.1/0.2; report the numbers to the user together with the CI confirmation from task 0.4
- [x] 11.4 `git status` shows no `*.received.*`, no changed `*.verified.*` content (`git diff -M --stat` only renames); `dotnet slopwatch` from the repo root passes
