## MODIFIED Requirements

### Requirement: Actor system setup container configures Akka.NET
The system SHALL provide a `NjordActorSystemSetup` extending
`ActorSystemSetupContainer` that configures persistence HOCON, configures
cluster formation via `SeedNodes` (self-join), and registers all top-level
actors via `WithResolvableActors`.

#### Scenario: Cluster forms via SeedNodes before singletons start
- **WHEN** the actor system setup container builds the system
- **THEN** `WithClustering` is called with `ClusterOptions.SeedNodes` containing the node's own address, so the cluster begins forming during actor system startup — before singleton proxies start their lookup timers

#### Scenario: Remoting uses a fixed port
- **WHEN** the actor system setup container configures remoting
- **THEN** `WithRemoting` uses a fixed port (2552) instead of an ephemeral port (0), because `SeedNodes` requires a known address

#### Scenario: No manual cluster join
- **WHEN** the actor system is started
- **THEN** there is no `cluster.Join(selfAddress)` call in a `WithActors` callback — the `SeedNodes` configuration handles cluster formation

#### Scenario: Stream shutdown task is registered independently
- **WHEN** the actor system setup container registers actors
- **THEN** `AddStreamShutdownTask` is registered in its own `WithActors` callback, decoupled from cluster join logic
