## ADDED Requirements

### Requirement: One configuration section per concern
Each options type SHALL bind its own section; no two options types SHALL bind the same section. The sections SHALL be `Njord` (forecast scope), `Njord:Polling`, `Njord:OpenMeteo`, `Njord:Storage`, `Njord:Persistence`, `Njord:Mqtt`, `Njord:Sensors` and `Njord:Enrichment:<Feature>`.

#### Scenario: Poll interval in its own section
- **WHEN** `Njord__Polling__PollInterval=00:30:00` is set
- **THEN** `PollingOptions.PollInterval` is 30 minutes and `NjordOptions` exposes no such key

#### Scenario: Open-Meteo base URL
- **WHEN** `Njord:OpenMeteo:BaseUrl` is set to `http://localhost:8080`
- **THEN** `OpenMeteoOptions.BaseUrl` is `http://localhost:8080`

#### Scenario: Section uniqueness is enforced
- **WHEN** the architecture spec lists the `SectionName` of every options type
- **THEN** all values are distinct

### Requirement: Options are registered through one helper
`Njord.Core` SHALL provide `AddNjordOptions<T>(configuration)` that binds `Bind(configuration.GetSection(T.SectionName))`, replaces collection defaults, registers the type's `IValidateOptions<T>` and enables `ValidateOnStart`. Setup containers SHALL NOT repeat these steps by hand.

#### Scenario: Helper registers validation
- **WHEN** an options type is added with `AddNjordOptions<T>` and its value is invalid
- **THEN** the host fails at start with the validator's messages

### Requirement: Hot and restart-required options are read differently
Options that `ConfigReloadPolicy` marks `Hot` SHALL NOT be injected as `IOptions<T>`; options marked `RestartRequired` SHALL NOT be injected as `IOptionsMonitor<T>`. `ConfigView` and `ConfigManager` are the only allowed monitor consumers of restart-required options for change reporting.

#### Scenario: A hot option read through IOptions
- **WHEN** a production class injects `IOptions<PollingOptions>`
- **THEN** the architecture spec fails

#### Scenario: A restart-required option read through the monitor
- **WHEN** a class other than `ConfigView` or `ConfigManager` injects `IOptionsMonitor<MqttOptions>`
- **THEN** the architecture spec fails

### Requirement: Legacy root-level keys fail loudly
When configuration or the environment contains `Njord:PollInterval`, `Njord:DiscoveryInterval`, `Njord:BudgetOverride`, `Njord:OpenMeteoBaseUrl` or `Njord:PersistencePath`, startup validation SHALL fail with a message naming the old key and its new location. The override file SHALL be migrated in place when it contains old keys: the values move to the new sections, a warning is logged, and a corrupt file keeps its existing resilience behaviour.

#### Scenario: Old environment variable
- **WHEN** `Njord__PollInterval=00:30:00` is set
- **THEN** startup fails with a message that names `Njord:PollInterval` and `Njord:Polling:PollInterval`

#### Scenario: Old override file
- **WHEN** the override file contains `"PollInterval": "00:45:00"` at the `Njord` level
- **THEN** the effective value is read from `Njord:Polling:PollInterval` and the file is rewritten with the new key on the next write

### Requirement: Example defaults match appsettings.json
Every `# Default:` value in `docker-compose.example.yml` SHALL equal the value of the same key in `appsettings.json` (or the code default for list-valued options). A spec SHALL fail on any mismatch.

#### Scenario: Stale default in the compose example
- **WHEN** the compose example says `# Default: 30` and `appsettings.json` holds 60 minutes
- **THEN** the spec fails and names the variable
