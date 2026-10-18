## Why

A feature such as alerts is spread over five places: logic in `Njord.Alerts`, its result types, enums and settings in `Njord.Compute/Contracts` and `Compute/Settings` (31 flat files shared with derived, trends and indices), options in `Njord.Core`, mappings in Grpc and Mqtt. `Njord.Compute` is a collection of unrelated results, not a library. The cause is the layering: `PipelineEvent` variants in `Njord.Messages` carry the result types, and Mqtt and Grpc read them while referencing only Core, so the types must sit at or below `Njord.Messages`. They cannot move into the feature libraries (cycle feature -> Core -> Messages -> feature, and no lateral references). `Njord.Messages` is already the Akka-free contract project, so the result types get domain folders there instead of a new assembly per feature.

## What Changes

- `Njord.Compute` becomes `Njord.Consensus`: it keeps what the features really share (consensus builder, statistics, consensus records `LocationConsensus`, `ConsensusSettings`, `ConsensusMethod`, and the query vocabulary `ConsensusQueries`, `HorizonWindow`, `HorizonHit`, `ScanDirection`).
- The result records, result enums and `*Settings` records of Alerts, Derived, Trends and Indices move into `Njord.Messages` folders `Alerts/`, `Derived/`, `Trends/`, `Indices/` (namespaces follow folders, e.g. `Njord.Messages.Alerts`), one type per file. No `Njord.<Feature>.Contracts` project is created.
- `WeatherCondition` and `WeatherCategory` move to `Njord.Weather` (domain vocabulary); `AnalysisSettings` moves to `Njord.Messages/Config` next to `AnalysisSettingsChanged`.
- Feature libraries keep only compute logic (analyzers, rules, formulas, scores) and DI extensions and reach the contracts through Core and Messages.
- Tests: `Njord.Compute.Tests` becomes `Njord.Consensus.Tests`; contract specs (`AlertTypeExtensions`, `SeverityThresholds`, enum and details specs) move to a new `Njord.Messages.Tests`.
- `LayerReferenceSpec`, `ZoneArchitectureSpec` (new guard: Messages declares no `*Analyzer`, `*Rule`, `*Formulas` types), `AGENTS.md` and the Dockerfile `COPY` lines are updated.

## Capabilities

### New Capabilities
<!-- covered by requirements in message-domain-separation -->

### Modified Capabilities
- `message-domain-separation`: analysis contract types live in domain folders of `Njord.Messages`; Messages references Consensus.
- `compute-library`: replaced by `Njord.Consensus` holding only shared consensus building blocks.
- `architecture-zone-enforcement`: layer direction and base library list use `Njord.Consensus`.
- `vertical-feature-libraries`: analysis libraries hold computation only; their contracts are in Messages.

## Impact

- Namespaces change from `Njord.Compute.*` to `Njord.Consensus` / `Njord.Messages.<Feature>` across about 150 files; project rename, solution, Dockerfile, CI paths and test projects change.
- No wire, MQTT or persistence change; golden masters byte-identical. Not breaking for operators; commit type `refactor:`.
- Replaces the abandoned `analysis-contracts-per-feature` (separate contracts projects). Depends on `snapshot-naming` (done) and is followed by `feature-owned-options`.
