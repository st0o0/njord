# publisher-protocol Specification

## Purpose

Publisher-agnostic egress routing: EgressActor accepts publisher registrations and broadcasts enrichment results to all registered publishers without knowing their concrete types.

## Requirements

### Requirement: EgressActor accepts publisher registration
The `EgressActor` SHALL accept `RegisterPublisher` messages containing an `IActorRef`. It SHALL add the publisher to an internal set of active publishers. It SHALL watch registered publishers and auto-unregister on `Terminated`.

#### Scenario: Publisher registers successfully
- **WHEN** an actor sends `RegisterPublisher` to `EgressActor`
- **THEN** the publisher is added to the active set and `EgressActor` watches it

#### Scenario: Publisher terminates and is auto-unregistered
- **WHEN** a registered publisher actor stops
- **THEN** `EgressActor` removes it from the active set

#### Scenario: Duplicate registration is idempotent
- **WHEN** the same `IActorRef` sends `RegisterPublisher` twice
- **THEN** it remains registered once

### Requirement: EgressActor broadcasts results to registered publishers
The `EgressActor` SHALL accept `PublishStateResult` messages and forward them to every registered publisher via `Tell`. If no publishers are registered, the message SHALL be silently dropped.

#### Scenario: Single registered publisher receives result
- **WHEN** one publisher is registered and a `PublishStateResult` arrives
- **THEN** the publisher receives the result

#### Scenario: Multiple publishers receive the same result
- **WHEN** three publishers are registered and a `PublishStateResult` arrives
- **THEN** all three publishers receive the result

#### Scenario: No publishers registered
- **WHEN** no publishers are registered and a `PublishStateResult` arrives
- **THEN** the message is dropped without error

### Requirement: EgressActor accepts publisher unregistration
The `EgressActor` SHALL accept `UnregisterPublisher` messages and remove the publisher from the active set. Unregistering a non-registered publisher SHALL be a no-op.

#### Scenario: Publisher unregisters
- **WHEN** a registered publisher sends `UnregisterPublisher`
- **THEN** it is removed from the active set and no longer receives results

#### Scenario: Unregistering unknown publisher is a no-op
- **WHEN** an unknown `IActorRef` sends `UnregisterPublisher`
- **THEN** no error occurs
