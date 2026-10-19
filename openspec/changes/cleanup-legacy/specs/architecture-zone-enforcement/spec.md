## MODIFIED Requirements

### Requirement: Architecture tests enforce new project structure
LayerReferenceSpec and ZoneArchitectureSpec SHALL be rewritten for the new ~12-project structure. The layer direction SHALL be: Model ← Core ← domain libraries (Weather, AirQuality, Flood, Solar, Marine) ← Pipeline ← Grpc/Mqtt ← Host. Domain libraries SHALL NOT reference each other.

#### Scenario: Domain libraries are laterally independent
- **WHEN** Njord.AirQuality is inspected
- **THEN** it SHALL NOT reference Njord.Weather, Njord.Flood, or any other domain library

#### Scenario: Removed projects are not in the assembly list
- **WHEN** NjordArchitecture lists solution assemblies
- **THEN** Consensus, Derived, Forecast, Sensors, SensorHub SHALL NOT appear
