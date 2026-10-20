## 1. StreamSupervision: Resume all

- [x] 1.1 Changed decider to Resume on ALL exceptions. Non-transient log as Error, transient as Warning.
- [x] 1.2 Updated StreamSupervisionSpec: all exceptions resume, non-transient log Error, transient log Warning.

## 2. ForecastPipeline: split materialization, remove throw

- [x] 2.1 Read ForecastPipeline.cs
- [x] 2.2 Split into MaterializeHubs() (once in PreStart) and MaterializeAnalysisGraph() (PreStart + on graph end)
- [x] 2.3 OnAnalysisGraphEnded: log Warning, call MaterializeAnalysisGraph() + RequestCurrentInputs() instead of throwing
- [x] 2.4 Removed BackoffSupervisor from PipelineSetupContainer — plain Props

## 3. Delete StreamConsumerActor

- [x] 3.1 Deleted StreamConsumerActor.cs
- [x] 3.2 Deleted StreamConsumerActorSpec.cs
- [x] 3.3 Removed njord_stream_consumer_rewires_total counter from Core Telemetry + its TelemetrySpec test
- [x] 3.4 Build clean after removal

## 4. Simplify GrpcConsumer

- [x] 4.1 Changed to inherit ReceiveActor directly
- [x] 4.2 One-shot attach in PreStart: ActorRegistry.GetAsync → PipeTo → Tell(RequestPipelineOutput) → OnSourceRef
- [x] 4.3 OnSourceRef: materialize graph with SourceRef via Sink.ActorRef(Self)
- [x] 4.4 Removed all backoff, retry, KillSwitch, stable-timer logic
- [x] 4.5 BroadcastHub for gRPC fan-out and point query handlers unchanged

## 5. Simplify MqttStatePublisher

- [x] 5.1 Changed to inherit ReceiveActor directly
- [x] 5.2 One-shot attach in PreStart (same pattern as GrpcConsumer)
- [x] 5.3 Removed all backoff, retry, KillSwitch logic
- [x] 5.4 MQTT publishing sink logic unchanged

## 6. Clean up messages and references

- [x] 6.1 StreamWiringMessages: no removals needed (messages are still used for the one-shot handshake)
- [x] 6.2 Updated AGENTS.md: removed StreamConsumerActor references, updated consumer description

## 7. Update tests

- [x] 7.1 Pipeline tests pass (including GatewayRestartSpec which was previously failing)
- [x] 7.2 Deleted GrpcConsumerRewireSpec (no more re-attach)
- [x] 7.3 Renamed MqttStateRewireSpec → MqttStateDedupSpec, removed 4 re-attach tests, kept 2 dedup tests
- [x] 7.4 GatewayRestartSpec passes (gateway restart ≠ pipeline restart, consumer stays attached)

## 8. Validation

- [x] 8.1 `dotnet build Njord.slnx` — 0 errors, 0 warnings
- [x] 8.2 All 19 test projects pass (1665 total, 0 failures)
- [x] 8.3 Architecture tests: 122 pass
- [x] 8.4 `dotnet format Njord.slnx --verify-no-changes` — clean
- [x] 8.5 Updated AGENTS.md test counts: 1711→1665
