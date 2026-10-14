## REMOVED Requirements

### Requirement: The pipeline graph is materialized by an actor
**Reason**: `PipelineActor` no longer exists. Its responsibilities are split between `OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs: fetch queue, throttle, fetch, scheduler feedback, `BroadcastHub<Forecast>`) and `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs: analysis stream and `BroadcastHub<PipelineEvent>`). `ScheduledPoll` is handled by `PollScheduler`, there is no `FetchOutcome`, `WeightedTarget`, `HashResult`, `StopStreams` or `UniqueKillSwitch`.
**Migration**: See `poll-pipeline` for the gateway fetch stream and `pipeline-composition` for the analysis stream.

### Requirement: PipelineActor receives ScheduledPoll via Tell and broadcasts FetchOutcome
**Reason**: `PipelineActor` no longer exists. Its responsibilities are split between `OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs: fetch queue, throttle, fetch, scheduler feedback, `BroadcastHub<Forecast>`) and `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs: analysis stream and `BroadcastHub<PipelineEvent>`). `ScheduledPoll` is handled by `PollScheduler`, there is no `FetchOutcome`, `WeightedTarget`, `HashResult`, `StopStreams` or `UniqueKillSwitch`.
**Migration**: See `poll-pipeline` for the gateway fetch stream and `pipeline-composition` for the analysis stream.

### Requirement: PipelineActor materializes a BroadcastHub for FetchOutcome distribution
**Reason**: `PipelineActor` no longer exists. Its responsibilities are split between `OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs: fetch queue, throttle, fetch, scheduler feedback, `BroadcastHub<Forecast>`) and `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs: analysis stream and `BroadcastHub<PipelineEvent>`). `ScheduledPoll` is handled by `PollScheduler`, there is no `FetchOutcome`, `WeightedTarget`, `HashResult`, `StopStreams` or `UniqueKillSwitch`.
**Migration**: See `poll-pipeline` for the gateway fetch stream and `pipeline-composition` for the analysis stream.

### Requirement: The pipeline actor materializes the feedback consumer locally
**Reason**: `PipelineActor` no longer exists. Its responsibilities are split between `OpenMeteoGateway` (src/Njord.OpenMeteo/OpenMeteoGateway.cs: fetch queue, throttle, fetch, scheduler feedback, `BroadcastHub<Forecast>`) and `ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs: analysis stream and `BroadcastHub<PipelineEvent>`). `ScheduledPoll` is handled by `PollScheduler`, there is no `FetchOutcome`, `WeightedTarget`, `HashResult`, `StopStreams` or `UniqueKillSwitch`.
**Migration**: See `poll-pipeline` for the gateway fetch stream and `pipeline-composition` for the analysis stream.
