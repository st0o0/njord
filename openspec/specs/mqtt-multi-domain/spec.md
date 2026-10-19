# mqtt-multi-domain Specification

## Purpose

MQTT topic scheme and payload support for multi-domain weather data. Defines domain-based topic hierarchy for data and alert topics.

## Requirements

### Requirement: MQTT topics include domain segment
The MQTT topic scheme SHALL include the domain as a path segment: `njord/{location}/{domain}/{parameter}` for data topics and `njord/{location}/alerts/{alert_type}` for alert topics.

#### Scenario: Weather temperature topic
- **WHEN** weather data is published for location "Lucerne"
- **THEN** the temperature SHALL be published to `njord/lucerne/weather/temperature`

#### Scenario: Air quality PM2.5 topic
- **WHEN** air quality data is published for location "Lucerne"
- **THEN** PM2.5 SHALL be published to `njord/lucerne/air-quality/pm25`
