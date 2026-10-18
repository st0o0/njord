## ADDED Requirements

### Requirement: Analysis contracts live in domain folders of Njord.Messages
The result records, result enums and per-feature settings records of the Alerts, Derived, Trends and Indices features SHALL be declared in `Njord.Messages` under `Alerts/`, `Derived/`, `Trends/` and `Indices/` with namespaces that follow the folders. No separate contracts assembly SHALL exist per feature. `Njord.Messages` SHALL contain no analyzer, rule, formula or score logic and no Akka reference; the feature libraries SHALL reach the contracts through `Njord.Core` and below.

#### Scenario: Contract type location
- **WHEN** `AlertResult` is inspected
- **THEN** its namespace is `Njord.Messages.Alerts` and its assembly is `Njord.Messages`

#### Scenario: No per-feature contracts assembly
- **WHEN** the solution is inspected
- **THEN** no project named `Njord.<Feature>.Contracts` exists

#### Scenario: Messages stays logic-free
- **WHEN** the types of `Njord.Messages` are scanned
- **THEN** none is named `*Analyzer`, `*Rule` or `*Formulas` and the assembly references no Akka package
