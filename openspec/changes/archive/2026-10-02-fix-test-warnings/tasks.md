## 1. Baseline

- [x] 1.1 From `src/`, run `dotnet build Njord.slnx` and list every `xUnit1069` and `IDE1006` site (file:line); note the total (expected 58+3, 60+3 if `akka-failure-hygiene` has landed)
- [x] 1.2 Re-grep `src/Njord.Tests` for `!.` (excluding `JsonNode` indexers) to get the current count and top files for task 5.1

## 2. Pure synchronous specs: drop the unenforceable timeout

- [x] 2.1 `src/Njord.Tests/Pipeline/BudgetTrackerStateSpec.cs` and `Pipeline/SchedulerStateSpec.cs`: remove `Timeout = 5000` from the `[Fact]` attributes
- [x] 2.2 `src/Njord.Tests/Grpc/EnrichmentSnapshotStateSpec.cs`, `Grpc/ForecastSnapshotStateSpec.cs`, `Enrichment/ForecastHistoryStateSpec.cs`: remove `Timeout = 5000`
- [x] 2.3 `src/Njord.Tests/Architecture/ZoneArchitectureSpec.cs` and `ConventionArchitectureSpec.cs`: remove `Timeout = ArchitectureTimeoutMs` and the now-unused constant and comment

## 3. Async specs that keep a timeout

- [x] 3.1 `src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs` (`StreamForecasts_throws_unavailable_when_egress_source_request_fails`, `StreamEnrichments_throws_unavailable_when_egress_source_request_fails`): reference `TestContext.Current.CancellationToken` in the awaited calls (or pass it into `TestServerCallContext.Create(...)` if it accepts one)
- [x] 3.2 Any other site left in the 1.1 list: pass `TestContext.Current.CancellationToken` where an API takes one; if a test cannot use a token, remove its `Timeout` per design Decision 1 (no `NoWarn`, no `#pragma`)

## 4. Naming

- [x] 4.1 `src/Njord.Tests/Architecture/NjordArchitecture.cs` lines 10-11: rename `_njordAssembly` -> `NjordAssembly`, `_testsAssembly` -> `TestsAssembly` (update all usages)
- [x] 4.2 `src/Njord.Tests/Architecture/ConventionArchitectureSpec.cs` line 14: rename `_testAttributes` -> `TestAttributes`

## 5. Guard and docs

- [x] 5.1 `src/.editorconfig`: add `dotnet_diagnostic.xUnit1069.severity = error`
- [x] 5.2 `AGENTS.md`: update the test-timeout convention (async/actor tests: `[Fact(Timeout = 5000)]` plus `TestContext.Current.CancellationToken`; pure synchronous specs omit it) and replace "about 20 existing `!.` uses" with the count and files from 1.2
- [x] 5.3 If `openspec/changes/zone-architecture-tests/` is still unarchived and mentions the 60 s timeout, align its `tasks.md` note; otherwise leave it

## 6. Validation

- [x] 6.1 From `src/`: `dotnet build Njord.slnx` shows 0 warnings with codes `xUnit1069` and `IDE1006` and 0 errors
- [x] 6.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj`: 779 of 780 pass; `PipelineConnectionSpec.Requests_have_no_serialization_gap_from_scheduler` fails (first request gap ~1.8-2.2 s vs <1 s) identically on unchanged HEAD, so it is pre-existing and not caused by this change (follow-up needed)
- [x] 6.3 `dotnet format Njord.slnx --verify-no-changes --no-restore` reports no violations
- [x] 6.4 `openspec validate fix-test-warnings` passes; `git diff --stat` shows only `src/Njord.Tests/**`, `src/.editorconfig`, `AGENTS.md` and this change's files (nothing under `src/Njord/`)
- [x] 6.5 Commit `test: fix xUnit1069 and IDE1006 warnings`; do not push; no attribution trailer (global rule)
