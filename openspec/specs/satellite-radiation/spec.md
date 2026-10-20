# satellite-radiation Specification

## Purpose
TBD - created by archiving change add-satellite-radiation. Update Purpose after archive.
## Requirements
### Requirement: Satellite is a fetched domain
The system SHALL provide `DomainKind.Satellite` with hourly `SatelliteRadiationPoint`s and a `SatelliteRadiationClient` (`IDomainClient`) in `Njord.OpenMeteo` that fetches `satellite-api.open-meteo.com/v1/archive` with the variables of the resolved requirements. The gateway SHALL dispatch `FetchDomain` for `Satellite` to the client and answer with exactly one `FetchDomainCompleted`, `FetchDomainFailed` or `FetchDomainRejected`.

#### Scenario: Request contains orientation
- **WHEN** `FetchAsync` is called with coordinates, the variables, tilt 25 and azimuth 135 (compass degrees) in `SolarYieldOptions` and 6 past hours
- **THEN** the URL SHALL contain the coordinates (invariant culture), the hourly variables, `tilt=25`, `azimuth=-45`, `past_hours=6`, `timeformat=unixtime` and `timezone=UTC`

#### Scenario: Gateway delivers satellite points
- **WHEN** the gateway receives `FetchDomain` for `Satellite`
- **THEN** it SHALL call only the satellite client and deliver a `DomainForecast<SatelliteRadiationPoint>` with `TimeResolution.Hourly`

#### Scenario: Missing values are null
- **WHEN** the response contains null entries for an hour
- **THEN** the mapped `SatelliteRadiationPoint` members for that hour SHALL be null

### Requirement: SatelliteRadiationPoint carries all irradiance components
`SatelliteRadiationPoint` in `Njord.Forecast/Solar` SHALL be a record implementing `IHasValidTime` with `GHI`, `DirectRadiation`, `DiffuseRadiation`, `DNI` and `GTI`, all `double?`, and `HasFields(SatelliteParams)`.

#### Scenario: Response mapping
- **WHEN** a response with N hourly timestamps is mapped
- **THEN** N points SHALL result with `ValidFrom` taken from the timestamps and each component from the matching variable

### Requirement: Satellite is polled only when needed
The Satellite domain SHALL be required only when the `SatelliteRadiation` feature is enabled (default off) and the PollWorker SHALL learn its update cycle like every other domain. `BudgetGuard` SHALL count it once per poll cycle like the other hourly domains.

#### Scenario: Default off
- **WHEN** no configuration is supplied
- **THEN** `SatelliteRadiation` SHALL be disabled and no satellite request SHALL be made

#### Scenario: Budget projection
- **WHEN** two locations require only the Satellite domain with a 60 minute poll interval
- **THEN** the projected monthly calls SHALL be 1440

### Requirement: Satellite options
`Njord:Satellite` SHALL expose `PastHours` (default 6) and the validator SHALL reject values outside 1..48.

#### Scenario: Invalid past hours
- **WHEN** `PastHours` is 0 or 49
- **THEN** validation SHALL fail with "PastHours must be between 1 and 48."

### Requirement: Satellite and ensemble irradiance are blended
`SolarIrradianceBlend` SHALL use satellite GTI for hours up to the newest satellite observation not after the current time and ensemble GTI for later hours. Satellite data whose newest observation is older than 3 hours SHALL be ignored. `SolarYieldHandler` SHALL use the blend, so without satellite data its output equals the ensemble-only output.

#### Scenario: Blend at transition
- **WHEN** satellite data covers the current hour and earlier and the ensemble covers the next 24 hours
- **THEN** the current hour SHALL come from satellite GTI and later hours from the ensemble forecast

#### Scenario: No satellite data
- **WHEN** the forecast has no satellite data
- **THEN** the SolarYield output SHALL equal the ensemble-only output

#### Scenario: Stale satellite data
- **WHEN** the newest satellite point is older than 3 hours
- **THEN** the handler SHALL use ensemble data only

### Requirement: SatelliteRadiation feature reports observed irradiance
`SatelliteRadiationHandler` SHALL emit `SatelliteRadiationComputed` with the newest observed hour's GTI, optional GHI and, when ensemble GTI is available, the blended 24-hour irradiation in kWh/m2. It SHALL emit nothing without a usable observation. The event SHALL be exposed as lifestyle update `satellite_radiation`.

#### Scenario: Observed hour reported
- **WHEN** the satellite domain holds an observation for the current hour
- **THEN** the event SHALL carry that hour as `ObservedAt` and its GTI

#### Scenario: Nothing usable
- **WHEN** the satellite data is empty, stale or has no GTI
- **THEN** no event SHALL be emitted

### Requirement: Satellite domain on the wire
The gRPC contract SHALL define `DOMAIN_SATELLITE` and a `SatelliteSample` with optional components; missing components SHALL stay unset.

#### Scenario: Missing component
- **WHEN** a `SatelliteRadiationPoint` has only GTI
- **THEN** the sample SHALL have GTI set and GHI and DNI unset

