## Context

See proposal.md. Reference pattern: `/home/st0o0/GIT/FunkArr` has one `FunkArr.<Lib>.Tests` per library, a `FunkArr.Tests.Shared`, and `FunkArr.Architecture.Tests` referencing only the host; AGENTS.md lists `dotnet run --project src/<Lib>.Tests/<Lib>.Tests.csproj` per project.

Facts read from the current tree (tests counted as `[Fact]`/`[Theory]` methods; theory rows bring the total to the 815 baseline):

| Test folder (src/Njord.Tests) | Files | Methods | Exercises | Target |
|---|---|---|---|---|
| `Domain/{Weather,Sensors,Analysis}` | 27 | 279 | `Njord.Domain` | Domain.Tests |
| `Persistence` | 5 (+6 `.verified.txt`) | 19 | `Njord.Persistence` DTOs; `SchedulerDtoSerializationSpec` also uses `Njord.Pipeline` state | Persistence.Tests (4), Scheduler spec to Pipeline.Tests |
| `Configuration` (10 of 14) | 10 | ~50 | `Njord.Core/Configuration` options, validators, budget | Core.Tests |
| `Configuration` (4 of 14) | 4 | ~22 | host: `NjordServiceSetup`, `NjordActorSystemSetup`, `ActorKeyRegistrationSpec`, `PersistenceBeforeActorsSpec` | stays in Njord.Tests |
| `Diagnostics` | 6 | 22 | `Njord.Core/Diagnostics` | Core.Tests |
| `Actors/StreamConsumerActorSpec` | 1 | 5 | `Njord.Core/Actors` | Core.Tests |
| `Actors/FailingRefProvider.cs` | 1 | 0 | helper, 11 users across 6 areas; has host-only `RequestMqttSink`/`SubscribeInbound` arms | Shared (+ host copy) |
| `Egress` | 4 | 25 | `Njord.Egress` | Egress.Tests |
| `Grpc` | 12 (incl. `TestServerCallContext` helper) | 74 | `Njord.Grpc` | Grpc.Tests |
| `Pipeline` | 18 (incl. `StopwatchGap` helper) | 106 | `Njord.Pipeline`; `PollPipelineSpec` uses host `Njord.Mqtt` (`MqttMessage`, `TopicScheme`) | Pipeline.Tests (17), `PollPipelineSpec` stays in Njord.Tests |
| `Ingest` | 1 | 15 | `Njord.Ingest` | stays |
| `Sensors` | 1 | 6 | `Njord.Sensors` | stays |
| `Mqtt` | 6 | 82 | host | stays |
| `Enrichment` (+`Features`) | 10 | 49 | host | stays |
| `Health` | 3 | 12 | host (`HealthEndpointSpec` uses `WebApplicationFactory<Program>`) | stays |
| `Architecture` | 4 | 17 | all assemblies | Architecture.Tests |
| `ModuleInitializer.cs`, `xunit.runner.json` | 2 | - | Verify/DiffEngine, runner config | see Decisions 5, 6 |

`Njord.Messages` has no test folder; its types are exercised through the actor tests of their producers/consumers. Resulting file counts: Domain 27, Core 17, Pipeline 18, Grpc 12, Persistence 4, Egress 4, Architecture 4, host Njord.Tests 27 spec/helper files (Mqtt 6, Enrichment 10, Health 3, Configuration 4, Ingest 1, Sensors 1, `PollPipelineSpec`, host `FailingRefProvider`) plus `ModuleInitializer.cs` and one orphan snapshot (see Risks). Sum 114 `.cs` files, identical to today (the Shared `FailingRefProvider` is one new file).

`Njord.Tests.Shared` (4 files, 52 lines, references only `Njord.Core`): `TestPersistenceConfig` used by 15 files (Pipeline 10, Grpc 3, Enrichment 1, Configuration 1), `TestTimefactorConfig` by 27 files (Pipeline 11, Grpc 5, Mqtt 3, Egress 2, Enrichment 2, Actors 1, Sensors 1), `FakeOpenMeteoClient` by 2 (both Pipeline), `FixtureReader` + `Fixtures/*.json` by 1 (`OpenMeteoClientSpec`, stays in Njord.Tests).

