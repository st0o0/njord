## REMOVED Requirements

### Requirement: Analysis types SHALL live in Njord.Core
**Reason**: The merge it describes was superseded: there is no `Njord.Core/Analysis/` folder or `Njord.Analysis` namespace, no `ConsensusComputer`, `AlertEvaluator` or `IndexScorer`, no `Njord.Core.Tests/Analysis/`, and `Njord.Core` has no Newtonsoft.Json reference. Shared analysis building blocks live in `Njord.Compute`; feature computation lives in `Njord.Alerts`, `Njord.Derived`, `Njord.Trends` and `Njord.Indices`, each with its own test project.
**Migration**: See `compute-library` and `vertical-feature-libraries`.

### Requirement: Analysis tests SHALL live in Core.Tests
**Reason**: The merge it describes was superseded: there is no `Njord.Core/Analysis/` folder or `Njord.Analysis` namespace, no `ConsensusComputer`, `AlertEvaluator` or `IndexScorer`, no `Njord.Core.Tests/Analysis/`, and `Njord.Core` has no Newtonsoft.Json reference. Shared analysis building blocks live in `Njord.Compute`; feature computation lives in `Njord.Alerts`, `Njord.Derived`, `Njord.Trends` and `Njord.Indices`, each with its own test project.
**Migration**: See `compute-library` and `vertical-feature-libraries`.

### Requirement: Core SHALL depend on Newtonsoft.Json
**Reason**: The merge it describes was superseded: there is no `Njord.Core/Analysis/` folder or `Njord.Analysis` namespace, no `ConsensusComputer`, `AlertEvaluator` or `IndexScorer`, no `Njord.Core.Tests/Analysis/`, and `Njord.Core` has no Newtonsoft.Json reference. Shared analysis building blocks live in `Njord.Compute`; feature computation lives in `Njord.Alerts`, `Njord.Derived`, `Njord.Trends` and `Njord.Indices`, each with its own test project.
**Migration**: See `compute-library` and `vertical-feature-libraries`.
