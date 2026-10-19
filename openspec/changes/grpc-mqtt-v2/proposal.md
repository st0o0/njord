## Why

The gRPC and MQTT schemas are designed around the old model (per-model forecasts, consensus, derived values). The new multi-domain architecture needs updated schemas that expose domain-specific data, ensemble statistics, and new enrichment events.

## What Changes

- **BREAKING**: gRPC proto schema redesign for multi-domain data.
- WeatherService updated: GetForecast returns ensemble data + optional per-model, GetEnrichments returns domain-specific enrichments.
- New domain-specific streaming (e.g., StreamWeather, StreamAirQuality, StreamAlerts).
- MQTT topic scheme updated for domain-based topics.
- MQTT payloads updated for new event types.
- Old proto messages for consensus, derived values removed.

## Non-goals

- Adding new domain-specific endpoints (those come with each domain in Phase 4).
- Changing the core pipeline — this is consumer-side only.

## Capabilities

### New Capabilities

- `grpc-multi-domain`: gRPC schema for multi-domain weather data.
- `mqtt-multi-domain`: MQTT topics and payloads for multi-domain events.

### Modified Capabilities

- `grpc-v2-weather-service`: Updated for ensemble + multi-domain.
- `grpc-v2-ops-service`: Updated for multi-domain poll status.
- `mqtt-typed-payloads`: New payload types for domain events.
- `mqtt-egress`: Topic scheme updated.

## Impact

- **Njord.Grpc**: Proto redesign, mapping layer rewrite.
- **Njord.Mqtt**: Topic scheme, payload mappings, serializer context.
- **ha-njord**: Must be redesigned for new gRPC schema (major effort).