`InternalsVisibleTo Njord.Tests` exists on all 9 libraries and the host (set in each csproj). `src/Njord.Tests/xunit.runner.json`: `parallelizeAssembly=false`, `parallelizeTestCollections=true`, `maxParallelThreads=4`. All specs use `[Fact(Timeout = 5000)]`; actor specs use `akka.test.timefactor = 3`.

## Goals / Non-Goals

**Goals:** each library has a test project that references only that library, Shared and what its tests already use; build and test cost scales with the touched library; architecture specs live apart from the tests they police; the host-resident areas keep working unchanged.

**Non-Goals:** see proposal.md. Additionally: no merge of ArchUnit rules, no new rules.

## Decisions

### 1. Target layout: seven new projects, Njord.Tests shrinks to the host

`Njord.{Domain,Persistence,Core,Egress,Grpc,Pipeline,Architecture}.Tests` plus the remaining `Njord.Tests` and `Njord.Tests.Shared`. Naming and folder layout copy FunkArr (`src/<Name>/<Name>.csproj`, `OutputType Exe`, `xunit.v3.mtp-v2`, `Microsoft.Testing.Extensions.CodeCoverage`). Files keep their sub-folder and `Spec` names; namespaces change from `Njord.Tests.<Area>` to `Njord.<Lib>.Tests.<Area>` (sealed/`Spec` convention rule is unaffected).

### 2. Threshold: own project at 4+ files or a distinct dependency profile; otherwise stay

Ingest (1 file, 15 tests) and Sensors (1 file, 6 tests) stay in `Njord.Tests`: a project costs a csproj, a runner process and a solution entry, which is not worth 1 file each. Both are split off when they reach 4 files. Persistence and Egress are 4 files each but have distinct profiles (Verify snapshots with no TestKit; Egress needs Pipeline messages) and are kept. Alternatives rejected: one project per library including singletons (9 + 2 projects for 2 files), and merging Ingest+Sensors into one "adapters" project (no such production grouping).

### 3. Mqtt, Enrichment, Health and host Configuration stay in Njord.Tests

They exercise the host project, which `extract-enrichment-mqtt-projects` will split. Splitting them now would be redone. `Njord.Tests` therefore keeps its reference to `Njord`, `Mvc.Testing`, Verify and the TestKit packages; it loses ArchUnit and the Pipeline/Egress project references only if the build no longer needs them. `PollPipelineSpec` (2 tests) stays because it asserts on host `MqttMessage`/`TopicScheme`; moving it would need Pipeline.Tests to reference the host (breaking the narrow reference rule) or a test rewrite (Non-goal).

### 4. Narrow references

Reference sets (each also references `Njord.Tests.Shared` when it uses a shared helper):

| Project | ProjectReferences |
|---|---|
| Domain.Tests | `Njord.Domain` |
| Persistence.Tests | `Njord.Persistence`, `Njord.Domain` |
| Core.Tests | `Njord.Core`, Shared |
| Egress.Tests | `Njord.Egress`, `Njord.Pipeline` (ModelStateActorSpec uses pipeline messages/markers), Shared |
| Grpc.Tests | `Njord.Grpc`, `Njord.Egress`, `Njord.Pipeline`, `Njord.Persistence`, Shared |
| Pipeline.Tests | `Njord.Pipeline`, `Njord.Ingest` (FakeOpenMeteoClient), `Njord.Persistence`, Shared |
| Architecture.Tests | `Njord` (host; libraries come through its references), every other test project (Decision 7) |
| Njord.Tests | `Njord`, `Njord.Pipeline`, `Njord.Egress`, Shared |

