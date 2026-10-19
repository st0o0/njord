## REMOVED Requirements

### Requirement: SensorService proto definition
**Reason**: External sensor input via gRPC is removed.
**Migration**: ha-njord must remove sensor push functionality.

### Requirement: Push RPC accepts single reading
**Reason**: SensorService is removed.
**Migration**: N/A.
