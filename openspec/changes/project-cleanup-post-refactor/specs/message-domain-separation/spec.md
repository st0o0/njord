## MODIFIED Requirements

### Requirement: Stream shutdown messages are actor-internal

`StopStreams` and `StreamsStopped` SHALL be defined in `Njord.Core.Actors` as
shared internal records for coordinated shutdown. `StreamsStopFailed` SHALL NOT
exist — PipelineActor always replies `StreamsStopped` synchronously, and the
`StopStreamsOf` helper in `PipelineSetupContainer` SHALL use
`Ask<StreamsStopped>` instead of `Ask<object>` with a type check.

#### Scenario: Stream shutdown messages not in shared messages
- **WHEN** the Njord.Messages project is inspected
- **THEN** it SHALL NOT contain StopStreams or StreamsStopped record definitions

#### Scenario: Coordinated shutdown uses typed Ask
- **WHEN** the actor system initiates coordinated shutdown
- **THEN** `StopStreamsOf` SHALL use `Ask<StreamsStopped>` and not check for a
  `StreamsStopFailed` variant

## REMOVED Requirements

### Requirement: HaBirthDetected message
**Reason**: Defined in `Njord.Messages.Mqtt` but never constructed or received by
any actor. Was planned during actor-architecture-cleanup but never wired up.
**Migration**: Remove `HaBirthDetected` from `MqttMessages.cs`. If HA birth
detection is needed later, re-add it with actual producer/consumer wiring.
