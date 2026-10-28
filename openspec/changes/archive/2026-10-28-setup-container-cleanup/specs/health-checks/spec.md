## ADDED Requirements

### Requirement: Akka persistence health checks on journal and snapshot stores
The `AkkaSetupContainer` SHALL configure `WithSqlPersistence` with `journalBuilder` calling `WithHealthCheck()` and `snapshotBuilder` calling `WithHealthCheck()`. These Akka-level checks verify that the persistence layer can perform read/write operations, complementing the existing `JournalHealthCheck` which checks underlying storage connectivity.

#### Scenario: Journal persistence health check is registered
- **WHEN** the actor system starts
- **THEN** an Akka persistence journal health check is registered via `WithHealthCheck()` on the journal builder

#### Scenario: Snapshot persistence health check is registered
- **WHEN** the actor system starts
- **THEN** an Akka persistence snapshot health check is registered via `WithHealthCheck()` on the snapshot builder

#### Scenario: Both storage and persistence checks coexist
- **WHEN** the host's health checks are evaluated
- **THEN** both the custom `JournalHealthCheck` (storage connectivity) and the Akka persistence checks (operational health) are evaluated independently

### Requirement: Actor system liveness check
The `AkkaSetupContainer` SHALL register `WithActorSystemLivenessCheck()` so that the actor system's operational status is included in the health check evaluation. The check SHALL report unhealthy when the actor system is terminating or has terminated.

#### Scenario: Healthy actor system
- **WHEN** the actor system is running normally
- **THEN** the actor system liveness check reports healthy

#### Scenario: Terminating actor system
- **WHEN** the actor system is shutting down
- **THEN** the actor system liveness check reports unhealthy

### Requirement: Cluster readiness check
The `AkkaSetupContainer` SHALL register `WithAkkaClusterReadinessCheck()` so that the cluster join status is included in the health check evaluation. The check SHALL report unhealthy when the node has not yet joined the cluster.

#### Scenario: Joined cluster
- **WHEN** the node has joined the cluster
- **THEN** the cluster readiness check reports healthy

#### Scenario: Not yet joined
- **WHEN** the node has not yet joined the cluster (still in joining state)
- **THEN** the cluster readiness check reports unhealthy
