## ADDED Requirements

### Requirement: Health checks query actors via Ask
Actor-backed health checks SHALL use Ask with a timeout to query the relevant actor, not read from intermediate state holders.

#### Scenario: Actor responds within timeout
- **WHEN** /healthz triggers a health check and the actor responds within 3 seconds
- **THEN** the check SHALL evaluate the response and return the appropriate HealthCheckResult

#### Scenario: Actor unresponsive
- **WHEN** /healthz triggers a health check and the actor does not respond within 3 seconds
- **THEN** the check SHALL return HealthCheckResult.Unhealthy with a descriptive message

#### Scenario: Actor restarting
- **WHEN** an actor is restarting (BackoffSupervisor) during a health check
- **THEN** the Ask SHALL timeout and the check SHALL report Unhealthy (correct: a restarting actor IS unhealthy)

### Requirement: No intermediate state holder classes
Health state SHALL NOT be cached in shared singleton state holders. Each health check SHALL query the actor that owns the state.

#### Scenario: State freshness
- **WHEN** a health check runs
- **THEN** it SHALL receive the actor's current state, not a stale cached value

### Requirement: Evaluate remains a pure static function
The health evaluation logic SHALL remain in a static Evaluate method for testability. The Check method does I/O (Ask), Evaluate does logic.

#### Scenario: Unit testing health logic
- **WHEN** testing health check evaluation logic
- **THEN** tests SHALL call the static Evaluate method directly with constructed inputs, without needing actors or DI
