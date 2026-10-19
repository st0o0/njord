## REMOVED Requirements

### Requirement: SensorKind domain enum
**Reason**: External sensor input is removed. VPD and indoor temperature come from config-only fallbacks or API-native values.
**Migration**: Indoor temperature for index calculations uses the configured `IndoorTemp` value in IndexOptions.

### Requirement: Plausibility validation per SensorKind
**Reason**: No sensor readings are accepted.
**Migration**: N/A.

### Requirement: Staleness expiry
**Reason**: No sensor readings to expire.
**Migration**: N/A.
