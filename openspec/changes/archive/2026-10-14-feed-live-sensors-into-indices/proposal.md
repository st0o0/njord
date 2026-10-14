## Why

Live indoor sensor readings never reach the indices. `SensorGrpcService` stores readings in the SensorHub, and `ForecastPipeline` has a sensor side channel that forwards `SensorSnapshot` messages, but nothing ever sends one. So `IndexAnalyzer` always gets `null`, and the documented fallback chain (live sensor → configured value → default) stops at the configured value. The side channel also could not work as designed: it holds a single latest snapshot, while snapshots are per location, so with several locations the indices of one location would use another location's sensors. Readings expire at query time (staleness), which a pushed snapshot would not reflect either.

## What Changes

- For each forecast, the pipeline asks the SensorHub for the snapshot of that forecast's location and passes it to `IndexAnalyzer`. If the location has no fresh readings, if the SensorHub is not reachable, or if it does not answer in time, the indices fall back to the configured or default indoor values, and the forecast is still analyzed.
- The sensor side channel and the pipeline's `SensorSnapshot` handler are removed; the settings side channel stays as it is.
- The sensor `Push` RPC stores a reading under the configured spelling of its location, so a reading pushed as `"luzern"` for the configured location `"Luzern"` is found for that location's forecasts.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `pipeline-composition`: "Sensor and settings side channels" is replaced by a settings side channel requirement and a per-forecast, per-location sensor snapshot requirement.
- `grpc-sensor-service`: the `Push` RPC stores the reading under the configured location name.

## Non-goals

- No reactive re-computation when a reading arrives: indices still update with the next forecast (unchanged guardrail).
- No new sensor kinds, no change to aggregation, staleness or plausibility rules.
- No fix for the other audit findings (consensus toggle, `GetEnrichments`, MQTT error handling).

## Impact

- `src/Njord.Pipeline/ForecastPipeline.cs`: per-forecast sensor lookup stage; sensor side channel and handler removed.
- `src/Njord.Grpc/Mapping/SensorProtoMappings.cs`: canonical location name.
- Tests in `src/Njord.Pipeline.Tests/` and `src/Njord.Grpc.Tests/`.
- One extra in-process actor ask per analyzed forecast. There is no polling change, so there is no API-budget impact: requests per month against the 300k free-tier limit are unchanged.
