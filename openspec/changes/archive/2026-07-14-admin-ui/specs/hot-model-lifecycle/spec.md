# hot-model-lifecycle Specification

## Purpose

Extensions to SchedulerActor and DiscoveryActor that enable runtime add/remove of poll targets and discovery entries without tearing down the Akka.Streams pipeline graph.

## ADDED Requirements

### Requirement: SchedulerActor handles AddTargets command
The `SchedulerActor` SHALL handle an `AddTargets(IReadOnlyList<(Location, WeatherModel)> Targets)` message. For each target not already in `_states`, it SHALL create a new `ModelPollState` in Discovery phase, persist a `TargetsAdded` event, recompute the request weight, and schedule the first poll via `ScheduleTellOnce`.

#### Scenario: Adding a new model to an existing location
- **WHEN** `AddTargets([("lucerne", "metno_nordic")])` is received
- **THEN** a new `ModelPollState` for `("lucerne", "metno_nordic")` is created in Discovery phase, a `TargetsAdded` event is persisted, and a poll is scheduled in 20 minutes

#### Scenario: Adding a target that already exists is a no-op
- **WHEN** `AddTargets([("lucerne", "icon_d2")])` is received and `("lucerne", "icon_d2")` is already in `_states`
- **THEN** no new state is created and no event is persisted

#### Scenario: Weight recomputed after adding targets
- **WHEN** targets are added and the total parameter count changes the call weight
- **THEN** `_weight` is recomputed from the current `ResolvedParameterSet`

### Requirement: SchedulerActor handles RemoveTargets command
The `SchedulerActor` SHALL handle a `RemoveTargets(IReadOnlyList<(Location, WeatherModel)> Targets)` message. For each target present in `_states`, it SHALL cancel the pending `ScheduleTellOnce` timer, remove the entry from `_states`, and persist a `TargetsRemoved` event.

#### Scenario: Removing an existing model
- **WHEN** `RemoveTargets([("lucerne", "icon_d2")])` is received
- **THEN** the `ModelPollState` for `("lucerne", "icon_d2")` is removed, its timer is cancelled, and a `TargetsRemoved` event is persisted

#### Scenario: Removing a non-existent target is a no-op
- **WHEN** `RemoveTargets([("lucerne", "nonexistent")])` is received
- **THEN** no state is changed and no event is persisted

#### Scenario: Removal stops polling immediately
- **WHEN** a target is removed
- **THEN** no further `WeightedTarget` for that model/location is offered into the pipeline queue

### Requirement: SchedulerActor recovers AddTargets/RemoveTargets events
The `SchedulerActor` SHALL handle `TargetsAdded` and `TargetsRemoved` events during recovery. `TargetsAdded` SHALL recreate `ModelPollState` entries; `TargetsRemoved` SHALL remove them. After recovery, the effective state SHALL reflect the cumulative effect of all persisted events.

#### Scenario: Recovery with add then remove
- **WHEN** the event journal contains `TargetsAdded(["lucerne", "metno_nordic"])` followed by `TargetsRemoved(["lucerne", "metno_nordic"])`
- **THEN** after recovery, no `ModelPollState` exists for `("lucerne", "metno_nordic")`

#### Scenario: Recovery with add only
- **WHEN** the event journal contains `TargetsAdded(["lucerne", "metno_nordic"])`
- **THEN** after recovery, a `ModelPollState` for `("lucerne", "metno_nordic")` exists in Discovery phase

### Requirement: DiscoveryActor handles RefreshDiscovery command
The `DiscoveryActor` SHALL handle a `RefreshDiscovery` message. On receipt, it SHALL:
1. Read the current `NjordOptions` from `IOptionsMonitor`
2. Compute the current device set from locations × models
3. Diff against the last published device set
4. Publish discovery config payloads for new devices
5. Publish empty retained messages (tombstones) for removed devices to remove them from HA

#### Scenario: New model triggers new discovery payloads
- **WHEN** `RefreshDiscovery` is received after model `metno_nordic` was added to location "lucerne"
- **THEN** a retained discovery config payload is published for the `lucerne_metno_nordic` device

#### Scenario: Removed model triggers tombstone
- **WHEN** `RefreshDiscovery` is received after model `icon_d2` was removed from location "lucerne"
- **THEN** an empty retained message is published to the discovery topic for the `lucerne_icon_d2` device, causing HA to remove it

#### Scenario: No diff produces no MQTT traffic
- **WHEN** `RefreshDiscovery` is received but the device set has not changed
- **THEN** no discovery messages are published

### Requirement: DiscoveryActor tracks published device set
The `DiscoveryActor` SHALL maintain an in-memory set of device IDs for which discovery payloads have been published. This set is rebuilt on each `PublishDiscovery` call and used for diffing on `RefreshDiscovery`.

#### Scenario: Published set updated on initial discovery
- **WHEN** `PublishDiscovery` runs on MQTT connect
- **THEN** the published device set is populated with all current devices

#### Scenario: Published set updated after refresh
- **WHEN** `RefreshDiscovery` adds 2 devices and removes 1
- **THEN** the published device set reflects the net change

### Requirement: DiscoveryActor uses IOptionsMonitor instead of IOptions
The `DiscoveryActor` SHALL receive `IOptionsMonitor<NjordOptions>` instead of `IOptions<NjordOptions>` and read `.CurrentValue` when computing the device set for discovery. It SHALL NOT snapshot options at construction time.

#### Scenario: Discovery uses current config
- **WHEN** `RefreshDiscovery` is called after config changes
- **THEN** the actor reads the latest `NjordOptions` from `IOptionsMonitor.CurrentValue`
