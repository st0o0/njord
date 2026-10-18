## Context

`Njord.Compute` (51 files) holds `Consensus/` (10), `Contracts/` (31, covering all four features plus shared enums such as `TrendDirection`, `Stability`, `DewPointComfort`, `VpdCategory`, `WeatherCondition`), `Querying/` (3) and `Settings/` (7, including `AnalysisSettings`). `Njord.Messages` and `Njord.Core` reference it; the four feature libraries reach it through Core. FunkArr's API/domain split (`FunkArr.ArrApi` contracts vs domain projects) is the model.

## Goals / Non-Goals

**Goals:** a feature owns its contract types; shared consensus code has an honest name; contracts are the only thing Messages and consumers need from a feature.

**Non-Goals:** moving options (`feature-owned-options`), changing analyzer logic, changing contract shapes, splitting Weather or Sensors.

## Decisions

1. **Contracts project per feature, not per layer.** Four small projects (about 8 to 12 files each) instead of one `Contracts` bucket; `Njord.Compute` disappears. The shared enums go to the feature that produces them: `AlertSeverity`, `AlertType` to Alerts; `DewPointComfort`, `VpdCategory` to Derived; `TrendDirection`, `Stability` to Trends; `IndexKind`, `ScoreEnvelope`, `DayScoreSet`, `ResolvedPreferences` to Indices.
2. **`WeatherCondition`/`WeatherCategory` move to `Njord.Weather`.** They classify weather, are used by `HorizonDerived` and derived formulas, and belong to the domain vocabulary, not to a feature.
3. **`AnalysisSettings`** (aggregates the five settings) moves to `Njord.Core/Configuration` because its only builders are Core/host factories; Messages' `AnalysisSettingsChanged` message needs it, so the record is placed in `Njord.Messages/Pipeline` next to that message. The feature settings records stay in their contracts projects.
4. **`Njord.Consensus` replaces `Njord.Compute`** by rename of project, namespace and test project; consensus queries stay with the data they query. This is a rename plus the removal of the foreign files.
5. **Move order** minimizes breakage: (a) create contracts projects empty and wire them into Messages and Core, (b) move one feature at a time (derived, trends, alerts, indices) with its namespace change and mapper `using` updates, (c) move WeatherCondition, (d) rename Compute to Consensus, (e) update architecture specs, Dockerfile, CI, docs, (f) delete the empty Compute.
6. **Tests follow types.** Specs for contract logic (`AlertTypeExtensions`, `SeverityThresholds`, `WeatherCondition`, enum conversion specs) move to a `<Feature>.Contracts.Tests` project only where such logic exists; otherwise they join the feature tests. Each new test project is picked up by the run loop automatically.
7. **Spec reconciliation first.** `compute-library` and `architecture-zone-enforcement` describe Compute by name; the first apply task writes their MODIFIED and REMOVED deltas against the final names so archive keeps the main specs true.

## Risks / Trade-offs

- Large mechanical diff (namespaces change in about 150 files); mitigated by moving feature by feature with the build green after each, and by the golden masters.
- More projects (4 added, 1 renamed) means more build nodes and Docker COPY lines; accepted for self-contained features.
- Feature contracts share small enums with `Njord.Weather` vocabulary in ambiguous cases (`WeatherCategory`); decision 2 settles the known ones, others are decided at move time and recorded in the apply report.

## Migration Plan

No data or operator impact. Each step is a commit-ready state: build, all test projects, format, `openspec validate`.
