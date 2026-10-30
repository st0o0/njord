## ADDED Requirements

### Requirement: Panel orientation lives in SolarOptions

`SolarOptions` SHALL be a sealed config class implementing `IConfigSection` with `SectionName` `Njord:Solar`, exposing `Tilt` (default 30, range 0..90 degrees) and `Azimuth` (default 180, compass degrees, range 0..360). `SolarOptionsValidator` SHALL reject values outside these ranges at startup.

#### Scenario: Tilt default
- **WHEN** no `Njord:Solar:Tilt` value is configured
- **THEN** `SolarOptions.Tilt` SHALL be 30

#### Scenario: Azimuth default
- **WHEN** no `Njord:Solar:Azimuth` value is configured
- **THEN** `SolarOptions.Azimuth` SHALL be 180

#### Scenario: Tilt out of range
- **WHEN** `Tilt` is -1 or 91
- **THEN** startup validation SHALL fail with a message naming `Tilt` and the range 0..90

#### Scenario: Azimuth out of range
- **WHEN** `Azimuth` is -1 or 361
- **THEN** startup validation SHALL fail with a message naming `Azimuth` and the range 0..360

#### Scenario: Boundary values accepted
- **WHEN** `Tilt` is 0 or 90 and `Azimuth` is 0 or 360
- **THEN** validation SHALL succeed

### Requirement: Forecast and Ensemble clients use SolarOptions for GTI

`ForecastClient` and `EnsembleClient` SHALL read tilt and azimuth from `IOptions<SolarOptions>` when global tilted irradiance is requested. The URL SHALL contain `tilt=<value>` and `azimuth=<converted>` where the conversion is compass-to-math (subtract from 180).

#### Scenario: Forecast client with GTI
- **WHEN** the Forecast client builds a request that includes global tilted irradiance and `SolarOptions` has Tilt 25 and Azimuth 135
- **THEN** the URL SHALL contain `tilt=25` and `azimuth=-45`

#### Scenario: Ensemble client with GTI
- **WHEN** the Ensemble client builds a request that includes global tilted irradiance and `SolarOptions` has Tilt 25 and Azimuth 135
- **THEN** the URL SHALL contain `tilt=25` and `azimuth=-45`

### Requirement: SolarOptions lives in Njord.OpenMeteo

`SolarOptions` and `SolarOptionsValidator` SHALL live in `Njord.OpenMeteo/Configuration/` because only the OpenMeteo clients consume them. Registration uses `AddNjordOptions<SolarOptions, SolarOptionsValidator>()`.

#### Scenario: Registration
- **WHEN** the host starts
- **THEN** `SolarOptions` SHALL be available via DI and validated on startup
