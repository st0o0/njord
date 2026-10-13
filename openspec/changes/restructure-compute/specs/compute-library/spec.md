## MODIFIED Requirements

### Requirement: Repeated horizon-scan and threshold-classification shapes are shared
Lookups that more than one feature performs on consensus data SHALL be provided once by Compute's query vocabulary: finding a parameter's series, restricting a series to a horizon window, finding the extreme or first qualifying value with its horizon and agreement, averaging agreement over a window or above a threshold, and counting contributing models at a horizon or over a window. Classification of a value against severity thresholds SHALL be provided once by the severity thresholds type. Feature code SHALL NOT re-implement these loops.

#### Scenario: New alert type reuses the shared scan and classification helpers
- **WHEN** a new alert rule is added
- **THEN** it expresses its scan with the query vocabulary and its classification with the severity thresholds, and does not introduce a new copy of either loop

#### Scenario: Existing alert results are unchanged
- **WHEN** the alert, index, derived and trend computations are migrated to the query vocabulary
- **THEN** the MQTT and gRPC golden masters remain identical without re-approval

## ADDED Requirements

### Requirement: Njord.Compute holds only shared analysis building blocks
The solution SHALL contain a `Njord.Compute` class library that holds what all analysis features share: consensus building and statistics, the query vocabulary over consensus data, and the shared analysis contracts (result records, settings records, severity thresholds). Feature-specific computation (alert rules, index scores, derived and trend formulas) SHALL NOT live in Compute. The library SHALL reference only `Njord.Weather` and `Njord.Sensors`: no Akka, no DI, no ASP.NET, no options dependencies, and no `InternalsVisibleTo` to any feature library.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** its only `ProjectReference`s SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Compute contains no Akka types
- **WHEN** `Njord.Compute` is built
- **THEN** it SHALL NOT reference any `Akka.*` package

#### Scenario: No feature logic in Compute
- **WHEN** `Njord.Compute` is inspected
- **THEN** it contains no alert rule, index score, derived formula or trend formula

#### Scenario: No friend assemblies
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** it contains no `InternalsVisibleTo` entry other than its own test project

## REMOVED Requirements

### Requirement: Njord.Compute is a pure computation library
**Reason**: Compute no longer holds all analysis computation; feature-specific logic moved into the feature libraries.
**Migration**: See the added requirement: Compute keeps consensus, the query vocabulary and shared contracts.