Exact sets are confirmed per stage by the compiler: start from the table, delete what is unused. Packages move with the tests (`Akka.Hosting.TestKit`, `Akka.Persistence.TestKit(.Xunit)`, `Microsoft.Extensions.TimeProvider.Testing` for actor projects; `Verify.XunitV3` only in Persistence.Tests, Pipeline.Tests and Njord.Tests; `TngTech.ArchUnitNET.xUnitV3` only in Architecture.Tests; `Microsoft.AspNetCore.Mvc.Testing` only in Njord.Tests). Versions stay in `Directory.Packages.props`; add with `dotnet add package`, never by hand.

`Njord.Tests.Shared` stays one project (FunkArr does the same; 52 lines do not justify a split) and keeps referencing only `Njord.Core`. `FakeOpenMeteoClient` already compiles against Core's `IOpenMeteoClient`. `FailingRefProvider` moves to Shared as `Njord.Tests.Shared.FailingRefProvider` with the Egress/Pipeline arms only (its Messages types arrive through Core), because Grpc, Egress, Pipeline users need it and Shared must not reference the host. The host-only version (with `RequestMqttSink`, `MqttSinkFailed`, `SubscribeInbound` arms) stays in `Njord.Tests/Actors/` for Mqtt and Enrichment specs; the duplication (about 30 lines) disappears when `Njord.Mqtt` exists and can host the Mqtt arms. Unused `Grpc.Net.Client` in Shared is dropped only if the build proves it unused, otherwise moved to Grpc.Tests.

### 5. Verify snapshots and ModuleInitializer

Five Persistence snapshots move to Persistence.Tests and `SchedulerDtoSerializationSpec.DataChanged_dto_round_trips_through_json.verified.txt` to Pipeline.Tests, all by `git mv` together with their spec (Verify resolves the file next to the source file by naming convention, so the bytes and file names stay unchanged and no snapshot may be re-approved; any `.received.*` file is a failure). Verify settings: `DiffRunner.Disabled = true` is a 4-line `ModuleInitializer`; each Verify-using project (Persistence.Tests, Pipeline.Tests, Njord.Tests) gets its own copy. Rejected: putting it in Shared (a module initializer runs when its own assembly loads, which is not guaranteed for an assembly only referenced for helpers, and it would force Verify packages on Shared).

### 6. One runner config

Move `src/Njord.Tests/xunit.runner.json` to `src/xunit.runner.json`; every test csproj includes it via `<Content Include="..\xunit.runner.json" Link="xunit.runner.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`. Identical settings keep behavior identical and avoid 9 diverging copies.

### 7. ArchUnit assembly set

`Njord.Architecture.Tests` references the host plus all test projects (no ordering constraint with `<ReferenceOutputAssembly>` default). `NjordArchitecture` keeps loading the host and the libraries referenced by the host (`GetReferencedAssemblies`, `Njord.*` minus tests). The only rule over test code, `Test_classes_with_tests_are_sealed_and_suffixed_Spec`, needs all test assemblies, so the loader also takes the assemblies of one type per test project (a `typeof(...)` of a spec in each; no production `AssemblyMarker` is added). Alternative if exe-to-exe references misbehave (duplicate `xunit.runner.json`, runner entry point clash): load sibling test assemblies by path from the `bin` output, or run the convention rule inside each test project via a shared helper. Spike this in task 8.1 before committing to the table; the zone rules (Ingest/Egress independence, Domain independence, lateral library rules) only need production assemblies and are unaffected. A host `Program`-style marker is not needed; FunkArr's `AssemblyMarker` is not copied (Non-goal).

### 8. InternalsVisibleTo

Today all 10 production projects grant `Njord.Tests`. For each new project the build tells which libraries it needs: add `<InternalsVisibleTo Include="Njord.<Lib>.Tests" />` to exactly those production csproj files, in the same task that moves the files. `Njord.Tests` entries stay on host, Ingest, Sensors and whatever the leftover tests still use, and are removed from a library in group 9 only when a build without it succeeds. Architecture.Tests needs none (it uses public types only; confirm at build).

