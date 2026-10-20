## Why

ForecastReadModel in Njord.Pipeline is a workaround that duplicates state outside streams (volatile dict) and uses an unnecessary MergeHub (only 1 producer) plus Recover/Sink.Ignore hacks. It also lives in the wrong project — it's a gRPC adapter, not pipeline logic. Renaming to GrpcConsumer and moving to Njord.Grpc clarifies the architecture.

## What Changes

- Rename ForecastReadModel → GrpcConsumer, IForecastReadModel → IGrpcConsumer
- Move from Njord.Pipeline to Njord.Grpc
- Replace volatile state dict with actor state: pipeline events flow via Sink.ActorRef(Self) → Receive<PipelineEvent> updates regular fields on the actor thread
- Replace MergeHub→BroadcastHub with Source.ActorRef→BroadcastHub (only 1 producer, MergeHub unnecessary)
- Remove Recover hack (stream errors handled by StreamConsumerActor base class re-attach)
- Keep BroadcastHub for gRPC client fan-out (intentional design)
- Keep Sink.Ignore drain (BroadcastHub requirement)
- Update RequestReadModelStream* → RequestGrpcStream* messages

## Non-goals

- No changes to ForecastPipeline or its BroadcastHub
- No changes to MqttStatePublisher
- No changes to gRPC service query logic (just updated references)

## Capabilities

### New Capabilities

- `grpc-consumer`: Clean actor-based gRPC consumer with in-actor state and direct BroadcastHub fan-out

### Modified Capabilities

_(none)_

## Impact

- Njord.Pipeline: ForecastReadModel.cs removed, PipelineSetupContainer updated
- Njord.Grpc: GrpcConsumer.cs added, registration added
- Njord.Core: ActorKeys.cs (marker rename), StreamWiringMessages.cs (message rename)
- Tests: ForecastReadModelSpec → GrpcConsumerSpec, simplified (no hub testing)
- AGENTS.md: references updated
