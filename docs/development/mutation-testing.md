# Mutation Testing

Line coverage says a line ran, not that a test would notice if it were wrong. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) changes the production code (`>` to `>=`, `+` to `-`, a removed statement) and re-runs the tests that cover the changed line; a mutant that no test kills **survived**, and points at an assertion that is too weak.

It is used on the pure-logic projects only, where tests are fast and assert exact values:
`Njord.Weather`, `Njord.Consensus`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`, and `ParameterSelection`, `BudgetCalculator`, `NjordOptionsValidator`, `LegacyConfigKeys` in `Njord.Core`. Actor, gRPC and MQTT code is not a good target (timing, I/O).

## Run

```bash
bash scripts/run-mutation.sh Derived          # in the .NET SDK container (needs Docker)
bash scripts/run-mutation.sh Derived --local  # directly, where the SDK's MSBuild resolves (CI)
```

On Windows the tool picks Visual Studio's `MSBuild.exe`, which cannot resolve the .NET SDK, so run it through the container. A project takes one to three minutes. Reports (`html`, `json`, `markdown`) are written to `src/Njord.<Project>.Tests/StrykerOutput/` (git-ignored); open the HTML report to see each surviving mutant in its line.

Configuration: `src/stryker-config.json` (MTP runner, thresholds high 80 / low 60, options classes, validators and DI registration are not mutated), the tool version is pinned in `.config/dotnet-tools.json`. The weekly workflow `.github/workflows/mutation.yml` runs every project and publishes the scores in the job summary. It is not a required check; `break` stays at 0.

## Baseline

First run, before any test was added:

| Project | Mutation score |
|---------|----------------|
| Weather | 58.1 % |
| Consensus | 75.9 % |
| Alerts | 47.1 % |
| Derived | 39.0 % |
| Trends | 60.3 % |
| Indices | 73.2 % |
| Core (selected files) | 80.8 % |
