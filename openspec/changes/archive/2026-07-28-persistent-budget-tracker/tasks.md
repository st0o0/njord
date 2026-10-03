## 1. Persistence DTOs

- [x] 1.1 Create `BudgetTrackerDtos.cs` in `src/Njord/Persistence/` with `ApiCallRecordedDto` (fields: `v`, `w`, `utc`) and `BudgetTrackerSnapshotDto` (fields: `v`, `month`, `day`, `monthly`, `daily`), plus `BudgetTrackerDtoMapping` static class with `ToDto`/`ToDomain` methods.

## 2. BudgetTrackerActor

- [x] 2.1 Create `BudgetTrackerActor.cs` in `src/Njord/Pipeline/` as a `ReceivePersistentActor` with `PersistenceId = "budget-tracker"`. Define nested message records: `RecordApiCall(int Weight)`, `GetBudgetUsage`, `BudgetUsage(long MonthlyUsed, long DailyUsed)`. Implement `Recover<ApiCallRecordedDto>` (skip stale months), `Recover<SnapshotOffer>` (restore with month/day staleness check), `Command<RecordApiCall>` (persist + update counters + snapshot every 50), `Command<GetBudgetUsage>` (reply). Use `TimeProvider` for all time checks.
- [x] 2.2 Write `BudgetTrackerActorSpec.cs` in `src/Njord.Tests/Pipeline/` using Akka.Persistence.TestKit. Tests: record and query usage, weighted calls, day boundary reset, month boundary reset, recovery replays events, recovery skips stale-month events, snapshot triggers after 50 events, snapshot recovery with same month, snapshot recovery with month rollover, snapshot recovery with day rollover.

## 3. WeightedBudgetGate integration

- [x] 3.1 Change `WeightedBudgetGate` constructor in `src/Njord/Pipeline/IBudgetGate.cs` to accept `IActorRef` (keyed as `BudgetTrackerActor`) instead of `BudgetTracker`. Replace `_tracker.RecordCall(cost)` with `_trackerActor.Tell(new BudgetTrackerActor.RecordApiCall(cost))`.
- [x] 3.2 Update `BudgetThrottleStageSpec.cs` in `src/Njord.Tests/Pipeline/` to provide a test actor probe or fake actor instead of `BudgetTracker` instance.

## 4. ConfigGrpcService integration

- [x] 4.1 Change `ConfigGrpcService` constructor in `src/Njord/Grpc/ConfigGrpcService.cs` to remove `BudgetTracker` parameter. In `GetStatus`, resolve `BudgetTrackerActor` from `ActorRegistry` and `Ask<BudgetTrackerActor.BudgetUsage>(new BudgetTrackerActor.GetBudgetUsage(), AskTimeout)` with `AskTimeoutException` fallback (zero usage + warning log). Add `process_start_utc` to the response.
- [x] 4.2 Update `ConfigGrpcServiceStatusSpec.cs` in `src/Njord.Tests/Grpc/` to register a fake `BudgetTrackerActor` in the actor registry. Add test for budget tracker timeout fallback. Add test for `process_start_utc` field.
- [x] 4.3 Update `ConfigGrpcServiceSpec.cs` in `src/Njord.Tests/Grpc/` if it constructs `ConfigGrpcService` directly — remove `BudgetTracker` from constructor calls.

## 5. Proto and registration

- [x] 5.1 Add `int64 process_start_utc = 6` to `ServerStatus` in `protos/njord/v1/config_service.proto`.
- [x] 5.2 Update `NjordServiceSetup.cs` in `src/Njord/Configuration/`: remove `BudgetTracker` singleton registration, register `BudgetTrackerActor` via `WithActors` in Akka.Hosting, update `WeightedBudgetGate` factory to resolve `IActorRef` from `ActorRegistry`.

## 6. Cleanup

- [x] 6.1 Delete `src/Njord/Configuration/BudgetTracker.cs`. Delete `src/Njord.Tests/Configuration/BudgetTrackerSpec.cs`. Remove any remaining references.

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`. Confirm all existing and new tests pass.
- [x] 7.2 Run `dotnet build Njord.slnx` from `src/` to confirm zero warnings/errors.
- [x] 7.3 Run `dotnet slopwatch` from repo root.
