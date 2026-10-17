## Why

All MQTT state payloads are assembled by hand: `StatePayloadBuilder` (307 lines) fills `JsonObject`s with string keys, `HorizonProjection` in `Njord.Weather` does the same for forecast horizons. Nothing in the type system says which fields a payload has, keys such as `{parameter.JsonKey}_models` or `trend_{api}_dir` are built with string concatenation and `Replace`, rounding sits in the middle of the builder, and the rules for absent values differ per payload (alerts omit, derived/trends/consensus write `null`). `soil_frozen` is published as the string `"True"`/`"False"`. Typed payload records with explicit mappers make the contract visible, testable and consistent. njord and ha-njord release together as 0.5.0, so intentional wire corrections are possible now.

## What Changes

- **Step 0, clean MQTT boundary (pure refactor)**: everything that is MQTT-specific and not shared configuration lives in `Njord.Mqtt`, organised like the adapter projects in FunkArr (wire models and mappers in the adapter, nothing wire-related in the domain projects): the three actor messages in `Njord.Messages/Mqtt/` and `MqttConnectionHealthCheck` (host) move into `Njord.Mqtt`, the health-check registration moves into `MqttSetupContainer`, the project gets a folder layout (`Payloads/`, `Mapping/`, `Serialization/`, `Topics/`, `Actors/`, `Health/`, `Transport/`, `Configuration/`), and an architecture spec pins where MQTT types may live.
- **Enum conversion convention**: every enum-to-enum or enum-to-text conversion is an extension method with an exhaustive `switch` expression and a throwing default. No casts between enums, no `Enum.Parse/TryParse` or `ToString()`-derived text for wire, topic or metric values. Two leftovers are fixed: `AlertTypeExtensions.ToTopicSegment` (falls back to `ToString().ToLowerInvariant()`) and `EnrichmentKindExtensions.ToWireName` (`ToString().ToLowerInvariant()`); output values stay identical. The rule is written into `AGENTS.md` and checked by a source-scan architecture spec.
- **Step 1, no wire change**: typed sealed payload records in `Njord.Mqtt/Payloads/` plus internal mapper extensions (`ToPayload()`), one `JsonSerializerContext` (source generator) and custom converters where keys are registry-driven. `StatePayloadBuilder` becomes a thin mapper + serializer. Proven by the existing golden masters staying byte-identical.
- `HorizonProjection` (Njord.Weather) returns a typed projection (parameter + number/text per horizon) instead of JSON strings; serialization moves to `Njord.Mqtt`. `Njord.Weather` no longer depends on `System.Text.Json.Nodes`.
- **Step 2, BREAKING wire corrections** (after the proof):
  - Null policy for enrichment state payloads: a known-but-absent value is an explicit `null`. Alerts, which omit `peak_value`, `hours_until` and `duration_hours` today, are aligned. Forecast horizon payloads keep omitting values a model does not deliver.
  - `IceDetails.soil_frozen` becomes a JSON boolean instead of `"True"`/`"False"`.
  - `earliest_frost` keeps its `"+Nh"` text.
- Verify snapshots per payload type, a mapper spec per mapper (every field), a roundtrip/shape spec.
- Update `docs/mqtt-reference.md`, `docs/home-assistant/entities.md`, `AGENTS.md` and the `mqtt-egress` main spec.

## Non-goals

- Moving `MqttOptions` / `MqttOptionsValidator`, the `ConfigReloadPolicy` entry, the marker interfaces (`IMqttConnectionActor`, `IMqttStatePublisher`) and `MqttHealthSnapshot` out of `Njord.Core`: the config layer (`ConfigWriter`, `ConfigReloadPolicy`, `ConfigManager`), the marker rule and the shared `NjordHealthMonitor` need them and feature libraries must not reference each other. They are listed explicitly in the architecture rule.
- MQTT topic scheme, `TopicSlug`, connection lifecycle, retained/LWT behaviour, gRPC.
- Changing which messages are published or when.
- Moving `earliest_frost` to a number, or any rename of payload keys.
- ha-njord changes: it consumes gRPC, not MQTT (no reference to the changed fields; to be re-checked during implementation).
- API-budget impact: none (no polling change).

## Capabilities

### New Capabilities

- `enum-conversions`: enum conversions are explicit switch-expression extensions; no cast- or name-based conversion for wire, topic or metric text.
- `mqtt-adapter-boundary`: MQTT-specific types live only in `Njord.Mqtt`, except an explicit allow-list in `Njord.Core`; wire models and mappers are never in domain projects.
- `mqtt-typed-payloads`: MQTT state payloads are produced from typed records through explicit mappers and one serializer configuration; wire output is deterministic and snapshot-tested.

### Modified Capabilities

- `mqtt-egress`: the absent-value rule now distinguishes forecast horizon payloads (omit) from enrichment payloads (explicit `null`), and `soil_frozen` is a boolean.

## Impact

- `src/Njord.Messages/Mqtt/` (removed), `src/Njord/Health/MqttConnectionHealthCheck.cs` (moved), `src/Njord/Configuration/CoreSetupContainer.cs`, `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs`, `src/Njord.Architecture.Tests/` (new boundary rule), `AGENTS.md` (solution structure).
- `src/Njord.Mqtt/` (new `Payloads/`, mappers, serializer context, slimmer `StatePayloadBuilder`, `MqttStatePublisher`), `src/Njord.Weather/HorizonProjection.cs`.
- Tests: `Njord.Mqtt.Tests` (golden masters for alerts and others change only in step 2), `Njord.Weather.Tests/HorizonProjectionSpec.cs`, `Njord.Pipeline.Tests/PollPipelineSpec.cs` (uses `BuildPerHorizon`), `Njord.Architecture.Tests`.
- Docs: `docs/mqtt-reference.md`, `docs/home-assistant/entities.md`, `AGENTS.md`.
- Consumers of retained MQTT topics (user automations) see the step 2 changes; ha-njord is unaffected.
