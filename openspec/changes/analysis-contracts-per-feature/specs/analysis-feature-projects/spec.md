## ADDED Requirements

### Requirement: Each analysis feature has a contracts project and a feature library
Alerts, derived values, trends and indices SHALL each be split into `Njord.<Feature>.Contracts` (result records, enums, settings record, pure helpers on those types; no Akka, no options, no logic beyond the types) and `Njord.<Feature>` (analyzer, rules or formulas, DI extension). A contracts project SHALL reference only `Njord.Weather`, and `Njord.Sensors` or `Njord.Consensus` when a contract needs them. It SHALL NOT reference another feature's contracts.

#### Scenario: Alerts contracts reference
- **WHEN** `Njord.Alerts.Contracts.csproj` is inspected
- **THEN** it references only `Njord.Weather` (and `Njord.Consensus` if needed) and no other Njord project

#### Scenario: Alert types live with the alerts contracts
- **WHEN** `AlertResult`, `AlertType`, `AlertSeverity`, `AlertDetails` and `AlertSettings` are located
- **THEN** all are declared in `Njord.Alerts.Contracts`

### Requirement: Consensus building blocks live in Njord.Consensus
`Njord.Consensus` SHALL hold the consensus builder, statistics, consensus records, consensus settings, `ConsensusMethod` and the query vocabulary on consensus data. It SHALL reference only `Njord.Weather` and `Njord.Sensors` and contain no Akka types. `Njord.Compute` SHALL no longer exist.

#### Scenario: Compute is gone
- **WHEN** the solution is inspected
- **THEN** there is no `Njord.Compute` project and no namespace `Njord.Compute`

#### Scenario: Analyzers use the consensus project
- **WHEN** an analyzer reads a location's consensus
- **THEN** it consumes `LocationConsensus` and `ConsensusQueries` from `Njord.Consensus`

### Requirement: Layer rules cover the contracts projects
`LayerReferenceSpec` SHALL enforce: Weather and Sensors reference no Njord project; `Njord.Consensus` and every contracts project reference only Weather, Sensors, `Njord.Consensus` or nothing else; Messages references Weather, Sensors, Consensus and the contracts projects and no Akka package; Core references those plus Persistence; feature libraries reference Core, their own contracts project and `Njord.Consensus`, never another feature or its contracts.

#### Scenario: Lateral contracts reference
- **WHEN** `Njord.Indices.Contracts` references `Njord.Alerts.Contracts`
- **THEN** the architecture spec fails

### Requirement: Behaviour and wire formats are unchanged
Moving types SHALL NOT change analysis results, gRPC messages, MQTT payloads or persisted data.

#### Scenario: Golden masters
- **WHEN** the full test suite runs after the move
- **THEN** all golden masters and wire-format specs are byte-identical
