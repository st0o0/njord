## REMOVED Requirements

### Requirement: Energy forecast computation
**Reason**: Energy management is being removed from njord entirely. Energy computations (HeatingDemand, CopEstimate, CopOptimalHours, ShadingScore, BatteryStrategy, NightCoolingPotential) will not be part of njord going forward.
**Migration**: None. Njord is unreleased. No consumers to migrate.

### Requirement: Energy enrichment pipeline integration
**Reason**: The `EnergyEnrichment` implementation of `IStatelessEnrichment` and its registration in the enrichment pipeline are removed along with all energy domain logic.
**Migration**: None.

### Requirement: Energy MQTT discovery and state payloads
**Reason**: Energy device discovery payloads (heating_demand, cop_estimate, shading, night_cooling, battery_strategy, cop_optimal, cop_optimal_conservative, heating_demand_max, cop_estimate_min) and state messages are removed from MQTT egress.
**Migration**: None.

### Requirement: Energy gRPC API surface
**Reason**: `EnergyUpdate`, `EnergyConfig`, `CopOptimalHour` proto messages, `SetEnrichment` energy field, and energy events in `GetEnrichments`/`StreamEnrichments` are removed.
**Migration**: None.

### Requirement: Energy configuration and validation
**Reason**: `EnergyOptions` (Enabled, FlowTemp, CarnotEfficiency, HeatingBaseTemp, CopOptimalHours, IndoorTemp) and `EnergyOptionsValidator` are removed from the configuration system.
**Migration**: The `Njord:Enrichment:Energy` config section is silently ignored if present.
