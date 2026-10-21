# grpc-multi-domain Specification

## Purpose

gRPC schema support for multi-domain weather data. Defines per-domain data messages and unified streaming RPCs that deliver domain-specific events to clients with optional domain filtering.

## Requirements

### Requirement: gRPC proto supports multi-domain data
The proto schema SHALL define per-domain data messages (WeatherData, AirQualityData, FloodData, MarineData,
SatelliteData, SeasonalData) and a unified streaming RPC that delivers domain-specific events to clients.
SeasonalData SHALL carry one point per month with its valid time, temperature anomaly, precipitation anomaly and
precipitation mean.

#### Scenario: Client receives weather and air quality data
- **WHEN** a client streams domain data for location "Lucerne"
- **THEN** it SHALL receive WeatherData and AirQualityData messages as each domain updates

#### Scenario: Seasonal data on request
- **WHEN** seasonal data has arrived for "Lucerne" and a client calls `GetDomainData` for the Seasonal domain
- **THEN** it SHALL receive SeasonalData with one point per month and the anomaly values of the forecast

#### Scenario: Seasonal data on the stream
- **WHEN** a client streams domain data filtered to Seasonal and seasonal data arrives
- **THEN** it SHALL receive a SeasonalData update

### Requirement: Domain filtering on streams
Streaming RPCs SHALL accept an optional domain filter so clients can subscribe to specific domains only.

#### Scenario: Client filters for Weather only
- **WHEN** a client subscribes with domain filter [Weather]
- **THEN** it SHALL receive only WeatherData events, not AirQuality or Flood events
