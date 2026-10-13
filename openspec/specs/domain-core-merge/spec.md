## Purpose

Defines the placement of the Analysis types and their tests in Njord.Core and Njord.Core.Tests after merging them out of Njord.Domain, including the Newtonsoft.Json dependency they need.

## Requirements

### Requirement: Analysis types SHALL live in Njord.Core

All types previously in `Njord.Domain/Analysis/` SHALL reside in
`Njord.Core/Analysis/` under the namespace `Njord.Analysis`.

#### Scenario: Analysis types accessible from Core
- **WHEN** a feature library references `Njord.Core`
- **THEN** it can use `ConsensusComputer`, `AlertEvaluator`, `TrendAnalyzer`, `IndexScorer` and all Analysis types without a separate Domain reference

#### Scenario: Analysis namespace drops Domain segment
- **WHEN** inspecting `ConsensusComputer.cs` in `Njord.Core/Analysis/`
- **THEN** its namespace is `Njord.Analysis`

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
