## Context

`analysis-contracts-per-feature` plans `Njord.Alerts.Contracts`, `Njord.Derived.Contracts`, `Njord.Trends.Contracts`, `Njord.Indices.Contracts`. Constraint that forces the types below the feature libraries: `PipelineEvent` variants (`AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`) in `Njord.Messages` carry them, and Mqtt and Grpc read them while referencing only Core (lateral rule: feature libraries never reference each other). Folding the contracts into the feature libraries would create a cycle (feature -> Core -> Messages -> feature) or break consumers.

## Goals / Non-Goals

**Goals:** fewer assemblies, contracts discoverable next to the other message domains, same reference direction.
**Non-Goals:** moving analyzer logic, changing shapes, touching `Njord.Weather` or `Njord.Sensors`.

## Decisions

1. **Folders in `Njord.Messages`** (`Alerts/`, `Derived/`, `Trends/`, `Indices/`), because Messages is already the Akka-free contract project and sits below Core. Alternative rejected: one shared `Njord.Analysis.Contracts` project (still an extra assembly for the same content).
2. **Feature settings records** (for example `AlertSettings`) move with their folder, since `AnalysisSettings` in Messages already aggregates them.
3. **Layering stays**: Weather/Sensors <- Consensus <- Messages <- Core <- features. Messages continues to reference Consensus for `LocationConsensus`.
4. **Guard rule** in `Njord.Architecture.Tests`: Messages declares no type whose name ends in `Analyzer`, `Rule` or `Formulas`, so compute logic cannot drift in.
5. **Tests**: one `Njord.Messages.Tests` project for contract logic and enum conversion specs; it joins the run loop automatically.
6. **Sequencing**: do this after `analysis-contracts-per-feature` is archived, as a mechanical move (namespace change, `git mv`, project removal). If that change is still unfinished when this one is approved, replace its contracts-project steps by this design instead.

## Risks / Trade-offs

- Messages grows by about 40 files and now contains the per-feature vocabulary; mitigated by domain folders and the guard rule.
- A second wave of namespace edits (roughly the same files as the first); mitigated by doing it as one mechanical commit with golden masters as proof.

## Migration Plan

No operator impact. Steps: move files per feature with the build green after each, drop the contracts projects from solution, Dockerfile and CI, move specs to `Njord.Messages.Tests`, update architecture specs and docs, run all test projects, format and validate.
