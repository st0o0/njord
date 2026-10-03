## REMOVED Requirements

### Requirement: EnergyUpdate and CopOptimalHour messages
**Reason**: The `EnergyUpdate` message (heating_demand, cop_estimate, shading, battery_strategy, night_cooling, cop_optimal) and `CopOptimalHour` message are removed from the common proto definitions.
**Migration**: None. Proto field numbers are reserved to prevent reuse.
