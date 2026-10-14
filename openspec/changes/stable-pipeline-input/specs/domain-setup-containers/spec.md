## MODIFIED Requirements

### Requirement: OpenMeteoSetupContainer registers the client and the gateway
`OpenMeteoSetupContainer` (src/Njord.OpenMeteo/Configuration) SHALL bind and validate `OpenMeteoOptions`, register `IBudgetProvider` (`OptionsBudgetProvider`), register the fetch timeouts (admission 20 s, HTTP 30 s, reply 60 s) unless already registered, register `IOpenMeteoClient` as a typed `HttpClient` whose base address comes from `OpenMeteoOptions.OpenMeteoBaseUrl`, with the HTTP fetch timeout (30 seconds) and an IPv4-only socket connect callback, and register an `IActorRegistration` that creates `OpenMeteoGateway` as a cluster singleton under `IForecastGateway`, wrapped in a backoff-on-failure supervisor (3 s to 30 s, unlimited retries).

#### Scenario: OpenMeteoClient is resolvable
- **WHEN** `OpenMeteoSetupContainer.SetupServices` is called
- **THEN** `IOpenMeteoClient` is resolvable from the service provider with IPv4-forced socket handling

#### Scenario: Gateway is a supervised singleton
- **WHEN** the actor system starts
- **THEN** `OpenMeteoGateway` runs as a singleton resolvable through `IForecastGateway`

### Requirement: PipelineSetupContainer registers scheduling, pipeline and read-model actors
`PipelineSetupContainer` (src/Njord.Pipeline/Configuration) SHALL bind and validate `PollingOptions` and `ConsensusOptions`, register the startup `AnalysisSettings` built by `AnalysisSettingsFactory` from the validated options, register the fetch timeouts unless already registered, and register an `IActorRegistration` that creates three cluster singletons: `PollScheduler` under `IPollScheduler` (inside a `BackoffSupervisor`, 3 s to 30 s, unlimited retries), `ForecastPipeline` under `IForecastPipeline` (inside a backoff-on-failure supervisor, 3 s to 30 s, unlimited retries) and `ForecastReadModel` under `IForecastReadModel`. It SHALL NOT register a CoordinatedShutdown stream-stop task.

#### Scenario: Pipeline actors are declared via IActorRegistration
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** it registers an `IActorRegistration` that configures `PollScheduler`, `ForecastPipeline` and `ForecastReadModel`

#### Scenario: Startup analysis settings are registered
- **WHEN** `ForecastPipeline` is created
- **THEN** it receives the `AnalysisSettings` built from the validated analysis options

#### Scenario: Pipeline is a supervised singleton
- **WHEN** the analysis graph of `ForecastPipeline` ends and the pipeline fails
- **THEN** its supervisor restarts it after the backoff delay and it stays resolvable through `IForecastPipeline`
