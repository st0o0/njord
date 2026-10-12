## ADDED Requirements

### Requirement: Actor classes use domain-role names without Actor suffix
All actor classes SHALL be named by their domain role without an `Actor` suffix, following the FunkArr convention. The mapping SHALL be:

| Old Name | New Name |
|---|---|
| `SchedulerActor` | `PollScheduler` |
| `PipelineActor` | `ForecastPipeline` |
| `BudgetTrackerActor` | `BudgetTracker` |
| `SensorHubActor` | `SensorHub` |
| `GrpcConsumerActor` | `GrpcConsumer` |
| `MqttConnectionActor` | `MqttConnection` |
| `MqttStateActor` | `MqttState` |

All actor classes SHALL remain `sealed`.

#### Scenario: No actor class uses the Actor suffix
- **WHEN** production code is searched for classes extending `ReceiveActor`, `ReceivePersistentActor`, or `UntypedActor`
- **THEN** none of the class names SHALL end with `Actor`

#### Scenario: Existing behavior preserved
- **WHEN** `PollScheduler` (formerly `SchedulerActor`) handles a `ScheduledPoll`
- **THEN** the behavior SHALL be identical to the previous `SchedulerActor` implementation

### Requirement: Actor keys use domain-role names without Actor suffix
All actor key marker interfaces in `Njord.Core/Actors/ActorKeys.cs` SHALL use domain-role names. The mapping SHALL be:

| Old Key | New Key |
|---|---|
| `ISchedulerActor` | `IPollScheduler` |
| `IPipelineActor` | `IForecastPipeline` |
| `IBudgetTrackerActor` | `IBudgetTracker` |
| `ISensorHubActor` | `ISensorHub` |
| `IGrpcConsumerActor` | `IGrpcConsumer` |
| `IMqttConnectionActor` | `IMqttConnection` |
| `IMqttStateActor` | `IMqttState` |

The new key `IForecastGateway` SHALL be added for the `OpenMeteoGateway` actor.

#### Scenario: Actor registration uses new key names
- **WHEN** the host registers actors via setup containers
- **THEN** all `WithSingleton<T>` calls SHALL use the new key interfaces

#### Scenario: Actor resolution uses new key names
- **WHEN** any actor resolves another via `Context.GetActor<T>()` or `GetActorAsync<T>()`
- **THEN** the generic parameter SHALL be the new key interface name

### Requirement: Test spec class names follow the rename
Test spec classes SHALL be renamed to match their actor's new name. For example, `SchedulerActorSpec` SHALL become `PollSchedulerSpec`. The `Spec` suffix SHALL be preserved.

#### Scenario: Test class names match actor names
- **WHEN** a test class tests `PollScheduler` behavior
- **THEN** the class SHALL be named `PollSchedulerSpec`, not `SchedulerActorSpec`
