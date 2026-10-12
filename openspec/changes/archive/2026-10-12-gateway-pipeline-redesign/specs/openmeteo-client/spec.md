## MODIFIED Requirements

### Requirement: IOpenMeteoClient and OpenMeteoClient live in Njord.OpenMeteo
The `IOpenMeteoClient` interface and `OpenMeteoClient` implementation SHALL reside in the `Njord.OpenMeteo` project (renamed from `Njord.Ingest`). The `IOpenMeteoClient` interface SHALL be `public` (for test access via `FakeOpenMeteoClient` in `Njord.Tests.Shared`). The `Njord.Core.Ingest` namespace SHALL be removed — no Open-Meteo-specific interfaces SHALL live in Core.

#### Scenario: IOpenMeteoClient is public in Njord.OpenMeteo
- **WHEN** `Njord.OpenMeteo` is inspected
- **THEN** `IOpenMeteoClient` SHALL be a public interface in the `Njord.OpenMeteo` namespace

#### Scenario: Njord.Core has no Ingest namespace
- **WHEN** `Njord.Core` is searched for files in an `Ingest/` directory
- **THEN** no files SHALL be found

#### Scenario: FakeOpenMeteoClient references Njord.OpenMeteo
- **WHEN** `Njord.Tests.Shared` references `FakeOpenMeteoClient`
- **THEN** it SHALL reference the `Njord.OpenMeteo` project for `IOpenMeteoClient`

### Requirement: Forecast fetch per location and model
The client SHALL fetch `GET {OpenMeteoBaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}` where `OpenMeteoBaseUrl` is read from `NjordOptions.OpenMeteoBaseUrl` (default `https://api.open-meteo.com`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL always include `wind_speed_unit=ms`, `timeformat=unixtime`, `timezone=auto`, and `forecast_days` from configuration. No authentication header or API key SHALL be sent. The `FetchAsync` method SHALL accept `LocationOptions`, `WeatherModel`, `CycleId`, and `CancellationToken` parameters. The `CycleId` parameter is retained for tracing purposes.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu`
- **THEN** the client returns `PollOutcome.Success` with a `Forecast` whose hourly series contains points including the +72 h horizon

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

## RENAMED Requirements

### Requirement: Njord.Ingest project renamed to Njord.OpenMeteo
- **FROM:** `Njord.Ingest` project, `Njord.Ingest` namespace
- **TO:** `Njord.OpenMeteo` project, `Njord.OpenMeteo` namespace

### Requirement: IngestSetupContainer renamed to OpenMeteoSetupContainer
- **FROM:** `IngestSetupContainer`
- **TO:** `OpenMeteoSetupContainer`

### Requirement: Njord.Ingest.Tests renamed to Njord.OpenMeteo.Tests
- **FROM:** `Njord.Ingest.Tests` project
- **TO:** `Njord.OpenMeteo.Tests` project
