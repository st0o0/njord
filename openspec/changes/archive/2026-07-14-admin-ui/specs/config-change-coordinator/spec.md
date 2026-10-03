# config-change-coordinator Specification

## Purpose

A hosted service that observes `IOptionsMonitor<NjordOptions>` changes, computes diffs between the old and new configuration snapshots, validates the new configuration, and dispatches actor messages to apply model/location changes without pipeline restart.

## ADDED Requirements

### Requirement: ConfigChangeCoordinator listens on IOptionsMonitor
The `ConfigChangeCoordinator` SHALL be an `IHostedService` that registers an `IOptionsMonitor<NjordOptions>.OnChange` callback. On each change, it SHALL capture the previous and current `NjordOptions` snapshots for diffing.

#### Scenario: Options change triggers diff
- **WHEN** `IOptionsMonitor<NjordOptions>` fires a change notification
- **THEN** the coordinator computes a diff between the previous and current snapshots

#### Scenario: Identical snapshots produce no actions
- **WHEN** the previous and current snapshots are structurally identical
- **THEN** no actor messages are dispatched

### Requirement: ConfigChangeCoordinator computes model/location diffs
The coordinator SHALL compute:
- **Added targets**: (location, model) pairs present in the new config but not the old
- **Removed targets**: (location, model) pairs present in the old config but not the new
- **Added locations**: locations present in the new config but not the old
- **Removed locations**: locations present in the old config but not the new

The diff SHALL use `LocationOptions.ResolveModels(globalModels)` to compute effective model sets per location.

#### Scenario: New model added to existing location
- **WHEN** location "lucerne" gains model `metno_nordic` in the new config
- **THEN** the diff contains `Added: [("lucerne", "metno_nordic")]` and `Removed: []`

#### Scenario: Model removed from location
- **WHEN** location "lucerne" loses model `icon_d2` in the new config
- **THEN** the diff contains `Added: []` and `Removed: [("lucerne", "icon_d2")]`

#### Scenario: New location added
- **WHEN** location "berlin" is added with models `["icon_d2", "icon_eu"]`
- **THEN** the diff contains `Added: [("berlin", "icon_d2"), ("berlin", "icon_eu")]`

#### Scenario: Location removed
- **WHEN** location "borken" is removed from the config
- **THEN** the diff contains `Removed` entries for all of borken's resolved models

### Requirement: ConfigChangeCoordinator validates before applying
The coordinator SHALL validate the new configuration using the same rules as `NjordOptionsValidator` (budget projection, model coverage, horizon bounds, required fields) before dispatching any actor messages. If validation fails, the coordinator SHALL log the validation errors and NOT dispatch any messages. The writable provider values SHALL NOT be reverted automatically — the UI is responsible for showing the validation error.

#### Scenario: Valid change dispatches messages
- **WHEN** the diff is non-empty and the new config passes validation
- **THEN** actor messages are dispatched for the added/removed targets

#### Scenario: Invalid change is rejected
- **WHEN** the new config exceeds the budget projection guard
- **THEN** no actor messages are dispatched and the validation error is logged

### Requirement: ConfigChangeCoordinator dispatches AddTargets/RemoveTargets
When the diff contains added or removed targets, the coordinator SHALL send `AddTargets(IReadOnlyList<(Location, WeatherModel)>)` and/or `RemoveTargets(IReadOnlyList<(Location, WeatherModel)>)` messages to the `SchedulerActor`.

#### Scenario: Added targets dispatched to SchedulerActor
- **WHEN** the diff contains 2 added targets
- **THEN** the coordinator sends `AddTargets` with those 2 targets to the SchedulerActor

#### Scenario: Removed targets dispatched to SchedulerActor
- **WHEN** the diff contains 1 removed target
- **THEN** the coordinator sends `RemoveTargets` with that target to the SchedulerActor

### Requirement: ConfigChangeCoordinator triggers discovery refresh
When the diff contains any added or removed targets, the coordinator SHALL send a `RefreshDiscovery` message to the `DiscoveryActor` after dispatching to the SchedulerActor.

#### Scenario: Discovery refreshed on model change
- **WHEN** targets are added or removed
- **THEN** the coordinator sends `RefreshDiscovery` to the DiscoveryActor

#### Scenario: Non-model changes skip discovery refresh
- **WHEN** only enrichment thresholds change (no target diff)
- **THEN** no `RefreshDiscovery` message is sent

### Requirement: ConfigChangeCoordinator is resilient to rapid changes
The coordinator SHALL debounce rapid `OnChange` callbacks. If multiple changes arrive within 2 seconds, only the last diff SHALL be processed. This prevents cascading actor messages when the UI updates multiple config keys in quick succession.

#### Scenario: Rapid changes debounced
- **WHEN** 5 config changes arrive within 1 second
- **THEN** only one diff (comparing original state to final state) is processed
