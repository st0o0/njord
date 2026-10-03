## REMOVED Requirements

### Requirement: WeatherConditionMapper translates WMO codes to HA conditions
**Reason**: The WMO-to-HA condition mapping is consumer-specific knowledge. It belongs in the HA integration (HACS project), not in njord's core. njord provides raw `weather_code` and `is_day` fields; consumers interpret them.
**Migration**: Consumers that need HA-compatible condition strings should implement their own mapping from WMO codes (0-99) to HA conditions using `weather_code` and `is_day` from the gRPC response.
