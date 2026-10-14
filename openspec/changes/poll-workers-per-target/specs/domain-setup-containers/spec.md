## MODIFIED Requirements

### Requirement: PipelineSetupContainer registers scheduling, pipeline and read-model actors
`PipelineSetupContainer` (src/Njord.Pipeline/Configuration) SHALL bind and validate `PollingOptions` and `ConsensusOptions`, register the startup `AnalysisSettings` built by `AnalysisSettingsFactory` from the validated options, register the fetch timeouts unless already registered, and register an `IActorRegistration` that creates the poll shard region `poll-worker` of `PollWorker` entities under `IPollRegion` (shared `ShardMessageExtractor`, no passivation, no remember-entities) before three cluster singletons: `PollManager` under `IPollManager` (singleton name `poll-manager`, inside a backoff-on-failure supervisor, 3 s to 30 s, unlimited retries), `ForecastPipeline` under `IForecastPipeline` (inside a backoff-on-failure supervisor, 3 s to 30 s, unlimited retries) and `ForecastReadModel` under `IForecastReadModel`. It SHALL NOT register a CoordinatedShutdown stream-stop task.

#### Scenario: Pipeline actors are declared via IActorRegistration
- **WHEN** `PipelineSetupContainer.SetupServices` is called
- **THEN** it registers an `IActorRegistration` that configures the poll shard region, `PollManager`, `ForecastPipeline` and `ForecastReadModel`

#### Scenario: Startup analysis settings are registered
- **WHEN** `ForecastPipeline` is created
- **THEN** it receives the `AnalysisSettings` built from the validated analysis options

#### Scenario: Pipeline is a supervised singleton
- **WHEN** the analysis graph of `ForecastPipeline` ends and the pipeline fails
- **THEN** its supervisor restarts it after the backoff delay and it stays resolvable through `IForecastPipeline`

#### Scenario: Polling runs in a single-node cluster
- **WHEN** the polling registration runs in an actor system that forms a single-node cluster with persistence
- **THEN** the manager starts a worker for every configured target in the shard region, each target is fetched, and the manager's `QueryPollStates` reports its last change