### 9. Running tests, CI and parallelism

Per project: `dotnet run --project <Name>/<Name>.csproj [-- -class "<FQN>"]` from `src/`. Whole solution: either `dotnet test --solution Njord.slnx` (MTP, what the shared CI workflow runs) or a loop `Get-ChildItem Njord.*Tests -Filter *.csproj -Recurse | % { dotnet run --project $_.FullName }` in AGENTS.md, summing the totals against 815. `Njord.Tests.Shared` is not a test project (no `IsTestProject`/exe) and is skipped.

CI is not changed. The shared workflow takes `solution-file: Njord.slnx` and `dotnet test --solution` discovers all test projects in the slnx, so the 8 test projects are picked up with no edit. This must be confirmed with the user before applying (coverage collection per project, merged report, runtime, number of parallel processes), per the "discuss CI first" rule.

Load: xUnit v3 runs each project as its own process and MTP runs several test modules concurrently under `dotnet test --solution`. Today one process runs with 4 threads; after the split up to 8 processes with 4 threads each can contend for the CPU of the runner, with one `akka.test.timefactor = 3` and `Timeout = 5000` per test. The known load-sensitive actor tests (Pipeline scheduler/budget, Grpc, Egress, Enrichment) get slower under oversubscription, though per-process load drops because each process holds fewer tests and test discovery is smaller. Mitigations without changing tests: record per-project timings in task 0 and 11, run the full suite 3 times and compare flakes against the baseline, and fall back to the documented `--max-parallel-test-modules` limit if flakes appear (flag name to be confirmed on the installed SDK). A CI-side limit is a user decision.

### 10. Staging

One project per task group, smallest blast radius first: shared infra, Domain (pure, no TestKit), Persistence, Core, Egress, Grpc, Pipeline, Architecture, cleanup, docs. After each group: `dotnet build Njord.slnx` with 0 warnings and every test project run, with the summed count equal to 815 (the moved tests disappear from `Njord.Tests` exactly as they appear in the new project). Each group is one commit.

## Risks / Trade-offs

- [Test count drifts or a spec is silently dropped by a move] -> per-group baseline table (task 0.3) and summed count check after every group.
- [Snapshot mismatch after a move] -> `git mv` spec and `.verified.txt` together; any `.received.*` fails the group; never re-approve.
- [Exe-to-exe project references in Architecture.Tests fail or double-run] -> spike (task 8.1) with the path-load and per-project-helper fallbacks of Decision 7.
- [Load-sensitive tests flake with parallel assemblies] -> Decision 9 measurements and fallbacks; no test edits.
- [Missing `InternalsVisibleTo` breaks compile] -> compiler-driven, added per group; entries on production csproj are test-visibility only.
- [`PollPipelineSpec` and `FailingRefProvider` host copy are temporary leftovers] -> noted in AGENTS.md and removed by `extract-enrichment-mqtt-projects` follow-up.
- [Orphan snapshot `src/Njord.Tests/Egress/Snapshots/DiscoveryPayloadBuilderSpec.The_device_payload_matches_the_approved_snapshot.verified.txt` (no spec uses Verify in `Mqtt`/`Egress`)] -> stays untouched where it is (it must not travel with Egress.Tests); report it to the user as a deletion candidate, out of scope here.
- [`extract-enrichment-mqtt-projects` paths go stale] -> task 10.4 updates its paths in place, or it lands first and this change's tasks adapt.
- [Slower cold restore/build: 7 more projects] -> accepted; incremental builds get faster when only one library changes.

## Migration Plan

Per group: create csproj, add to `Njord.slnx` Tests folder, `git mv` files, adjust namespaces/usings, add `InternalsVisibleTo`, build, run all test projects, commit. Rollback is reverting the group commit; no data or deployed artifact is affected. The Dockerfile does not copy test projects (verify with a grep in task 10.3), so the image is unaffected.

## Open Questions

None blocking. The CI confirmation (Decision 9) is a user check, not a prerequisite for the code groups.
