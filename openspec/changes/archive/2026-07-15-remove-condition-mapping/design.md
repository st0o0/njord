## Context

The `WeatherConditionMapper` was introduced with the gRPC forecast API to translate WMO weather codes (0-99) to HA-compatible condition strings. On reflection, this maps HA-specific domain knowledge into njord's core — the gRPC API should serve raw weather data and let consumers interpret it.

## Goals / Non-Goals

**Goals:**
- Remove all HA-specific condition mapping from njord.
- Keep `weather_code` and `is_day` in the proto as raw data for consumers to interpret.

**Non-Goals:**
- Redesigning the proto or adding new fields.
- Building the replacement mapper in the HA integration.

## Decisions

### D1: Remove condition fields rather than deprecate

**Decision:** Delete the `condition` fields from the proto messages outright. No `reserved` markers.

**Rationale:** The proto has never been published to external consumers. There are no clients to break. Adding `reserved` for field numbers that were never used externally is unnecessary ceremony.
