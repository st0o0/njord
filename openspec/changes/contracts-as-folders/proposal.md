## Why

`analysis-contracts-per-feature` gives each analysis feature its own `Njord.<Feature>.Contracts` project (4 new projects, 4 new test projects, Dockerfile and CI lines). Each of those projects holds 8 to 12 small records and enums, which is more structure than the content justifies. The reason they cannot simply live inside the feature libraries is real: `PipelineEvent` variants in `Njord.Messages` carry these types, and Mqtt and Grpc consume them while referencing only Core, so the contract types must sit at or below `Njord.Messages`. A folder in an existing foundation project achieves that without new assemblies.

## What Changes

- Replace the four `Njord.<Feature>.Contracts` projects by folders inside `Njord.Messages`: `Alerts/`, `Derived/`, `Trends/`, `Indices/` next to the existing `Budget/`, `Config/`, `Forecast/`, `Pipeline/`, `Sensors/` (namespaces follow folders, e.g. `Njord.Messages.Alerts`). The feature settings records move with their feature folder.
- Feature libraries keep only compute logic (analyzers, rules, formulas, scores) and reference the contracts through Core and Messages as they do today.
- `Njord.Consensus` (the rename of `Njord.Compute`) keeps `ConsensusBuilder`, statistics and querying; consensus result records remain there or move to `Messages/Consensus/` if Messages must name them (decided at move time, recorded in the apply report).
- Contract-level specs (`AlertType`, `SeverityThresholds`, enum conversions) move to `Njord.Messages.Tests`, a new test project replacing the four planned contracts test projects.
- Architecture rules: `LayerReferenceSpec` drops the four contracts projects again; a rule keeps `Njord.Messages` free of analyzers (types ending `Analyzer`, `Rule`, `Formulas`) and of Akka.
- Dockerfile, CI, solution file and docs lose the four contracts entries.

Not breaking: no wire, config or behavior change.

## Capabilities

### New Capabilities
<!-- covered by requirements in message-domain-separation -->

### Modified Capabilities
- `message-domain-separation`: analysis contract types live in domain folders of `Njord.Messages`, not in separate contracts projects.

## Impact

- Applies after `analysis-contracts-per-feature` is implemented and archived (it removes what that change adds); alternatively it can replace the remaining steps of that change if it is still unfinished.
- Touches namespaces in the files that use the moved types (mappers, analyzers, pipeline, tests), the solution, `Dockerfile`, CI and `AGENTS.md`.
