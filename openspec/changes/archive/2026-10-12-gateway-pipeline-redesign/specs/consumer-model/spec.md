## MODIFIED Requirements

### Requirement: Consumers attach via SourceRef to pipeline BroadcastHub
Each consumer SHALL request a `SourceRef<PipelineEvent>` from `ForecastPipeline` (renamed from `PipelineActor`) and run its own sink. There is exactly one hop from pipeline to consumer — no intermediate relay actors, no intermediate BroadcastHubs. The consumer SHALL resolve the pipeline via `Context.GetActor<IForecastPipeline>()`.

#### Scenario: MQTT consumer attaches
- **WHEN** `MqttState` (renamed from `MqttStateActor`) starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from `ForecastPipeline` and materializes its own sink that publishes state to MQTT

#### Scenario: gRPC consumer attaches
- **WHEN** `GrpcConsumer` (renamed from `GrpcConsumerActor`) starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from `ForecastPipeline` and materializes its own sink that updates in-memory state and feeds its secondary BroadcastHub

#### Scenario: Consumer independence
- **WHEN** any consumer project is inspected
- **THEN** it does not reference other consumer projects — consumers are independent output adapters

## REMOVED Requirements

### Requirement: StopStreams/StreamsStopped shutdown coordination
**Reason**: The `StopStreams`/`StreamsStopped` message pattern and `CoordinatedShutdown` task are removed. Akka.NET's actor-scoped materializer handles stream cleanup automatically when the actor stops. The `AbruptTerminationException` during shutdown is cosmetic and can be suppressed via Akka logger config if needed.
**Migration**: Remove `StopStreams`, `StreamsStopped` messages, `KillSwitch` usage, `AddStreamShutdownTask` in `PipelineSetupContainer`, and `PipelineActorShutdownSpec`.
