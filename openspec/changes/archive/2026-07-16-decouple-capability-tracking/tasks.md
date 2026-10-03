## 1. EgressEvent Variant

- [x] 1.1 Add `CapabilityLearned` variant to `EgressEvent` in `src/Njord/Egress/EgressEvent.cs` with fields: `Location`, `Model`, `SupportedParameters`, `ApplicableHorizons`, `ApplicableDayOffsets`
- [x] 1.2 Delete the standalone `ModelCapabilityLearned` record from `src/Njord/Egress/EgressMessages.cs`

## 2. ModelStateActor Decoupling

- [x] 2.1 Refactor `ModelStateActor` (`src/Njord/Egress/ModelStateActor.cs`): emit `EgressEvent.CapabilityLearned` into the egress sink instead of direct-telling `DiscoveryActor`. Remove `_discoveryActor`, `_mqttEnabled`, `using Njord.Mqtt`, `SendCapabilityLearned` method, and all null guards. The `SelectMany` lambda returns both `CapabilityLearned` and `PerModelUpdate` when capabilities change.
- [x] 2.2 Update `ModelStateActorSpec` (`src/Njord.Tests/Egress/ModelStateActorSpec.cs`): remove `FakeDiscoveryActor`, remove DiscoveryActor registry setup from all tests, verify `CapabilityLearned` events appear in the egress sink alongside `PerModelUpdate` events

## 3. DiscoveryActor Hub Subscription

- [x] 3.1 Update `DiscoveryActor` (`src/Njord/Mqtt/DiscoveryActor.cs`): add `RequestEgressSource` to `EgressActor` in `PreStart`, wait for both `MqttSinkResponse` and `EgressSourceResponse` before transitioning. Materialize egress source stream filtering for `CapabilityLearned`, pipe to self as internal messages. Replace `Receive<ModelCapabilityLearned>` with the internal message type. Update `_capabilities` dictionary to use `EgressEvent.CapabilityLearned`.
- [x] 3.2 Update `DiscoveryActorSpec` (`src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs`): feed capability events through a fake EgressActor hub instead of direct `Tell`

## 4. Cleanup

- [x] 4.1 Verify `ModelStateActor` has no `using Njord.Mqtt` directive — compile check
- [x] 4.2 Search for remaining references to the deleted `ModelCapabilityLearned` record and update any test helpers or shared types

## 5. Validation

- [x] 5.1 Run full unit test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 5.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 5.3 Run slopwatch: `dotnet slopwatch` from repo root
