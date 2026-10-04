## ADDED Requirements

### Requirement: Analysis types SHALL live in Njord.Core

All types previously in `Njord.Domain/Analysis/` SHALL reside in
`Njord.Core/Analysis/` under the namespace `Njord.Analysis`.

#### Scenario: Analysis types accessible from Core
- **WHEN** a feature library references `Njord.Core`
- **THEN** it can use `ConsensusComputer`, `AlertEvaluator`, `TrendAnalyzer`, `IndexScorer` and all Analysis types without a separate Domain reference

#### Scenario: Analysis namespace drops Domain segment
- **WHEN** inspecting `ConsensusComputer.cs` in `Njord.Core/Analysis/`
- **THEN** its namespace is `Njord.Analysis`

### Requirement: Options types SHALL live in Njord.Core/Configuration

All types previously in `Njord.Domain/Options/` SHALL reside in
`Njord.Core/Configuration/`. They already use `Njord.Configuration` namespace.

#### Scenario: Options types merge cleanly
- **WHEN** inspecting `LocationOptions.cs` in `Njord.Core/Configuration/`
- **THEN** its namespace is `Njord.Configuration` (unchanged)

### Requirement: Domain SHALL retain only Weather and Sensors

`Njord.Domain` SHALL contain only `Weather/` and `Sensors/` folders — the pure
record types referenced by `Njord.Messages`.

#### Scenario: Domain contains no Analysis or Options
- **WHEN** listing files in `Njord.Domain/`
- **THEN** only `Weather/` and `Sensors/` subdirectories exist

### Requirement: Analysis tests SHALL live in Core.Tests

Test specs for Analysis computations SHALL reside in `Njord.Core.Tests/Analysis/`.

#### Scenario: Core.Tests runs Analysis specs
- **WHEN** running `Njord.Core.Tests`
- **THEN** it executes Analysis specs (ConsensusComputerSpec, AlertEvaluatorSpec, etc.) alongside existing Core tests

### Requirement: Core SHALL depend on Newtonsoft.Json

`Njord.Core` SHALL have a PackageReference to `Newtonsoft.Json` for the
`[JsonProperty]` attributes on Analysis result types.

#### Scenario: Core builds with JsonProperty attributes
- **WHEN** building `Njord.Core`
- **THEN** the `[JsonProperty]` attributes on Analysis types resolve correctly
