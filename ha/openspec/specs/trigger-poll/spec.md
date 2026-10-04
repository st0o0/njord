## Purpose

Defines the trigger poll capability -- a button entity and HA service that allow users to manually trigger forecast polling on the njord server via `OpsService.TriggerPoll`.

## Requirements

### Requirement: Trigger poll button entity
The integration SHALL create one `NjordTriggerPollButton` button entity per config entry that triggers a full forecast poll on njord via `OpsService.TriggerPoll("", "")`. The button entity SHALL be assigned to the Server device (identifier `{entry_id}_server`) alongside diagnostic sensors.

#### Scenario: Button press triggers poll
- **WHEN** the user presses the "Trigger Poll" button in the HA UI
- **THEN** the integration calls `OpsService.TriggerPoll(location="", model="")` and the button's `triggered_count` attribute reflects the server's response

#### Scenario: Button shows last trigger result
- **WHEN** a poll has been triggered
- **THEN** the button entity exposes `triggered_count` and `last_triggered` as extra state attributes

#### Scenario: Button available when connected
- **WHEN** the gRPC client is connected
- **THEN** the button entity is available

#### Scenario: Button belongs to Server device
- **WHEN** the button entity is created
- **THEN** its `DeviceInfo.identifiers` SHALL match the Server device `(DOMAIN, "{entry_id}_server")`
- **AND** no standalone device is created for the button

### Requirement: Trigger poll HA service
The integration SHALL register a domain-level HA service `njord.trigger_poll` with optional `location` and `model` string parameters for fine-grained poll triggering.

#### Scenario: Service call with no parameters
- **WHEN** `njord.trigger_poll` is called without parameters
- **THEN** the integration calls `OpsService.TriggerPoll(location="", model="")` triggering all targets

#### Scenario: Service call with location only
- **WHEN** `njord.trigger_poll` is called with `location="graz"`
- **THEN** the integration calls `OpsService.TriggerPoll(location="graz", model="")` triggering all models for that location

#### Scenario: Service call with location and model
- **WHEN** `njord.trigger_poll` is called with `location="graz"` and `model="icon_d2"`
- **THEN** the integration calls `OpsService.TriggerPoll(location="graz", model="icon_d2")` triggering only that specific target

#### Scenario: Service registered on setup
- **WHEN** the integration is loaded
- **THEN** the `njord.trigger_poll` service is registered and visible in HA Developer Tools

#### Scenario: Service unregistered on unload
- **WHEN** the last config entry is unloaded
- **THEN** the `njord.trigger_poll` service is removed
