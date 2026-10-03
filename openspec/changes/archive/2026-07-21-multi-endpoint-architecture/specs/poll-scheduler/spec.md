## MODIFIED Requirements

### Requirement: The SchedulerActor manages per-model poll timing
A `SchedulerActor` (ReceivePersistentActor) SHALL maintain a `ModelPollState` per configured (location, endpointType, model?) triple. For endpoint types without a model dimension, the model component SHALL be null. Each state SHALL track: `lastHash` (int?), `lastChangeUtc` (DateTimeOffset?), `prevChangeUtc` (DateTimeOffset?), `nextPollUtc` (DateTimeOffset), `missCount` (int), and `phase` (Discovery or Steady). The actor SHALL use `ScheduleTellOnce` to fire polls at each entry's individually calculated time. On first initialization (no prior persisted state), all entries SHALL have `NextPollUtc = now` — there is no stagger delay. The pipeline's Throttle operator is the sole rate-limiting gate.

#### Scenario: Each location-endpoint-model triple gets its own timer
- **WHEN** 1 location, Weather with 3 models, and AirQuality (no models) are configured
- **THEN** the SchedulerActor maintains 4 independent `ModelPollState` entries: 3 for Weather and 1 for AirQuality

#### Scenario: Timer fires delegate target creation to the endpoint module
- **WHEN** a `ScheduleOnce` timer fires for (lucerne, Weather)
- **THEN** the actor calls `WeatherModule.CreateTargets(lucerne, cycleId)` and offers the returned targets into its local `Source.Queue`

#### Scenario: Initial polls are offered without stagger delay
- **WHEN** the SchedulerActor initializes with 27 weather entries and 3 AQ entries
- **THEN** all 30 `ScheduleOnce` timers fire with `NextPollUtc = now`, offering all targets to the queue immediately
- **AND** the pipeline Throttle shapes them to the configured rate

#### Scenario: Recovered state preserves existing NextPollUtc
- **WHEN** the SchedulerActor recovers with persisted state for a weather model
- **THEN** the recovered `NextPollUtc` is used as-is (no stagger applied)

### Requirement: SchedulerActor iterates endpoint modules and resolved locations
The `SchedulerActor` SHALL iterate all registered `IEndpointModule` instances and for each module, iterate all configured locations. For endpoints with a model dimension, the scheduler SHALL use `LocationOptions.ResolveModels(globalModels)`. For endpoints without models, the scheduler SHALL create one poll state entry per location. The scheduler SHALL NOT hardcode which endpoints have models — this is determined by the module's `CreateTargets` output.

#### Scenario: Weather module resolves models per location
- **WHEN** global Models is `["icon_global"]` and location "berlin" has Models `["icon_d2"]`
- **THEN** the scheduler SHALL create poll states for `(berlin, Weather, icon_global)` and `(berlin, Weather, icon_d2)`

#### Scenario: Model-less module creates one entry per location
- **WHEN** AirQuality module is registered for locations [lucerne, zurich]
- **THEN** the scheduler SHALL create poll states `(lucerne, AirQuality, null)` and `(zurich, AirQuality, null)`

### Requirement: State is persisted and recovered via Akka.Persistence
The SchedulerActor SHALL persist `DataChanged` events to a SQLite journal via Akka.Persistence. Events SHALL include the `EndpointType` to distinguish entries. On recovery, the actor SHALL rebuild all `ModelPollState` entries from the event stream. Legacy events without an `EndpointType` field SHALL default to `Weather`. If a recovered `nextPollUtc` is in the past, the actor SHALL poll immediately. If a cycle is known from recovery, the actor SHALL enter Steady phase directly without re-discovery.

#### Scenario: Recovery of legacy events defaults to Weather
- **WHEN** the actor recovers a persisted `DataChanged` event from before the multi-endpoint migration that has no `EndpointType` field
- **THEN** the event SHALL be treated as `EndpointType = Weather`

#### Scenario: Recovery skips discovery for known cycles
- **WHEN** the actor recovers with a persisted cycle of 3h and `lastChangeUtc = 09:30` for a Weather entry
- **THEN** the actor enters Steady phase and schedules the next poll at `09:30 + 3h + 1min = 12:31`

#### Scenario: Past nextPollUtc triggers immediate poll
- **WHEN** the actor recovers and `nextPollUtc = 08:00` but the current time is 10:00
- **THEN** the actor polls immediately

#### Scenario: Recovery with no prior events starts in Discovery
- **WHEN** the actor recovers with an empty event journal
- **THEN** all entries start in Discovery phase at their endpoint-specific poll interval
