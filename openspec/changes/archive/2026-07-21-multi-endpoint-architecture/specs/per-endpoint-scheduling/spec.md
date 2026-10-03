## ADDED Requirements

### Requirement: Scheduler manages poll state per location and endpoint type
The `SchedulerActor` SHALL maintain poll state entries keyed by `(Location, EndpointType, Model?)` instead of `(Location, Model)`. For endpoints without a model dimension, `Model` SHALL be null. Each entry SHALL have its own independent timer, Discovery/Steady phase, and backoff state.

#### Scenario: Weather entries are keyed with model
- **WHEN** location "lucerne" is configured with models [icon_d2, icon_eu]
- **THEN** the scheduler SHALL create entries `(lucerne, Weather, icon_d2)` and `(lucerne, Weather, icon_eu)`

#### Scenario: Model-less endpoint entries are keyed without model
- **WHEN** AirQuality is enabled for location "lucerne"
- **THEN** the scheduler SHALL create entry `(lucerne, AirQuality, null)`

#### Scenario: Independent timers per endpoint type
- **WHEN** Weather has a 60min interval and AirQuality has a 120min interval
- **THEN** the scheduler SHALL fire weather timers at 60min intervals and AQ timers at 120min intervals independently

### Requirement: Scheduler creates targets via endpoint modules
The `SchedulerActor` SHALL use `IEndpointModule.CreateTargets(location, cycleId)` to produce `WeightedTarget` instances instead of constructing `WeightedTarget` directly. This ensures each target is the correct concrete subclass for its endpoint.

#### Scenario: Scheduler delegates target creation to modules
- **WHEN** the weather timer fires for location "lucerne"
- **THEN** the scheduler SHALL call `WeatherModule.CreateTargets(lucerne, cycleId)` and offer the returned targets into the MergeHub queue

#### Scenario: Module returns multiple targets for multi-model endpoint
- **WHEN** `WeatherModule.CreateTargets(lucerne, cycle1)` is called and lucerne has 3 models
- **THEN** 3 `WeatherTarget` instances SHALL be offered into the queue

### Requirement: Per-endpoint poll interval is configurable
Each endpoint type SHALL have a configurable `PollInterval` in the `Endpoints` configuration section. The default poll interval for Weather SHALL be 60 minutes. The `SchedulerActor` SHALL use the endpoint-specific interval when scheduling timers in Discovery phase and as the fallback interval.

#### Scenario: Weather uses configured interval
- **WHEN** `Endpoints.Weather.PollInterval` is set to `"00:45:00"`
- **THEN** the scheduler SHALL use 45 minutes as the Discovery-phase interval for weather entries

#### Scenario: Unconfigured endpoint uses its default
- **WHEN** `Endpoints.AirQuality.PollInterval` is not set and the default is 120 minutes
- **THEN** the scheduler SHALL use 120 minutes for AQ Discovery-phase polling
