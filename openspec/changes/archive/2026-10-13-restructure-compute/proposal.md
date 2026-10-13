## Why

The analysis code is organised inconsistently and in oversized classes:

- Alerts has a 1040-line static `AlertEvaluator` with 14 `Evaluate*` methods in
  `Njord.Alerts`, reached via `InternalsVisibleTo` from Compute. Derived, Trends and Indices
  keep all their logic in `Njord.Compute`, and their feature libraries are 26–52-line
  wrappers.
- Each feature is split into three layers with arbitrary names (Computer, ResultComputer,
  Calculator, Scorer, Factory, Evaluator, Analyzer). `IndexComputer` is a 387-line loop
  that builds every score.
- The same consensus lookups (`FindParam`, five copies; "max within N hours", "agreement
  above x", "models at horizon") are re-implemented per feature.

The `vertical-feature-libraries` spec and AGENTS.md already say each feature library owns
its compute logic. The code drifted away from that. This is change 3 of 4 and builds on
the typed models (`typed-analysis-models`) and settings records
(`split-options-per-feature`).

## What Changes

- `Njord.Compute` keeps only what is shared:
  - consensus building and statistics (`ConsensusBuilder`, `ConsensusStatistics`),
  - a query vocabulary on consensus data (find a parameter, restrict to a horizon
    window, find the first/extreme qualifying value, agreement and model counts),
  - the shared analysis contracts (result records, settings records, `SeverityThresholds`).
- Each feature library owns its computation:
  - `Njord.Alerts`: one `IAlertRule` class per alert type (14 rules).
  - `Njord.Indices`: one `IIndexScore` class per index kind, plus day slicing and the
    frost/VPD utility computations.
  - `Njord.Derived` and `Njord.Trends`: `DerivedFormulas`/`TrendFormulas` for pure math
    and their analyzer for assembling results.
- One naming scheme:
  - `XxxAnalyzer` is the feature entry point,
  - `XxxRule`/`XxxScore` is a single rule or score,
  - `XxxFormulas` holds pure static math.
  - The names Computer, ResultComputer, Calculator, Scorer, Evaluator and Factory disappear.
- Rules and scores are stateless and receive the current settings per call, so hot
  reload from change 2 keeps working.
- Remove `InternalsVisibleTo("Njord.Alerts")` from Compute.
- Update the stale `vertical-feature-libraries` spec: no `History` feature, no per-feature
  input interfaces (AGENTS.md already rejects them), result types live in Compute.

## Capabilities

### New Capabilities
- `analysis-rules`: alert rules and index scores are independent, individually testable
  units with a fixed output order.

### Modified Capabilities
- `compute-library`: Compute is consensus + query vocabulary + shared contracts. The
  shared-helper requirement becomes the query vocabulary.
- `vertical-feature-libraries`: feature libraries own their computation. The input
  interface and History requirements are removed, and names follow the new scheme.

## Non-goals

- No change to computed values, MQTT payloads or gRPC responses. The golden masters from
  `typed-analysis-models` must stay green without re-approval.
- No new alert types, indices or analysis features.
- No change to project references beyond removing `InternalsVisibleTo`. No projects are
  added or removed.
- No change to polling; API budget impact: 0 additional requests/month.

## Impact

- `src/Njord.Compute/Analysis/*` shrinks to consensus, querying and contracts.
- `src/Njord.Alerts`, `src/Njord.Derived`, `src/Njord.Trends`, `src/Njord.Indices` gain
  their logic, and their DI extensions register the rules and scores.
- `src/Njord.Pipeline/ForecastPipeline.cs` only sees the analyzers (signatures from change
  2), so it is barely touched.
- Specs move from `src/Njord.Compute.Tests/Analysis/` to the feature test projects.
  `src/Njord.Architecture.Tests` gains naming and placement rules.
