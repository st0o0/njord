## Why

`ConfigManager` is njord's largest actor at 486 LOC with 9 constructor parameters. It aggregates four responsibilities: config reads (queries), config writes (updates), config streaming (SourceRef), and reload propagation (IOptionsMonitor callbacks). The write handlers (~200 LOC) own validation, budget-guard projection, and persistence through `IConfigWriter` — logic that doesn't need actor state or Akka APIs.

## What Changes

- Extract `ConfigUpdateHandler` as a plain sealed class in `src/Njord/Configuration/` that owns `IConfigWriter` and the 4 write handler methods (`UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, `UpdateBudget`).
- `ConfigManager` creates `ConfigUpdateHandler` in its constructor and delegates write messages to it.
- `ConfigManager` drops the `IConfigWriter` constructor parameter (moves to the handler).
- After a write, `IOptionsMonitor` change callbacks fire automatically (the file write triggers them), so no explicit reload notification is needed.
- No message type changes — `IConfigManager` API surface unchanged.

## Non-goals

- Splitting ConfigManager into two separate actors (adds coordination overhead for marginal benefit).
- Reducing the IOptionsMonitor count (ConfigManager IS the config aggregation point — monitoring 6 option types is its job).
- Changing the gRPC API surface.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `config-persistence`: Write/update logic moves from ConfigManager actor to ConfigUpdateHandler helper class.

## Impact

- `src/Njord/Configuration/ConfigManager.cs`: delegates write handlers, drops IConfigWriter param
- `src/Njord/Configuration/ConfigUpdateHandler.cs`: new file, ~200 LOC extracted
- `src/Njord/Configuration/ConfigSetupContainer.cs`: may need adjustment for handler DI
- Tests: ConfigManagerSpec write-related tests may need minor adjustments
