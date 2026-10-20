## ADDED Requirements

### Requirement: Single AlertType enum covers all domains
AlertType SHALL contain all alert types: Frost, Ice, Heat, HeavyRain, Snow, Storm, Thunderstorm, Fog, Humidity, PressureDrop, TropicalNight, UV, Visibility, WindChill, Pollen, Pm25, FloodRisk, FloodRain. AirQualityAlertType and FloodAlertType SHALL NOT exist.

#### Scenario: No parallel alert type enums
- **WHEN** the codebase is searched for AirQualityAlertType or FloodAlertType
- **THEN** zero references SHALL exist

### Requirement: One AlertRaised pipeline event
All alerts SHALL flow through one `AlertRaised(Location, Alert, DateTimeOffset)` event. Separate `AirQualityAlertRaised` and `FloodAlertRaised` events SHALL NOT exist.

#### Scenario: AQ alert emits AlertRaised
- **WHEN** the pollen level exceeds the threshold
- **THEN** the pipeline SHALL emit `AlertRaised` with `Alert.Type == AlertType.Pollen`

#### Scenario: Flood alert emits AlertRaised
- **WHEN** river discharge exceeds p75
- **THEN** the pipeline SHALL emit `AlertRaised` with `Alert.Type == AlertType.FloodRisk`

## REMOVED Requirements

### Requirement: AirQualityAlertRaised pipeline event
**Reason**: Replaced by unified AlertRaised.
**Migration**: Use AlertRaised with AlertType.Pollen or AlertType.Pm25.

### Requirement: FloodAlertRaised pipeline event
**Reason**: Replaced by unified AlertRaised.
**Migration**: Use AlertRaised with AlertType.FloodRisk or AlertType.FloodRain.
