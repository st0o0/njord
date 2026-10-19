## MODIFIED Requirements

### Requirement: Request budget defaults to the Open-Meteo free tier
The system SHALL resolve a request budget of 300,000 requests/month and
600 requests/minute (Open-Meteo free-tier soft limits) when no explicit
budget is configured. All throttling and validation SHALL consume the
resolved budget.

#### Scenario: Default budget without configuration
- **WHEN** no budget is configured
- **THEN** the resolved budget is 300,000 requests/month and 600 requests/minute

### Requirement: Budget override supersedes the preset
The system SHALL accept an optional budget override (requests/month,
requests/minute) via environment variables or appsettings.json that replaces the default free-tier values entirely. Runtime budget changes via gRPC are no longer supported.

#### Scenario: Override wins over default
- **WHEN** an override of 50,000 requests/month and 60 requests/minute is configured via environment variables
- **THEN** the resolved budget is 50,000 requests/month and 60 requests/minute

### Requirement: All options are read via IOptions at startup
All options classes SHALL be read via `IOptions<T>` (not `IOptionsMonitor<T>`) since there is no runtime config change mechanism. The `ConfigReloadPolicy` distinction between Hot and RestartRequired no longer applies. `ConfigView` SHALL continue to provide read-only access to `NjordOptions` and `PollingOptions` for components that need scope and polling data.

#### Scenario: Options are fixed for process lifetime
- **WHEN** njord starts with a configured poll interval of 30 minutes
- **THEN** the poll interval remains 30 minutes until the process is restarted

#### Scenario: ConfigView provides scope and polling
- **WHEN** PollManager reads ConfigView at startup
- **THEN** it receives the current NjordOptions and PollingOptions values
