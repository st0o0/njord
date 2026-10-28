# Mutation Testing

Line coverage says a line ran, not that a test would notice if it were wrong. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) changes the production code (`>` to `>=`, `+` to `-`, a removed statement) and re-runs the tests that cover the changed line; a mutant that no test kills **survived**, and points at an assertion that is too weak.

## Targets

The targets are listed in `src/mutation-targets.txt`, one project per line; each is mutated with its `<Project>.Tests` project.

| Project | Why |
|---------|-----|
| `Njord.Features` | Primary target: the feature rules are pure logic with exact-value tests |
| `Njord.Core` | Domain logic and validation |
| `Njord.Messages` | Small result records |
| `Njord.Budget` | Budget calculations |
| `Njord.Grpc` | Mapping logic |
| `Njord.OpenMeteo` | Client logic and mapping |

Not targets:

- `Njord.IntegrationTests`: slow, whole host.
- `Njord.Architecture.Tests`: ArchUnit rules, no production logic.
- `Njord.Persistence`: extend-only records, nothing to mutate.
- `Njord.Pipeline`, `Njord.Polling`: Akka streams and actors, timing-sensitive tests make mutation runs flaky.

To add a target, append the project name to `src/mutation-targets.txt` (`#` starts a comment). The script and the workflow both read it.

## Run

```bash
bash scripts/run-mutation.sh                          # all targets, in the .NET SDK container (needs Docker)
bash scripts/run-mutation.sh Njord.Features           # a single target
bash scripts/run-mutation.sh Njord.Features --local   # directly, where the SDK's MSBuild resolves (CI, Linux/macOS)
```

On Windows the tool picks Visual Studio's `MSBuild.exe`, which cannot resolve the .NET SDK, so run it through the container. The script exits non-zero when any target fails its threshold and lists the failed projects. Reports (`html`, `json`, `markdown`) are written to `src/<Project>.Tests/StrykerOutput/` (git-ignored); open the HTML report to see each surviving mutant in its line.

## CI

`.github/workflows/mutation.yml` runs weekly (Sunday 03:00 UTC) and on manual dispatch (optionally for one project). A discover job reads `src/mutation-targets.txt` and builds the matrix, so one job per target runs. Each job publishes its score in the job summary and uploads the reports as an artifact. It never runs on pull requests and is not a required check.

## Thresholds

Configuration: `src/stryker-config.json` (MTP runner; the tool version is pinned in `.config/dotnet-tools.json`).

- `high` 80: the target score.
- `low` 60: below this the score is flagged as a warning.
- `break` 60: a project under 60 % fails its run (and its weekly job).

## Exclusions

Not mutated (`mutate` in `src/stryker-config.json`):

- `*Options.cs` and `*OptionsValidator.cs`: configuration shape and cross-field rules checked at startup.
- `ServiceCollectionExtensions.cs` and `*SetupContainer.cs`: DI and host wiring.
- `obj/`: generated code.

Mutators: `string` mutations are ignored (log and wire text, no behavior).

Stryker does not score mutants that fail to compile, so those do not count.

### Equivalent mutants

Mutants that survive because the mutation does not change observable behavior are accepted, not fixed by weakening production code. Examples from the alert rules:

- `HeatRule`: `??` operand swap on the temperature fallback; `found = true`; `peak > MinValue` (always true once an extreme exists).
- `StormRule`: `peak > MinValue`, same reason.
- `PressureDropRule`: `j < i` to `j <= i` (a pair of span 0 fails the `>= 3` check).
- `SnowRule`: `> 0` to `>= 0` (a total of 0 means confidence 0, severity None either way).
- `VisibilityRule`: `totalHorizons > 0` (the Extreme early return guarantees a hit); `hoursBelow >= 0` (0 / N = 0).
