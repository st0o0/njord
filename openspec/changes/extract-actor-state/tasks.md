## 1. GrpcConsumer state extraction

- [x] 1.1 Create `GrpcConsumerState` sealed record in `src/Njord.Grpc/`
- [x] 1.2 Create `GrpcConsumerStateExtensions` with Apply methods for all event types
- [x] 1.3 Fix: Apply(AlertRaised) handles ALL AlertTypes (Weather + AQ + Flood)
- [x] 1.4 Refactor GrpcConsumer to thin shell using `_state = _state.Apply(...)`
- [x] 1.5 Add `GrpcConsumerStateSpec` with pure state tests
- [x] 1.6 Update existing GrpcConsumer actor tests

## 2. MqttConnection state extraction

- [x] 2.1 Create `MqttConnectionState` sealed record
- [x] 2.2 Move `_isConnected`, `_disconnectedSince`, `_connectAttempts`, `_onlineAttempts` into record
- [x] 2.3 Create Extensions with Apply methods for connect/disconnect/failure
- [x] 2.4 Refactor MqttConnection actor to thin shell
- [x] 2.5 Add pure state tests

## 3. PollWorker round state

- [x] 3.1 Create `PollRoundState` record for `_roundId`, `_domainResults`, `_pendingDomains`
- [x] 3.2 Add to PollWorkerState as nullable property (null = no round in flight)
- [x] 3.3 Update PollWorker to use state record for round management

## 4. ProjectionEngine extraction

- [x] 4.1 Extract `ProjectionEngine.Evaluate` to a static pure method
- [x] 4.2 Add direct unit tests for Evaluate (no Akka needed)

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx`
- [x] 5.2 All test projects pass
- [x] 5.3 GrpcConsumer correctly caches AQ and Flood alerts (verify with test)
