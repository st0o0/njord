## MODIFIED Requirements

### Requirement: Startup validates configuration
`NjordOptionsValidator` SHALL validate all location entries at startup. Validation SHALL check that each location has a non-empty `Name`, valid `Latitude` (-90 to 90), and valid `Longitude` (-180 to 180). Timezone validation SHALL NOT be performed — the timezone is derived from the API response, not from configuration.

#### Scenario: Valid location without timezone passes validation
- **WHEN** a location has `Name: "Lucerne"`, `Latitude: 47.05`, `Longitude: 8.31` and no `Timezone` property
- **THEN** validation SHALL succeed

#### Scenario: Location with leftover Timezone property passes validation
- **WHEN** a location config still contains a `Timezone` property from a previous configuration format
- **THEN** validation SHALL succeed — the property is ignored by the binder since it no longer exists on `LocationOptions`

## REMOVED Requirements

### Requirement: Timezone validation at startup
**Reason:** The `Timezone` property has been removed from `LocationOptions`. Timezone is now derived from the Open-Meteo API response. There is no timezone string to validate at startup.
**Migration:** Remove `Timezone` entries from location configuration. No replacement configuration is needed.
