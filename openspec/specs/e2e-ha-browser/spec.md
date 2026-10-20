# e2e-ha-browser Specification

## Purpose

Defines the browser-driven E2E checks in Home Assistant: entity cards, Developer Tools inspection and triggering a poll from the UI.

## Requirements

### Requirement: Trigger poll from HA UI

The E2E test plan SHALL verify that the trigger_poll button works from the HA interface.

#### Scenario: Button press via HA UI
- **WHEN** `button.trigger_poll` is found in the HA UI (entity page or developer tools) and pressed
- **THEN** within 60 seconds, weather entity `last_updated` timestamps are refreshed
