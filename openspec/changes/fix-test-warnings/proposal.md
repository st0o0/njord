## Why

`dotnet build Njord.slnx` reports 58 distinct `xUnit1069` warnings ("test has a `Timeout` but does not reference `TestContext.Current.CancellationToken`") and 3 `IDE1006` naming warnings. Warning noise hides real regressions and blocks promoting these rules to errors, which the later analyzer work (`BannedApiAnalyzers`, scoped `WarningsAsErrors`) and the project split build on.

## What Changes

- Resolve every `xUnit1069` site under `src/Njord.Tests/`: the pure synchronous state specs (`Pipeline/BudgetTrackerStateSpec.cs`, `Pipeline/SchedulerStateSpec.cs`, `Grpc/EnrichmentSnapshotStateSpec.cs`, `Grpc/ForecastSnapshotStateSpec.cs`, `Enrichment/ForecastHistoryStateSpec.cs`), the synchronous architecture specs (`Architecture/ZoneArchitectureSpec.cs`, `Architecture/ConventionArchitectureSpec.cs`) and any async test that has a `Timeout` but never uses the token (for example the two `Stream*_throws_unavailable_*` tests in `Grpc/WeatherGrpcServiceSpec.cs`).
- Rename the three `_`-prefixed private static fields that violate `private_static_fields_must_be_pascal_case` (`Architecture/NjordArchitecture.cs` lines 10-11, `Architecture/ConventionArchitectureSpec.cs` line 14); the rule is not relaxed.
- Raise `dotnet_diagnostic.xUnit1069.severity = error` in `src/.editorconfig` so the warning cannot return.
- Refresh the stale `!.` deviation note in `AGENTS.md` (states "about 20", the current grep finds about 41 in `Njord.Tests`).
- Clarify the test-timeout convention in `AGENTS.md` (see design.md Decision 1).

## Capabilities

### New Capabilities

None. Test-only and docs/config change; `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- Files: ~10 test files under `src/Njord.Tests/`, `src/.editorconfig`, `AGENTS.md`. No production code under `src/Njord/` changes.
- API budget: none; no polling is added or altered (0 additional requests/month against the 300k free-tier limit).
- Ordering: the build warning list must be re-read after `akka-failure-hygiene` lands, because that change adds tests (the two gRPC `Unavailable` specs already add `xUnit1069` sites).

## Non-goals

- `BannedApiAnalyzers` / `BannedSymbols.txt`, `TreatWarningsAsErrors` or any other analyzer severity changes beyond `xUnit1069` (and `IDE1006` only if already effective).
- Changing production code or test behavior/assertions.
- The project split.
- Introducing `[Fact(Timeout = ...)]` where tests have none today.
