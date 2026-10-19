## ADDED Requirements

### Requirement: gRPC proto supports multi-domain data
The proto schema SHALL define per-domain data messages (WeatherData, AirQualityData, FloodData, SolarData, MarineData) and a unified streaming RPC that delivers domain-specific events to clients.

#### Scenario: Client receives weather and air quality data
- **WHEN** a client streams domain data for location "Lucerne"
- **THEN** it SHALL receive WeatherData and AirQualityData messages as each domain updates

### Requirement: Domain filtering on streams
Streaming RPCs SHALL accept an optional domain filter so clients can subscribe to specific domains only.

#### Scenario: Client filters for Weather only
- **WHEN** a client subscribes with domain filter [Weather]
- **THEN** it SHALL receive only WeatherData events, not AirQuality or Flood events
