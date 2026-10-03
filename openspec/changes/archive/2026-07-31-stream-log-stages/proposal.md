## Why

Akka.Streams data-processing graphs in njord have no element-level tracing. When diagnosing data-flow issues (missing forecasts, stale enrichments, MQTT gaps), there is no way to observe what flows through each stream stage without adding ad-hoc logging, recompiling, and redeploying. The built-in Akka.Streams `.Log()` stage provides zero-overhead passthrough tracing at Debug level — invisible in production, instantly available by flipping a Serilog `MinimumLevel.Override` per namespace.

## What Changes

- **Add `.Log()` stages to all 10 data-processing stream graphs** across 8 actors and 1 gRPC service, at semantically meaningful boundaries (not every operator).
- **Define a naming convention** for Log stage names that maps to predictable SourceContext values for filtering.
- **Add compact extractor functions** per stage that log location/model/type/size — not full payloads.
- **Document per-namespace override examples** so operators can selectively enable stream tracing for specific subsystems.

## Capabilities

### New Capabilities

- `stream-log-tracing`: Defines the `.Log()` stage placement, naming convention, extractor patterns, and per-namespace activation for verbose stream-level tracing across all data-processing graphs.

### Modified Capabilities

- `structured-logging`: Add a requirement for per-namespace override documentation and the relationship between `.Log()` stage names and Serilog filtering.

## Impact

- **8 actor files + 1 gRPC service** modified (inserting `.Log()` calls into existing stream graphs)
- **No new dependencies** — `.Log()` is built into Akka.Streams, already referenced
- **No API budget impact** — no polling changes
- **No behavioral changes** — `.Log()` is a passthrough; default Debug level means zero visible output in production config
- **`appsettings.Example.json`** updated with commented override examples for stream tracing

## Non-goals

- Replacing existing Info-level structured logs (`_log.Info(...)`) with `.Log()` stages — those serve different purposes (operational visibility vs debug tracing).
- Adding `.Log()` to infrastructure graphs (StreamRef vending) — those are lifecycle plumbing, not data flow.
- Adding metrics, counters, or OpenTelemetry spans to streams.
- Changing log levels of existing log statements.
