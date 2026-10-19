# Mutation Testing

Line coverage says a line ran, not that a test would notice if it were wrong. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) changes the production code (`>` to `>=`, `+` to `-`, a removed statement) and re-runs the tests that cover the changed line; a mutant that no test kills **survived**, and points at an assertion that is too weak.

It is used on the pure-logic projects only, where tests are fast and assert exact values:
`Njord.Model`, `Njord.Alerts`, and `ParameterSelection`, `BudgetCalculator`, `NjordOptionsValidator` in `Njord.Core`. Actor, gRPC and MQTT code is not a good target (timing, I/O).

## Run

```bash
bash scripts/run-mutation.sh Alerts           # in the .NET SDK container (needs Docker)
bash scripts/run-mutation.sh Alerts --local   # directly, where the SDK's MSBuild resolves (CI)
```

On Windows the tool picks Visual Studio's `MSBuild.exe`, which cannot resolve the .NET SDK, so run it through the container. A project takes one to three minutes. Reports (`html`, `json`, `markdown`) are written to `src/Njord.<Project>.Tests/StrykerOutput/` (git-ignored); open the HTML report to see each surviving mutant in its line.

Configuration: `src/stryker-config.json` (MTP runner, thresholds high 80 / low 60, options classes, validators and DI registration are not mutated), the tool version is pinned in `.config/dotnet-tools.json`. The weekly workflow `.github/workflows/mutation.yml` runs every project and publishes the scores in the job summary. It is not a required check; `break` stays at 0.

## Baseline

First run, before any test was added:

| Project | Mutation score |
|---------|----------------|
| Alerts | 47.1 % |
| Core (selected files) | 80.8 % |

After closing surviving mutants in the alert rules:

| Project | Before | After |
|---------|--------|-------|
| Alerts | 47.1 % | 92.2 % |

Stryker does not score mutants that fail to compile, so the Fog, Humidity, Ice, Thunderstorm and TropicalNight rules (all mutants are compile errors) do not count; their specs still run. No `break` threshold is set for any project: the score is a trend to watch weekly, not a PR gate.

## Justified exclusions (equivalent mutants)

These survive because the mutation does not change observable behavior:

- `HeatRule`: `??` operand swap on the temperature fallback (only null-ness of both is tested); `found = true` (without a hit `Classify` returns None anyway); `peak > MinValue` (always true once an extreme exists).
- `StormRule`: `peak > MinValue`, same reason.
- `PressureDropRule`: `j < i` to `j <= i` (a pair of span 0 fails the `>= 3` check).
- `SnowRule`: `> 0` to `>= 0` (a total of 0 means confidence 0, severity None either way).
- `VisibilityRule`: `totalHorizons > 0` (the Extreme early return guarantees a hit); `hoursBelow >= 0` (0 / N = 0).
