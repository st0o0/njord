## 1. Proto & Message Types

- [x] 1.1 Update `ModelStatus` in `protos/njord/v1/config_service.proto`: replace fields 3-5 with `phase` (string), `next_poll_utc` (int64), `last_change_utc` (optional int64), `miss_count` (int32, field 6), `cycle_seconds` (optional int64, field 7). Add `repeated string active_enrichments = 5` to `ServerStatus`.
- [x] 1.2 Add `GetPollStates`, `PollStatesSnapshot`, and `PollStateEntry` sealed records to `src/Njord/Pipeline/SchedulerMessages.cs`.

## 2. SchedulerActor Query Handler

- [x] 2.1 Add `Command<GetPollStates>` handler in `Ready` behavior of `src/Njord/Pipeline/SchedulerActor.cs` — iterate `_states`, map to `PollStateEntry` list, respond with `PollStatesSnapshot`.
- [x] 2.2 Add `GetPollStates` to `StashKnownCommands()` so it is stashed in `WaitingForRefs`, `Connecting`, and `WaitingForConnection` behaviors.
- [x] 2.3 Write `src/Njord.Tests/Pipeline/SchedulerActorGetPollStatesSpec.cs`: test that Ready state returns snapshot with correct entries, test that query during startup is stashed and answered after Ready.

## 3. ConfigGrpcService GetStatus

- [x] 3.1 Make `GetStatus` in `src/Njord/Grpc/ConfigGrpcService.cs` async. Ask SchedulerActor for `PollStatesSnapshot` (5s timeout), map entries to proto `ModelStatus`. Catch `AskTimeoutException` and return empty model list. Inject `IOptionsMonitor<NjordOptions>` enrichment flags into `active_enrichments`.
- [x] 3.2 Write `src/Njord.Tests/Grpc/ConfigGrpcServiceStatusSpec.cs`: test ModelStatus mapping from PollStateEntry, test active_enrichments reflects enabled features, test graceful degradation on Ask timeout.

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/`.
- [x] 4.2 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 4.3 Run slopwatch: `dotnet slopwatch` from repo root.
