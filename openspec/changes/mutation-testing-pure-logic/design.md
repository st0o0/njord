## Context

The pure-logic libraries have no Akka dependency, run in milliseconds, and are covered by `Spec` classes with exact-value assertions. That is the sweet spot for mutation testing: fast test runs, deterministic results, and high business value per surviving mutant. Stryker.NET 5.0.0 supports `-t mtp` / `test-runner: mtp` with `coverage-analysis: perTest` against xUnit v3 on MTP. The repo runs tests with `dotnet run` per executable test project and `dotnet test --solution` in CI.

## Goals / Non-Goals

**Goals:** a repeatable local and scheduled mutation run for the pure libraries; a recorded baseline; assertions added where mutants show real gaps.

**Non-Goals:** mutation gates on pull requests, mutating actor or I/O code, 100 % mutation score, replacing line coverage.

## Decisions

1. **Spike first.** The initial task proves Stryker 5.x with `xunit.v3.mtp-v2` 4.0.2 on `Njord.Derived` (36 tests): the run must report both killed and survived mutants (not "100 % survived"), and run time per project must stay under a few minutes. If the spike fails, the change stops and records the blocker; nothing else depends on it.
2. **Configuration layout.** A base `src/stryker-config.json` holds runner, coverage analysis, reporters, thresholds and exclusions common to all; each project has a small `stryker-config.json` next to its test project naming `project` and `test-projects`. A script `scripts/run-mutation.sh <project>` wraps the invocation so CI and developers use the same command.
3. **Scope by project, not by file.** Only the six libraries plus a `mutate` glob limiting Core to `Configuration/BudgetCalculator.cs`, `Configuration/ParameterSelection.cs` and the validators; generated code and `ServiceCollectionExtensions` are excluded.
4. **CI workflow.** `mutation.yml` runs weekly and on `workflow_dispatch`, uses a matrix over projects, restores the pinned tool, uploads `StrykerOutput/**/reports` and writes the score to the job summary. `timeout-minutes` per job; it does not use the external dotnet-ci reusable workflow because that lives in another repository.
5. **No break threshold initially.** Thresholds (`high` 80, `low` 60) only color the report; `break` stays 0 until each project's first baseline is reviewed. The baseline table goes into `docs/development/mutation-testing.md`.
6. **Gaps closed by tests, not by exclusion.** For alert rules and consensus statistics, survivors are analysed and closed with exact-value assertions (existing assertion conventions apply). Exclusions are for equivalent mutants only and need a reason.
7. **Sequencing.** After `analysis-contracts-per-feature`, because the Compute rename and the new contracts projects change project and test-project paths.

## Risks / Trade-offs

- Stryker 5.0.0 is young; mitigated by the spike, a pinned version and the non-gating policy.
- Mutation runs are slow on large libraries (Indices, Alerts); `perTest` coverage and per-project matrix jobs keep them bounded, and a `since` option can restrict PR-independent reruns later.
- Time spent on equivalent mutants; capped by only addressing alert rules and consensus statistics in this change.

## Migration Plan

No operator impact. Order: spike, tool pin and base config, per-project configs and runner script, workflow, baseline run and documentation, then the targeted test improvements.
