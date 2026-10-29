## ADDED Requirements

### Requirement: Satellite options carry panel orientation
`Njord:Satellite` (`SatelliteOptions`) SHALL expose `Tilt` (default 30, range 0..90 degrees) and `Azimuth` (default 180, compass degrees, range 0..360). These are API parameters for every request that asks for global tilted irradiance (Satellite, Forecast, Ensemble). `SatelliteOptionsValidator` SHALL reject values outside the ranges at startup.

#### Scenario: Tilt default
- **WHEN** no `Njord:Satellite:Tilt` value is configured
- **THEN** `SatelliteOptions.Tilt` SHALL be 30

#### Scenario: Azimuth default
- **WHEN** no `Njord:Satellite:Azimuth` value is configured
- **THEN** `SatelliteOptions.Azimuth` SHALL be 180

#### Scenario: Tilt out of range
- **WHEN** `Tilt` is -1 or 91
- **THEN** startup validation SHALL fail with a message naming `Tilt` and the range 0..90

#### Scenario: Azimuth out of range
- **WHEN** `Azimuth` is -1 or 361
- **THEN** startup validation SHALL fail with a message naming `Azimuth` and the range 0..360

#### Scenario: Boundary values accepted
- **WHEN** `Tilt` is 0 or 90 and `Azimuth` is 0 or 360
- **THEN** validation SHALL succeed

### Requirement: Forecast and Ensemble clients use the satellite orientation
`ForecastClient` and `EnsembleClient` SHALL read tilt and azimuth from `IOptions<SatelliteOptions>` when global tilted irradiance is requested. No class named `SolarYieldOptions` and no `Njord:SolarYield` section SHALL exist.

#### Scenario: Forecast client with GTI
- **WHEN** the Forecast client builds a request that includes global tilted irradiance and `SatelliteOptions` has Tilt 25 and Azimuth 135
- **THEN** the URL SHALL contain `tilt=25` and `azimuth=-45`

#### Scenario: Ensemble client with GTI
- **WHEN** the Ensemble client builds a request that includes global tilted irradiance and `SatelliteOptions` has Tilt 25 and Azimuth 135
- **THEN** the URL SHALL contain `tilt=25` and `azimuth=-45`

#### Scenario: Old section ignored
- **WHEN** `Njord:SolarYield:Tilt` is configured
- **THEN** it SHALL have no effect

## MODIFIED Requirements

### Requirement: Satellite is a fetched domain
The system SHALL provide `DomainKind.Satellite` with hourly `SatelliteRadiationPoint`s and a `SatelliteRadiationClient` (`IDomainClient`) in `Njord.OpenMeteo` that fetches `satellite-api.open-meteo.com/v1/archive` with the variables of the resolved requirements. The gateway SHALL dispatch `FetchDomain` for `Satellite` to the client and answer with exactly one `FetchDomainCompleted`, `FetchDomainFailed` or `FetchDomainRejected`.

#### Scenario: Request contains orientation
- **WHEN** `FetchAsync` is called with coordinates, the variables, tilt 25 and azimuth 135 (compass degrees) in `SatelliteOptions` and 6 past hours
- **THEN** the URL SHALL contain the coordinates (invariant culture), the hourly variables, `tilt=25`, `azimuth=-45`, `past_hours=6`, `timeformat=unixtime` and `timezone=UTC`

#### Scenario: Gateway delivers satellite points
- **WHEN** the gateway receives `FetchDomain` for `Satellite`
- **THEN** it SHALL call only the satellite client and deliver a `DomainForecast<SatelliteRadiationPoint>` with `TimeResolution.Hourly`

#### Scenario: Missing values are null
- **WHEN** the response contains null entries for an hour
- **THEN** the mapped `SatelliteRadiationPoint` members for that hour SHALL be null

### Requirement: Satellite options
`Njord:Satellite` SHALL expose `PastHours` (default 6), `Tilt` (default 30) and `Azimuth` (default 180). The validator SHALL reject `PastHours` outside 1..48, `Tilt` outside 0..90 and `Azimuth` outside 0..360.

#### Scenario: Invalid past hours
- **WHEN** `PastHours` is 0 or 49
- **THEN** validation SHALL fail with "PastHours must be between 1 and 48."

#### Scenario: Invalid orientation
- **WHEN** `Tilt` is 91 or `Azimuth` is 361
- **THEN** validation SHALL fail naming the offending property and its range
