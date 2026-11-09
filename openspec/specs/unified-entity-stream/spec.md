# unified-entity-stream Specification

## Purpose
Single Subscribe stream replacing StreamDomainData + StreamEnrichments, with snapshot-on-connect, availability events, and generic per-OutputKind value types.

## Requirements

### Requirement: Subscribe returns a unified entity event stream
The v3 NjordService SHALL expose a `Subscribe` RPC that returns a server-streaming response of `EntityEvent` messages. This single stream SHALL replace the v2 `StreamDomainData` and `StreamEnrichments` RPCs. All entity state updates, availability changes, and operational signals SHALL flow through this one stream.

#### Scenario: Client receives updates for all entity types
- **WHEN** a client calls `Subscribe` and both a weather update and an alert result arrive
- **THEN** the client SHALL receive both as `EntityEvent` messages on the same stream

### Requirement: Snapshot on connect
When a client calls `Subscribe`, the server SHALL immediately send the current state of every entity that has data as a batch of `EntityStateUpdate` messages before any live updates. This snapshot SHALL reflect the latest known value for each entity at the time of subscription.

#### Scenario: New subscriber receives current state
- **WHEN** a client subscribes and 10 entities already have values
- **THEN** the client SHALL receive 10 `EntityStateUpdate` messages before any live events

#### Scenario: Reconnecting client receives fresh snapshot
- **WHEN** a client disconnects and reconnects
- **THEN** it SHALL receive a complete fresh snapshot, not a diff from the previous connection

### Requirement: State updates keyed by entity ID
Each `EntityStateUpdate` SHALL carry the `entity_id` from the manifest and an `updated_at` timestamp. The value SHALL be a oneof of the OutputKind-specific value types: `WeatherValue`, `AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `ObservationValue`. The entity lookup SHALL resolve Observation results by their `FeatureName` field rather than by CLR type, since all observation features share `ObservationResult`.

#### Scenario: Alert state update
- **WHEN** the FrostAlert feature produces a result for location Vienna
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "vienna_frost"` and an `AlertValue`

#### Scenario: Observation state update
- **WHEN** the PM2.5 observation feature produces a result for location Berlin
- **THEN** the stream SHALL emit an `EntityStateUpdate` with `entity_id = "berlin_pm25"` and an `ObservationValue`

#### Scenario: Multiple observation features produce distinct entity updates
- **WHEN** both PM2.5 and WaveHeight observation features produce results for location Vienna
- **THEN** the stream SHALL emit two distinct `EntityStateUpdate` messages with `entity_id = "vienna_pm25"` and `entity_id = "vienna_wave_height"` respectively

### Requirement: Availability events
The server SHALL send `EntityUnavailable` when it detects that an entity's data source is stale (no successful poll within the expected interval plus tolerance) and `EntityAvailable` when fresh data arrives. Availability SHALL be tracked per entity based on the domain poll status of the entity's required domains.

#### Scenario: Entity becomes unavailable after missed polls
- **WHEN** the Forecast domain for Vienna has not been successfully polled for longer than the expected poll interval plus tolerance
- **THEN** the stream SHALL emit an `EntityUnavailable` for all Vienna entities that depend on the Forecast domain

#### Scenario: Entity becomes available again
- **WHEN** a successful poll arrives for a previously stale domain
- **THEN** the stream SHALL emit `EntityAvailable` for the affected entities

### Requirement: Keep-alive heartbeat
The server SHALL inject a keep-alive signal into the stream at a regular interval (default 30 seconds) when no other events have been sent. The keep-alive SHALL NOT be delivered to the client as an `EntityEvent`; it SHALL only prevent transport-level idle timeouts.

#### Scenario: Idle stream stays alive
- **WHEN** no entity updates occur for 60 seconds
- **THEN** the underlying transport SHALL NOT time out due to inactivity

### Requirement: Client cancellation terminates the stream
When the client cancels the Subscribe call, the server SHALL terminate the stream and release associated resources without error logging.

#### Scenario: Client disconnects gracefully
- **WHEN** a client cancels its Subscribe call
- **THEN** the server-side stream SHALL terminate and the associated KillSwitch SHALL shut down
