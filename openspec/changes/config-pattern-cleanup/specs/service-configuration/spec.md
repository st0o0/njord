## MODIFIED Requirements

### Requirement: Weather forecast options bind to Njord:Weather
The weather forecast configuration (forecast days, ensemble models, past hours) SHALL bind to the section `Njord:Weather` (not `Njord:Forecast`). The options class SHALL be named `WeatherOptions`.

#### Scenario: Environment variable naming
- **WHEN** a user configures forecast days via environment variable
- **THEN** the variable SHALL be `Njord__Weather__ForecastDays`

#### Scenario: JSON config naming
- **WHEN** a user configures weather settings in appsettings.json
- **THEN** the section SHALL be `"Njord": { "Weather": { "ForecastDays": 7 } }`

#### Scenario: WeatherOptions.Models implements array defaults
- **WHEN** no models are configured
- **THEN** `ApplyDefaults()` SHALL fill `Models` with the single default model id

#### Scenario: User-provided models preserved
- **WHEN** models are explicitly configured
- **THEN** `ApplyDefaults()` SHALL not overwrite them

### Requirement: Configuration layering follows ASP.NET conventions
The `appsettings.json` file SHALL contain only production-appropriate settings (logging levels). It SHALL NOT contain a `Njord:` section — all option defaults SHALL be defined in the Options classes. The `appsettings.Development.json` file SHALL contain dev-specific overrides (debug logging, test locations, MQTT disabled). It SHALL be loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`.

#### Scenario: Dev overrides use correct section name
- **WHEN** `appsettings.Development.json` contains `"Njord": { "Weather": { "ForecastDays": 16 } }`
- **THEN** the override SHALL take effect because the section name matches `WeatherOptions.SectionName`

#### Scenario: Docker image starts with no Njord config
- **WHEN** the service starts in Production environment with no `Njord:` configuration
- **THEN** all Options properties use their code defaults and startup validation rejects the empty locations list with a clear message
