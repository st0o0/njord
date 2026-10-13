## Why

The analysis models are keyed and described by strings: horizons are `"h12"`/`"d1"`
dictionary keys built with `$"h{n}"` in five places, alert diagnostics are an
`IReadOnlyDictionary<string, object?>`, alert thresholds are `double[]` read by index
(`thresholds[0]`, `Length < 3`), index preferences are looked up by
`(string Location, string Score)` with literals like `PrefsFor("Outdoor")`, and trend,
stability, dew-point and WMO results are free-form strings. A typo compiles, the shape of
an alert is invisible to callers, and the big compute classes are full of key plumbing.
This change is the first of four (followed by `split-options-per-feature`,
`restructure-compute`, `separate-messages-from-api`) and gives the later ones typed models
to build on.

## What Changes

- Pin today's wire output first: Verify golden masters for every MQTT state message
  builder and every gRPC enrichment/forecast mapper, from one fixed fixture. Remove the
  orphaned `*.verified.txt` files in `Njord.Mqtt.Tests` that no spec references any more.
- Introduce a typed `Horizon` (hourly offset or daily offset) and key every
  `ByHorizon` collection by it. The `"h12"`/`"d1"` text form exists only where MQTT topics,
  JSON keys and proto fields are written.
- Replace `Alert.Attributes` with one typed detail record per alert type (e.g. frost:
  expected low, earliest frost horizon, models agreeing). MQTT and gRPC mappers produce the
  existing snake_case attribute keys from those records.
- Replace threshold arrays in the compute layer with a `SeverityThresholds` value
  (advisory/warning/severe) and the matching direction. Configuration keeps its array form;
  conversion and validation happen once (completed in `split-options-per-feature`).
- Replace score-name strings with an `IndexKind` enum and the preference key with
  `(LocationId, IndexKind)`. Configuration keeps string keys (case-insensitive).
- Replace string results with enums: trend direction, stability, dew-point comfort,
  WMO weather category and condition. Display text is produced at the edge.
- Introduce `LocationId` for location identity in analysis results.
- **BREAKING (internal only)**: drop the unused `[JsonProperty]` annotations and the
  Newtonsoft dependency from `Njord.Compute` analysis records; no production code
  serializes them and nothing persists them.

## Capabilities

### New Capabilities
- `wire-format-golden-masters`: MQTT state payloads and gRPC analysis responses are pinned
  by snapshot tests; internal refactorings must leave them byte-identical.
- `typed-analysis-keys`: horizons, index kinds, locations and categorical results are
  typed values inside the system; their text forms exist only at the output edge.

### Modified Capabilities
- `threshold-alerts`: alert diagnostics become typed per alert type; the serialization
  requirement is removed.
- `derived-values`: dew-point comfort and WMO description become typed categories.
- `trend-analysis`: trend direction, weather-change categories and stability become
  typed; the serialization requirement is removed.
- `index-preferences`: resolved preferences are keyed by typed location and index kind.
- `activity-indices`: the result serialization requirement is removed (wire keys stay
  pinned by golden masters).
- `consensus-computation`: the result serialization requirement is removed.

## Non-goals

- No change to MQTT topics, MQTT JSON payloads, proto files or gRPC responses.
- No change to configuration file shape (`appsettings*.json`, `njord-config.json`, env vars).
- No units-of-measure types (`Celsius`, `MetersPerSecond`).
- No restructuring of compute classes or options classes; that is
  `restructure-compute` and `split-options-per-feature`.
- No change to polling; API budget impact: 0 additional requests/month.

## Impact

- `src/Njord.Weather` (`HorizonProjection`, new `Horizon`, `LocationId`),
  `src/Njord.Compute/Analysis/*` (records, horizon keys, enums),
  `src/Njord.Alerts/AlertEvaluator.cs`, `src/Njord.Mqtt/StatePayloadBuilder.cs`,
  `src/Njord.Grpc/EnrichmentProtoMapper.cs`, `src/Njord.Indices`, `src/Njord.Trends`,
  `src/Njord.Derived`.
- Tests: new golden-master specs in `src/Njord.Mqtt.Tests` and `src/Njord.Grpc.Tests`;
  updates in `src/Njord.Compute.Tests`, `src/Njord.Alerts.Tests`, `src/Njord.Weather.Tests`.
- No external API, config or persistence change.
