## ADDED Requirements

### Requirement: Satellite radiation client fetches measured irradiance
The system SHALL provide an `ISatelliteRadiationClient` in `Njord.OpenMeteo` that fetches hourly radiation from `satellite-radiation-api.open-meteo.com/v1/radiation` using `OpenMeteoRequestBuilder`. The client SHALL receive variables, tilt and azimuth from the caller and return a typed `SatelliteRadiationResponse`.

#### Scenario: Request contains orientation
- **WHEN** `FetchAsync` is called with coordinates, tilt 30 and azimuth 180
- **THEN** the request URL SHALL contain the coordinates (invariant culture), the supplied variables, the tilt and azimuth parameters, `timeformat=unixtime` and `timezone=UTC`

#### Scenario: Missing values are null
- **WHEN** the response contains null entries for an hour
- **THEN** the mapped `SatelliteRadiationPoint` members for that hour SHALL be null

### Requirement: SatelliteRadiationPoint carries all irradiance components
`SatelliteRadiationPoint` in `Njord.Forecast/Solar` SHALL be a record implementing `IHasValidTime` with `GHI`, `DirectRadiation`, `DiffuseRadiation`, `DNI` and `GTI`, all `double?`.

#### Scenario: Response mapping
- **WHEN** a response with N hourly timestamps is mapped
- **THEN** N points SHALL result with `ValidFrom` taken from the timestamps and each component from the matching variable

### Requirement: Satellite service fetches hourly and stores the result
The `SatelliteRadiationService` SHALL be a hosted service that, only while `EnrichmentOptions.SatelliteRadiation` is true, fetches satellite radiation once per configured interval (default 60 minutes) per location with coordinates, passing `SolarYieldOptions.Tilt` and `Azimuth`, and writes the points to `ISatelliteRadiationStore`. All timing SHALL use `TimeProvider`.

#### Scenario: Hourly fetch
- **WHEN** the toggle is enabled and the interval elapses
- **THEN** exactly one request per location SHALL be sent and the store SHALL contain the returned points

#### Scenario: Disabled toggle
- **WHEN** the toggle is false
- **THEN** no request SHALL be sent

#### Scenario: Failure keeps previous data
- **WHEN** a fetch fails after an earlier success
- **THEN** the store SHALL keep the previous points and the service SHALL retry after 15 minutes

#### Scenario: Location without coordinates
- **WHEN** a location has no resolved coordinates
- **THEN** the service SHALL skip it and log a warning

### Requirement: Store holds recent satellite data per location
`ISatelliteRadiationStore` in `Njord.Core/Domain` SHALL provide thread-safe in-memory storage keyed by location, overwritten per fetch.

#### Scenario: Overwrite
- **WHEN** a second fetch result is stored for the same location
- **THEN** reads SHALL return only the second result

### Requirement: SolarYield blends satellite and ensemble data
`SolarYieldHandler` SHALL use satellite values for hours up to the latest satellite timestamp not after the current time and ensemble values for later hours. For satellite hours it SHALL use GTI directly and SHALL NOT apply the orientation factor. Satellite data older than 3 hours SHALL be ignored.

#### Scenario: Blend at transition
- **WHEN** satellite data covers hours up to 10:00 and the current time is 10:30
- **THEN** hours up to 10:00 SHALL come from satellite GTI and later hours from the ensemble forecast

#### Scenario: No satellite data
- **WHEN** the store is empty or the toggle is disabled
- **THEN** the handler output SHALL equal the current ensemble-only output

#### Scenario: Stale satellite data
- **WHEN** the newest satellite point is older than 3 hours
- **THEN** the handler SHALL use ensemble data only
