## 1. Proto Definition

- [x] 1.1 Add `google/protobuf/timestamp.proto` import, `GetTriggerTargets` RPC, `GetTriggerTargetsRequest`, `GetTriggerTargetsResponse`, and `TriggerTarget` message to `protos/njord/v1/config_service.proto`
- [x] 1.2 Verify `dotnet build src/Njord.slnx` compiles with generated `TriggerTarget` type containing `Timestamp` properties

## 2. Service Implementation

- [x] 2.1 Add `GetTriggerTargets` override to `src/Njord/Grpc/ConfigGrpcService.cs` — ask `SchedulerActor` for `PollStatesSnapshot`, map entries to `TriggerTarget` messages using `Timestamp.FromDateTimeOffset()`, return empty list on `AskTimeoutException`

## 3. Tests

- [x] 3.1 Add `GetTriggerTargetsSpec` in `src/Njord.Tests/Grpc/` — test returning all configured pairs with poll state, test empty response on scheduler timeout, test Timestamp field population

## 4. Validation

- [x] 4.1 Run `dotnet build src/Njord.slnx` — verify clean build
- [x] 4.2 Run `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — verify all tests pass including new specs
