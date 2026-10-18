## Why

Line coverage says what ran, not what is checked. The pure-logic projects (consensus statistics, alert rules, derived formulas, trend formulas, index scores, budget and parameter selection logic) carry the product's correctness, and a surviving mutant in a threshold comparison or a rounding step would change user-visible alerts without failing a test. njord has no mutation testing. Stryker.NET 5.0.0 (2026-09-11) added a Microsoft.Testing.Platform runner with per-test coverage analysis, which finally works with the xUnit v3 on MTP setup used here (earlier versions reported every mutant as surviving with xunit.v3).

## What Changes

- Add Stryker.NET as a local tool (`dotnet-tools.json`, version pinned) with one `stryker-config.json` per mutated project and a shared base config: MTP runner, `perTest` coverage analysis, JSON and HTML reports under `StrykerOutput/` (git-ignored).
- Mutate only deterministic, Akka-free libraries: `Njord.Weather`, `Njord.Consensus` (or `Njord.Compute` until the rename lands), `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`, and the pure parts of `Njord.Core` (`BudgetCalculator`, `ParameterSelection`, validators). Actors, streams, Grpc, Mqtt and the host are out of scope.
- Add a scheduled and manually dispatchable workflow that runs the mutation job per project in a matrix and uploads the reports as artifacts; it never gates pull requests.
- Record the baseline mutation score per project, set `thresholds` (high 80, low 60) and `break` only after the first baseline is reviewed, so the first run is informational.
- Close the surviving mutants that point at real test gaps in the highest-value libraries (alert rules and consensus statistics first) by adding assertions; equivalent mutants are excluded explicitly in the config with a reason.
- Document the workflow (`docs/development/mutation-testing.md`, `AGENTS.md` build and test section).

## Capabilities

### New Capabilities
- `mutation-testing`: scope, configuration, reporting and the non-gating policy for Stryker.NET runs.

### Modified Capabilities
<!-- none -->

## Impact

- New: `.config/dotnet-tools.json` entry, `src/stryker-config.json` base plus per-project overrides, `.github/workflows/mutation.yml`, docs page. Test files gain assertions where mutants survive. No production behaviour change.
- First task is a spike: confirm Stryker.NET 5.x works with `xunit.v3.mtp-v2` 4.0.2 and the repo's central package management on one small project (Sensors or Derived) before configuring the rest.
- Not breaking; commit types `test:`/`ci:`/`docs:`. Applies after `analysis-contracts-per-feature` (project names and test project moves) to avoid configuring projects that are about to be renamed.
