## ADDED Requirements

### Requirement: BudgetTracker lives in an independent Njord.Budget project
The `BudgetTracker` persistent actor, its state, DTO mapping, and telemetry SHALL reside in a dedicated `Njord.Budget` project. `Njord.Budget` SHALL reference only `Njord.Core` (and below). It SHALL NOT reference `Njord.Pipeline`, `Njord.OpenMeteo`, or any other feature library. The actor SHALL be registered independently via a `BudgetSetupContainer` implementing `IServiceSetupContainer`.

#### Scenario: Budget project references only Core
- **WHEN** the `Njord.Budget.csproj` project references are inspected
- **THEN** it SHALL reference only `Njord.Core` (and its transitive dependencies)

#### Scenario: Architecture tests enforce isolation
- **WHEN** `LayerReferenceSpec` and `ZoneArchitectureSpec` run
- **THEN** `Njord.Budget` SHALL be treated as a feature library with no lateral references to other feature libraries

### Requirement: BudgetTracker is registered independently
The `BudgetTracker` actor SHALL be registered as a backoff-supervised singleton under the `IBudgetTracker` actor key via `BudgetSetupContainer`. It SHALL NOT be registered as part of `PipelineSetupContainer`. The `IBudgetTracker` key SHALL be defined in `Njord.Core/Actors/ActorKeys.cs`.

#### Scenario: BudgetTracker registered via BudgetSetupContainer
- **WHEN** the host starts with `Njord.Budget` enabled
- **THEN** the `BudgetTracker` actor SHALL be available via `ActorRegistry.Get<IBudgetTracker>()`

#### Scenario: PipelineSetupContainer does not register BudgetTracker
- **WHEN** `PipelineSetupContainer.SetupServices` is invoked
- **THEN** it SHALL NOT register the BudgetTracker actor or any budget-related DI services

### Requirement: BudgetTracker receives RecordApiCall from any producer
The `BudgetTracker` actor SHALL accept `RecordApiCall` messages from any actor (currently the OpenMeteoGateway). The interaction SHALL be fire-and-forget via `Tell`. The actor SHALL NOT require the sender to be a specific actor type.

#### Scenario: Gateway sends RecordApiCall
- **WHEN** the OpenMeteoGateway completes an API call with weight 3
- **THEN** it SHALL send `RecordApiCall(3)` to the BudgetTracker via Tell
- **AND** the BudgetTracker SHALL persist the event and update counters

### Requirement: BudgetTracker is queryable by gRPC admin
The `BudgetTracker` actor SHALL continue to handle `QueryBudgetUsage` messages and reply with `QueryBudgetUsageCompleted(MonthlyUsed, DailyUsed)`. The gRPC `OpsGrpcService` SHALL resolve the actor via `IActorRegistry.GetAsync<IBudgetTracker>()`.

#### Scenario: gRPC admin queries budget usage
- **WHEN** a gRPC client calls `GetStatus` which queries budget usage
- **THEN** the service SHALL resolve `IBudgetTracker` via `GetAsync` and Ask for `QueryBudgetUsage`
