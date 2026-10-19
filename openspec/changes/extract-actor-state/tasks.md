## 1. GrpcConsumer state extraction

- [ ] 1.1 Create `GrpcConsumerState` sealed record in `src/Njord.Grpc/`
- [ ] 1.2 Create `GrpcConsumerStateExtensions` with Apply methods for all event types
- [ ] 1.3 Fix: Apply(AlertRaised) handles ALL AlertTypes (Weather + AQ + Flood)
- [ ] 1.4 Refactor GrpcConsumer to thin shell using `_state = _state.Apply(...)`
- [ ] 1.5 Add `GrpcConsumerStateSpec` with pure state tests
- [ ] 1.6 Update existing GrpcConsumer actor tests

## 2. MqttConnection state extraction

- [ ] 2.1 Create `MqttConnectionState` sealed record
- [ ] 2.2 Move `_isConnected`, `_disconnectedSince`, `_connectAttempts`, `_onlineAttempts` into record
- [ ] 2.3 Create Extensions with Apply methods for connect/disconnect/failure
- [ ] 2.4 Refactor MqttConnection actor to thin shell
- [ ] 2.5 Add pure state tests

## 3. PollWorker round state

- [ ] 3.1 Create `PollRoundState` record for `_roundId`, `_domainResults`, `_pendingDomains`
- [ ] 3.2 Add to PollWorkerState as nullable property (null = no round in flight)
- [ ] 3.3 Update PollWorker to use state record for round management

## 4. ProjectionEngine extraction

- [ ] 4.1 Extract `ProjectionEngine.Evaluate` to a static pure method
- [ ] 4.2 Add direct unit tests for Evaluate (no Akka needed)

## 5. Validation

- [ ] 5.1 `dotnet build Njord.slnx`
- [ ] 5.2 All test projects pass
- [ ] 5.3 GrpcConsumer correctly caches AQ and Flood alerts (verify with test)
