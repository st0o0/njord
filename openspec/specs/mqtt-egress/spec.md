# mqtt-egress Specification

## Purpose

MQTT state payload rules and topic scheme for multi-domain weather data. Topics use domain-based hierarchy: `njord/{location}/{domain}/{parameter}` for data and `njord/{location}/alerts/{alert_type}` for alerts. Missing values are omitted from payloads.

## Requirements

### Requirement: Topic scheme uses domain-based hierarchy
The TopicScheme SHALL produce topics in the format `njord/{location}/{domain}/{parameter}` for domain data and `njord/{location}/alerts/{alert_type}` for alerts. The old `njord/{location}/{model}/{parameter}` format is removed.

#### Scenario: Domain topic for air quality
- **WHEN** AirQuality data for location "Lucerne" with PM2.5 is published
- **THEN** the topic SHALL be `njord/lucerne/air-quality/pm25`

#### Scenario: Alert topic for frost
- **WHEN** a frost alert is raised for location "Lucerne"
- **THEN** the topic SHALL be `njord/lucerne/alerts/frost`

### Requirement: MqttStatePublisher handles new event types
The publisher SHALL map WeatherDataReceived, AirQualityDataReceived, FloodDataReceived, SolarDataReceived, and AlertRaised events to their respective MQTT topics and payloads. Old event types (ConsensusComputed, DerivedComputed, TrendComputed, IndexComputed) produce no messages.

#### Scenario: Publisher handles AlertRaised
- **WHEN** an AlertRaised event arrives for a frost alert
- **THEN** the publisher SHALL publish an alert payload to `njord/{location}/alerts/frost`

#### Scenario: Publisher handles WeatherDataReceived
- **WHEN** a WeatherDataReceived event arrives
- **THEN** the publisher SHALL publish weather parameter values to domain-based topics

### Requirement: Missing values are omitted from state payloads
Domain data payloads SHALL omit parameters whose value is null. Alert payloads SHALL write an explicit `null` for a known member whose value is absent. njord SHALL NOT publish `unavailable` states or `expire_after` settings. Availability SHALL be signalled only through the retained `online`/`offline` service status topic.

#### Scenario: Unsupported parameter has no key
- **WHEN** a domain returns no value for a parameter
- **THEN** the payload contains no key for that parameter

#### Scenario: Alert without optional timing values
- **WHEN** an alert has no peak value, no hours until and no duration
- **THEN** its payload contains `"peak_value":null`, `"hours_until":null` and `"duration_hours":null`

#### Scenario: Soil frozen is a boolean
- **WHEN** an ice alert reports frozen soil
- **THEN** the payload contains `"soil_frozen":true`
