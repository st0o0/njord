## REMOVED Requirements

### Requirement: Discovery emits daily consensus components on the consensus device
**Reason**: MQTT Discovery was removed; no consensus device discovery payload is published.
**Migration**: None; daily consensus state is published per day horizon (requirement "State messages emit one JSON per daily horizon").
