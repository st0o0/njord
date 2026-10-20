## ADDED Requirements

### Requirement: Locations require coordinates
Every configured location SHALL have a name, a latitude between -90 and 90 and a longitude between -180 and 180. The service SHALL NOT call any geocoding API.

#### Scenario: Valid location accepted
- **WHEN** a location "Lucerne" with latitude 47.05 and longitude 8.31 is configured
- **THEN** startup validation SHALL succeed

#### Scenario: Missing coordinates rejected
- **WHEN** a location is configured with a name only
- **THEN** startup SHALL fail with a validation error naming the location and the missing latitude and longitude

#### Scenario: Out-of-range coordinates rejected
- **WHEN** a location has latitude 91
- **THEN** startup SHALL fail with a validation error naming the location

### Requirement: Enrichment toggles exclude ClimateAnomaly
The enrichment configuration SHALL expose 19 toggles; there SHALL be no ClimateAnomaly toggle, and no climate data SHALL be fetched.

#### Scenario: Obsolete toggle ignored
- **WHEN** the configuration still contains `Njord:Enrichments:ClimateAnomaly`
- **THEN** startup SHALL succeed and the setting SHALL have no effect
