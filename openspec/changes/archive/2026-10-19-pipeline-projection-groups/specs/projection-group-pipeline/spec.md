## ADDED Requirements

### Requirement: Pipeline evaluates enrichments via ProjectionGroups
The StatefulSelectMany stage SHALL iterate registered ProjectionGroups. For each group affected by the incoming domain update, it SHALL call the group's Projector, and if the projection is non-null, evaluate all enrichments in the group.

#### Scenario: Group with satisfied dependencies runs
- **WHEN** Weather data arrives and a WeatherAlertGroup requires Weather
- **THEN** the WeatherAlertProjector SHALL project and all alert enrichments SHALL evaluate

#### Scenario: Group with unsatisfied dependencies is skipped
- **WHEN** Weather data arrives and a FloodRainGroup requires Weather and Flood but no Flood data exists yet
- **THEN** the FloodRainGroup SHALL be skipped entirely

### Requirement: Pipeline state is LocationForecast per location
The StatefulSelectMany closure SHALL maintain a dictionary of (LocationForecast Current, LocationForecast? Previous) per location. On each DomainDataReceived, the current forecast's domain slot SHALL be updated and the previous SHALL be set to the pre-update state.

#### Scenario: Previous state preserved for trends
- **WHEN** Weather data arrives twice for the same location
- **THEN** the second evaluation SHALL have access to the first Weather data as Previous
