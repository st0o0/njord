## ADDED Requirements

### Requirement: Analysis feature libraries are independent and hold computation only
Each analysis feature (Alerts, Derived, Trends, Indices) SHALL be an independent project (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) that references only `Njord.Core` and below. Feature libraries SHALL NOT reference each other, the Host, or any consumer library. Each library SHALL contain its computation logic (rules, scores, formulas) and its DI extension. Result records and settings records SHALL live in the feature's folder of `Njord.Messages`, so consumers and messages can use them without referencing a feature library.

#### Scenario: Adding a new analysis feature
- **WHEN** a developer creates a new analysis feature
- **THEN** they create one new project with its compute logic and DI extension plus one folder in `Njord.Messages` with its result and settings records; no other feature library changes

#### Scenario: Analysis library references
- **WHEN** any analysis library project is inspected
- **THEN** it references only `Njord.Core` (and transitively Weather, Sensors, Messages, Consensus, Persistence), never another feature library, a consumer, or the Host

#### Scenario: Njord.Consensus retains only consensus types
- **WHEN** `Njord.Consensus` is inspected after restructuring
- **THEN** it contains only consensus building and statistics, the consensus records and settings, and the query vocabulary; no feature result or settings records

## REMOVED Requirements

### Requirement: Each analysis feature is a self-contained library
**Reason**: Result and settings records no longer live in `Njord.Compute`; they live in the feature's folder of `Njord.Messages`.
**Migration**: See "Analysis feature libraries are independent and hold computation only".
