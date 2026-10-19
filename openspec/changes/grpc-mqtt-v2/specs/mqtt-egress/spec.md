## MODIFIED Requirements

### Requirement: Topic scheme uses domain-based hierarchy
The TopicScheme SHALL produce topics in the format `njord/{location}/{domain}/{parameter}` for domain data and `njord/{location}/alerts/{alert_type}` for alerts. The old `njord/{location}/{model}/{parameter}` format is removed.

#### Scenario: Domain topic for air quality
- **WHEN** AirQuality data for location "Lucerne" with PM2.5 is published
- **THEN** the topic SHALL be `njord/lucerne/air-quality/pm25`

### Requirement: MqttStatePublisher handles new event types
The publisher SHALL map WeatherDataReceived, AirQualityDataReceived, FloodDataReceived, SolarDataReceived, and AlertRaised events to their respective MQTT topics and payloads.

#### Scenario: Publisher handles AlertRaised
- **WHEN** an AlertRaised event arrives for a frost alert
- **THEN** the publisher SHALL publish an alert payload to `njord/{location}/alerts/frost`
