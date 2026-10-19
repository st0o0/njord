## Why

The pipeline's BroadcastHub and MergeHub are already PreMaterialized (separate from the analysis graph), yet they die when the pipeline actor restarts because `OnAnalysisGraphEnded` deliberately throws to trigger a BackoffSupervisor restart. This kills the materializer, which kills the hubs, which kills all consumer SourceRefs, which requires StreamConsumerActor — a 200-line framework for re-attaching consumers with exponential backoff.

The restart is wrong: an analysis bug is still there after restart, so the bug triggers again → restart loop. Resume (skip the failing event, log as Error, preserve accumulated state) is the correct behavior. With Resume-all and graph re-materialization instead of actor restart, the hubs never die, consumers subscribe once, and StreamConsumerActor is eliminated entirely.

## What Changes

- **StreamSupervision**: Resume ALL exceptions (not just known transient ones). Log non-transient as Error. A skipped event is better than a restart loop.
- **ForecastPipeline**: Split materialization into `MaterializeHubs()` (once in PreStart) and `MaterializeAnalysisGraph()` (PreStart + recovery). On analysis graph end: re-materialize the graph connecting to the same hubs, don't throw.
- **Remove BackoffSupervisor** from pipeline registration (pipeline never needs to restart).
- **Delete StreamConsumerActor** entirely (~200 lines).
- **GrpcConsumer**: Inherit `ReceiveActor` directly, one-shot attach in PreStart. No backoff, no retry.
- **MqttStatePublisher**: Same — `ReceiveActor`, one-shot attach.
- **Remove re-attach messages** from StreamWiringMessages if unused.

## Non-goals

- Not changing OpenMeteoGateway's internal re-attach (its forecast branch to the MergeHub SinkRef is a separate concern — the gateway CAN restart independently and the MergeHub hands out new SinkRefs).
- Not changing PollWorker or PollManager.
- Not changing the MergeHub/BroadcastHub topology itself.

## Capabilities

### New Capabilities

- `immortal-hubs`: Pipeline hubs survive analysis failures through Resume-all and graph re-materialization.

### Modified Capabilities

_(none — same external behavior, simpler internals)_

## Impact

- Njord.Core: StreamSupervision simplified, StreamConsumerActor deleted
- Njord.Pipeline: ForecastPipeline refactored, PipelineSetupContainer simplified
- Njord.Grpc: GrpcConsumer simplified (ReceiveActor, no base class)
- Njord.Mqtt: MqttStatePublisher simplified (ReceiveActor, no base class)
- Tests: StreamConsumerActor specs deleted, consumer re-attach specs deleted/simplified, new analysis-graph-recovery spec
- Net: ~400+ lines deleted
