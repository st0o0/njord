## Why

The current poll pipeline uses nested materialization: a `RestartSource` wraps a `Source.Tick`, which in each cycle materializes a separate inner stream (`Source.From(targets).RunWith(Sink.Seq)`). This makes the pipeline hard to extend (new trigger sources require restructuring), impossible to test as a single graph, and introduces unnecessary allocation overhead per cycle. To support future command sources (MQTT commands from HA automations, REST refresh endpoints) the pipeline needs a flat, composable architecture where triggers are decoupled from processing.

## What Changes

- **Replace nested materialization with a single flat graph** using `MergeHub<PipelineCommand>` as the entry point, eliminating the inner `RunWith` and `RestartSource` wrapper.
- **Introduce a typed command protocol** (`PollAll`, `RefreshLocation`, `RefreshModel`) so triggers and processing are decoupled — any source can emit commands.
- **Switch from element-counting throttle to a weighted throttle** using the Open-Meteo cost formula (`ceil(hourlyVars/10) × ceil(days/14)`) so requests can safely run in parallel without budget risk.
- **Remove cycle aggregation** — each `FetchOutcome` maps directly to one device state publish (hourly + daily combined from the same API call). No `CycleId`, no `GroupBy`, no timeout-based collection.
- **Phase 1 scope**: only `Source.Tick` feeds the MergeHub (behaviour identical to today). The architecture enables MQTT command and REST trigger sources to be added later without restructuring.
- **Eliminate `PipelineGuardianActor` as stream owner** — the graph lifecycle moves to a `KillSwitch`-based approach or simpler hosting pattern; supervision is via stream attributes (`RestartSettings` on the source, `Decider` on the graph).

## Non-goals

- Adding MQTT command or REST trigger sources (Phase 2/3).
- Consensus computation (deferred; per-model data goes 1:1 to HA).
- Changing the MQTT discovery mechanism or entity grid.
- Changing the Open-Meteo client interface or request shape.
- Renaming the project (long-term "OpenMeteo2Mqtt" vision is out of scope).

## Capabilities

### New Capabilities
- `pipeline-commands`: Typed command protocol (PollAll, RefreshLocation, RefreshModel) and the command expansion logic that maps commands to fetch targets with pre-calculated API weight.
- `stream-composition`: The flat, composable pipeline graph — MergeHub source, weighted throttle, parallel fetch, direct-to-publish sink — as testable flow segments.

### Modified Capabilities
- `poll-pipeline`: Requirements change — cycles no longer aggregate with timeout; each fetch outcome publishes independently. The "cycle result" concept is removed; the pipeline emits per-device state updates directly. Restart-with-backoff moves from wrapping the entire source to stream-level supervision attributes.

## Impact

- **`src/Njord/Pipeline/`**: `PollPipeline.cs` rewritten; `PipelineGuardianActor.cs` simplified or removed.
- **`src/Njord/Domain/`**: New command types; `CycleId` and `CycleResult` removed or deprecated.
- **`src/Njord.Tests/Pipeline/`**: Tests rewritten against the new graph shape (TestSource → graph → TestSink).
- **Dependencies**: No new NuGet packages (MergeHub, Throttle with cost function are in Akka.Streams core).
- **API budget**: No change — same number of requests per poll interval, same targets, same weight. The throttle is now weight-aware but the actual request volume is unchanged.
