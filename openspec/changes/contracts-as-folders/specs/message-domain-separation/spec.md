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

## MODIFIED Requirements

### Requirement: No Akka.Streams types in shared messages

The Njord.Messages project SHALL NOT have a package reference to any Akka package (including Akka and Akka.Streams). Its only project references SHALL be `Njord.Weather`, `Njord.Sensors` and `Njord.Consensus`. No record in Njord.Messages SHALL reference `IActorRef`, `ISinkRef`, `ISourceRef` or any other Akka type. Records that carry such types SHALL live in `Njord.Core/Actors/` (shared stream handshakes) or privately inside the actor that uses them.

#### Scenario: Messages project has no Akka.Streams dependency
- **WHEN** the Njord.Messages.csproj is inspected
- **THEN** it SHALL NOT contain a PackageReference to any `Akka*` package

#### Scenario: SinkRef request records are actor-internal
- **WHEN** an actor needs to request a SinkRef from another actor
- **THEN** the request and response records SHALL be defined in `Njord.Core/Actors/` (shared handshake) or privately in the requesting library
- **AND** they SHALL NOT appear in the Njord.Messages project

#### Scenario: Forecast stream request is not a shared message
- **WHEN** a component requests a stream of forecasts
- **THEN** the request and response records SHALL be defined in `Njord.Core/Actors/`
- **AND** they SHALL NOT appear in the Njord.Messages project
