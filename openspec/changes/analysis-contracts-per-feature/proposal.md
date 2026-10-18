## Why

A feature such as alerts is spread over five places: logic in `Njord.Alerts`, its result types, enums and settings in `Njord.Compute/Contracts` and `Compute/Settings` (31 flat files shared with derived, trends and indices), options in `Njord.Core`, mappings in Grpc and Mqtt. `Njord.Compute` is a collection of unrelated results, not a library. The cause is the layering: `Njord.Messages` carries the pipeline events, so every result type had to sit below it, and the only place below it that features share was Compute. FunkArr separates API and domain projects (`FunkArr.ArrApi` / `FunkArr.Download`); the same separation here makes each analysis feature a self-contained pair: a small contracts project below `Messages`, and the feature library above `Core`.

## What Changes

- `Njord.Compute` becomes `Njord.Consensus`: it keeps what the features really share (consensus builder, statistics, consensus records `LocationConsensus`, `ConsensusSettings`, `ConsensusMethod`, and the query vocabulary `ConsensusQueries`, `HorizonWindow`, `HorizonHit`).
- Four new contracts projects, each referencing only `Njord.Weather` (and `Njord.Sensors` / `Njord.Consensus` where needed): `Njord.Alerts.Contracts`, `Njord.Derived.Contracts`, `Njord.Trends.Contracts`, `Njord.Indices.Contracts`. Each holds the feature's result records, enums, thresholds helpers and its `*Settings` record, one type per file.
- `Njord.Messages` references the contracts projects (still no Akka); `Njord.Core` references them for settings assembly; `AnalysisSettings` stays in `Njord.Consensus` or moves to a small shared place decided in design.
- Feature libraries (`Njord.Alerts`, ...) reference their own contracts project plus `Njord.Core`; they do not reference each other's contracts (checked by the architecture specs).
- Consumers (`Njord.Grpc`, `Njord.Mqtt`, `Njord.Pipeline`) reference the contracts they map; the lateral rules keep features independent.
- Tests: `Njord.Compute.Tests` splits into `Njord.Consensus.Tests` and one contracts test project per contracts library only where contract logic exists (`AlertTypeExtensions`, `SeverityThresholds`, `WeatherCondition`); existing specs move with their types.
- `LayerReferenceSpec`, `ZoneArchitectureSpec`, `AGENTS.md` (solution structure, reference direction) and the Dockerfile `COPY` lines are updated.

## Capabilities

### New Capabilities
- `analysis-feature-projects`: each analysis feature is a contracts project plus a feature library; shared consensus building blocks live in `Njord.Consensus`.

### Modified Capabilities
<!-- compute-library and architecture-zone-enforcement requirements become partly false; the apply phase starts with a spec-reconciliation task that writes their MODIFIED/REMOVED deltas against the final project names -->

## Impact

- New projects and solution entries in `Njord.slnx`, `Dockerfile`, CI paths; namespaces change from `Njord.Compute.*` to `Njord.<Feature>.Contracts` / `Njord.Consensus` across about 150 files; test projects relocate.
- No wire, MQTT or persistence change; golden masters byte-identical. Not breaking for operators; commit type `refactor:`.
- Depends on `snapshot-naming` (it renames `ConsensusSnapshot` to `LocationConsensus`) and is followed by `feature-owned-options`.
