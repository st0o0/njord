## Context

`Njord.Compute` (51 files) holds `Consensus/` (10), `Contracts/` (31, covering all four features plus shared enums such as `TrendDirection`, `Stability`, `DewPointComfort`, `VpdCategory`, `WeatherCondition`), `Querying/` (3) and `Settings/` (7, including `AnalysisSettings`). `Njord.Messages` and `Njord.Core` reference it; the four feature libraries reach it through Core. Constraint: `PipelineEvent` variants (`AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`) in `Njord.Messages` carry the result types, and Mqtt and Grpc read them while referencing only Core (feature libraries never reference each other). Types inside the feature libraries would create a cycle (feature -> Core -> Messages -> feature) or break consumers.

## Goals / Non-Goals

**Goals:** feature vocabulary in discoverable domain folders, shared consensus code with an honest name, no new assemblies, same reference direction.
**Non-Goals:** moving options (`feature-owned-options`), changing analyzer logic or contract shapes, splitting Weather or Sensors.

## Decisions

1. **Folders in `Njord.Messages`** (`Alerts/`, `Derived/`, `Trends/`, `Indices/`). Alternatives rejected: a `Contracts` project per feature (four small assemblies and test projects for 8 to 12 types each), one shared `Njord.Analysis.Contracts` project (an extra assembly for the same content). Shared enums go to the feature that produces them: `AlertSeverity`, `AlertType`, `SeverityThresholds` to Alerts; `DewPointComfort`, `VpdCategory` to Derived; `TrendDirection`, `Stability` to Trends; `IndexKind`, `ScoreEnvelope`, `DayScoreSet`, `ResolvedPreferences` to Indices.
2. **`WeatherCondition`/`WeatherCategory` move to `Njord.Weather`.** They classify weather and are used by derived formulas and results, so they belong to the domain vocabulary.
3. **`AnalysisSettings`** moves to `Njord.Messages/Config` next to `AnalysisSettingsChanged`; the feature settings records move with their feature folder.
4. **`Njord.Consensus` replaces `Njord.Compute`** by rename of project, namespace and test project; `ScanDirection` and the query vocabulary stay with the data they query. Messages references Consensus for `LocationConsensus`.
5. **Guard rule** in `Njord.Architecture.Tests`: Messages declares no type whose name ends in `Analyzer`, `Rule` or `Formulas` and has no Akka reference, so compute logic cannot drift into the contract project.
6. **Tests follow types.** Contract specs move to `Njord.Messages.Tests` (picked up by the run loop automatically); consensus specs stay in the renamed `Njord.Consensus.Tests`.
7. **Move order**: (a) spec reconciliation and failing layer rules, (b) move one feature at a time (derived, trends, alerts, indices) with namespace changes and `using` updates, build green after each, (c) move `WeatherCondition`/`WeatherCategory` and `AnalysisSettings`, (d) rename Compute to Consensus, (e) update architecture specs, Dockerfile, CI, docs.
8. **Foreign edits**: six files carry the user's uncommitted style edits (`FogRule`, `HeavyRainRule`, `DerivedFormulas`, `DaySlicerSpec`, `IndexAnalyzerSpec`, `StopwatchGapSpec`); the apply step edits them minimally and stages only its own hunks.

## Risks / Trade-offs

- Large mechanical diff (about 150 files); mitigated by moving feature by feature with the build green after each and by the golden masters as byte-identical proof.
- Messages grows by about 40 files and carries the per-feature vocabulary; mitigated by domain folders and the guard rule.
- Ambiguous shared types (for example `WeatherCategory`) are decided at move time and recorded in the apply report.

## Migration Plan

No data or operator impact. Each step is a commit-ready state: build, all test projects, format, `openspec validate`.
