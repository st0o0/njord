## 1. Extract ConfigUpdateHandler

- [x] 1.1 Create `src/Njord/Configuration/ConfigUpdateHandler.cs` — sealed class with `IConfigWriter` dependency. Extract the 4 write methods from ConfigManager: `HandleUpdateLocations`, `HandleUpdateSettings`, `HandleUpdateAnalysisConfig`, `HandleUpdateBudget`. Each takes the command message + current option values as parameters and returns the response record.
- [x] 1.2 Move the static `BudgetRejection()` helper and any write-specific validation logic to the handler.

## 2. Slim ConfigManager

- [x] 2.1 Replace ConfigManager's 4 write Receive handlers with delegation: each calls the handler method and tells `Sender` the result.
- [x] 2.2 Remove `IConfigWriter` from ConfigManager's stored fields. Create `ConfigUpdateHandler` in the constructor from the injected writer.
- [x] 2.3 Remove the extracted private methods from ConfigManager (they now live in the handler).

## 3. Test updates

- [x] 3.1 Verify existing `src/Njord.Tests/Configuration/ConfigManagerSpec.cs` still passes — the message-level behavior is unchanged.
- [x] 3.2 No separate ConfigUpdateHandlerSpec needed — actor tests cover the behavior through message send/receive.

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings
- [x] 4.2 Run affected test suites — all pass (184 + 157 + 128 = 469 tests, 0 failures).
- [x] 4.3 Full suite loop — all projects pass with no regressions.
