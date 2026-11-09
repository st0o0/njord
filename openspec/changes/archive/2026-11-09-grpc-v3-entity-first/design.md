## Context

The gRPC layer (`Njord.Grpc`) was built incrementally as domains and features were added. It exposes a data-centric API: `WeatherService` with 5 RPCs (GetCatalog, GetDomainData, GetEnrichments, StreamDomainData, StreamEnrichments) and `OpsService` with 3 RPCs. The ha-njord integration assembles HA entities from this by combining catalog info, two independent data streams, and `active_enrichments` from the status endpoint.

Current pain points:

- **11 touchpoints per feature**: adding a feature requires a per-feature proto message, a per-feature oneof case in `EnrichmentEvent` (65 and growing), a per-feature mapping extension, plus 4 registrations in ha-njord (Feature enum, FEATURE_KEYS, ALERT_META/LIFESTYLE_SENSORS, entity class).
- **Data-driven entity creation**: HA entities appear only when their first data point arrives. If njord is down at HA startup, no entities exist.
- **Fragile string contracts**: ha-njord's `FEATURE_KEYS` maps strings like `"Forecast:Frost"` to Python enums with no compile-time or runtime validation.
- **Two parallel streams**: domain data and enrichments flow through separate RPCs but share the same underlying `GrpcConsumer` BroadcastHub, adding unnecessary filtering complexity in `WeatherGrpcService`.

What works well and should be preserved:

- **Akka Streams topology**: `ForecastPipeline` (MergeHub -> FeatureEngine -> BroadcastHub) -> `GrpcConsumer` (actor with SourceRef subscription, own BroadcastHub for clients, snapshot-on-connect via `Source.From(snapshot).Concat(hub)`, keep-alive 30s, idle timeout 90s, buffer 256 with backpressure, KillSwitch + CancellationToken bridge).
- **FeatureRule abstraction**: `FeatureDescriptor` already carries `OutputKind`, `FeatureCategory`, `Name`, `Requirements`, `PollDepth`. The feature registration in `AddFeatures` and the `FeatureEngine` dispatch are clean.
- **PipelineEvent hierarchy**: domain data events (`ForecastDataReceived`, `AirQualityDataReceived`, ...) and `FeatureResult` subclasses flow as a single `PipelineEvent` stream through the hub.

## Goals / Non-Goals

**Goals:**

- Entity-first API: njord declares all entities via a manifest; ha-njord creates them immediately at setup
- One path: every HA entity (weather, alerts, scores, raw sensors) is a feature flowing through the same pipeline and stream
- Extensibility: adding a feature is 2-3 files in njord, zero in ha-njord
- Availability tracking: njord knows poll timing and emits staleness events
- Generic proto: value types per `OutputKind` (7) instead of per feature (65+)

**Non-Goals:**

- Changing Pipeline, FeatureEngine, or FeatureRule evaluation logic
- Changing PollWorker, PollManager, or polling behavior
- Changing OpenMeteo clients or budget system
- Hot-reload of the manifest (remains static per process)
- Bulk migration of all existing domain sensors to Observation features (a follow-up; this change creates the pattern and a few examples)

## Decisions

### 1. EntityMetadata on FeatureDescriptor

`FeatureDescriptor` is extended with an `EntityMetadata` record:

```
EntityMetadata(
    EntityPlatform Platform,    // Weather, Sensor, BinarySensor
    string? DeviceClass,        // "temperature", "pm25", "safety", null
    string? Icon,               // "mdi:thermometer", null (HA default)
    string? Unit)               // "°C", "%", "µg/m³", null
```

**Why on the descriptor**: the descriptor is already the single source of truth for what a feature *is* (output kind, category, name, requirements). Entity metadata is a natural extension — it says what the feature *becomes* in HA. Keeping it separate would require a parallel registry that must stay in sync.

**Why not derive it purely from OutputKind**: while OutputKind determines the platform (Alert -> BinarySensor, Score -> Sensor), it cannot determine device_class, icon, or unit. A UV alert and a frost alert share OutputKind.Alert but need different device_classes and icons.

`EntityPlatform` is a new enum in `Njord.Core.Domain`: `Weather`, `Sensor`, `BinarySensor`. It maps to HA platforms. The `Event` and `Calendar` HA platforms remain ha-njord-only concerns derived from the feature's OutputKind.

### 2. v3 Proto structure

A single `protos/njord/v3/njord.proto` file with one service:

```
service NjordService {
    rpc GetManifest(ManifestRequest) returns (ManifestResponse);
    rpc Subscribe(SubscribeRequest) returns (stream EntityEvent);
    rpc GetStatus(StatusRequest) returns (StatusResponse);
    rpc TriggerPoll(TriggerPollRequest) returns (TriggerPollResponse);
}
```

**Why one proto file**: v2 split into `weather.proto`, `ops.proto`, `common.proto` because the services were separate. v3 has one service; a single file with shared types is simpler.

**Why 4 RPCs**: GetManifest (discovery), Subscribe (all data), GetStatus (ops), TriggerPoll (ops). The unary GetDomainData and GetEnrichments are removed — all data flows through Subscribe's snapshot-on-connect.

**Value oneof by OutputKind** (7 types, not 65):

```
message EntityStateUpdate {
    string entity_id = 1;
    google.protobuf.Timestamp updated_at = 2;
    oneof value {
        WeatherValue weather = 10;
        AlertValue alert = 11;
        ScoreValue score = 12;
        ConditionValue condition = 13;
        TrendValue trend = 14;
        ForecastValue forecast = 15;
        ObservationValue observation = 16;
    }
}
```

Each value message carries the common fields for its OutputKind. For example, `AlertValue` has severity, confidence, trigger_value, threshold, peak_value, hours_until, duration_hours — the fields from `AlertFeatureResult`. Feature-specific extra fields (e.g. `FrostAlertResult.ExpectedLow`) are carried in a `map<string, double> extra` field on the value message, keyed by field name. This preserves feature-specific data without per-feature proto types.

### 3. ManifestBuilder

A new `ManifestBuilder` class (in `Njord.Grpc` or `Njord` Host) constructs the manifest at startup from `IEnumerable<IFeatureRule>` (all registered rules) and `NjordOptions` (locations):

```
for each location in options.Locations:
    for each rule in rules where rule.Enabled:
        yield EntityDefinition(
            entity_id:    slugify(location.Name) + "_" + snake_case(rule.Descriptor.Name),
            location:     location.Name,
            feature:      rule.Descriptor.Name,
            platform:     rule.Descriptor.EntityMetadata.Platform,
            device_class: rule.Descriptor.EntityMetadata.DeviceClass,
            icon:         rule.Descriptor.EntityMetadata.Icon,
            unit:         rule.Descriptor.EntityMetadata.Unit,
            category:     rule.Descriptor.Category,
            output_kind:  rule.Descriptor.Output)
```

**Entity ID format**: `{location_slug}_{feature_name_snake}`, e.g. `vienna_frost`, `vienna_weather`, `vienna_pm25`. The Weather feature per secondary model uses `{location_slug}_{model_id}_weather`.

The manifest is built once at startup (same lifetime as the current `_catalog` in `WeatherGrpcService`) and registered as a singleton. `GetManifest` returns it directly.

### 4. GrpcConsumer state model

Current state: `ImmutableDictionary<(Location, DomainKind, string?), PipelineEvent>` for domain data + `ImmutableDictionary<(Location, Type), FeatureResult>` for results. Two separate dictionaries, keyed by domain/type.

New state: `ImmutableDictionary<string, EntityState>` keyed by `entity_id`. An `EntityState` record holds the latest `FeatureResult` (or domain data mapped to a result for Weather/Observation features) and the `DateTimeOffset` of the last update.

**How PipelineEvent maps to EntityState**: The consumer needs to know which `entity_id` a `PipelineEvent` belongs to. Two paths:

- **FeatureResult subclasses** (alerts, scores, etc.) already carry `Location`. The consumer looks up the entity_id from a `FeatureResult.GetType()` -> feature name mapping, built from the registered `IFeatureRule` list at startup. Key: `(Location, FeatureResultType) -> entity_id`.
- **Domain data events** (`ForecastDataReceived`, etc.) map to Weather and Observation feature entities. The consumer uses a similar lookup: `(Location, DomainKind) -> entity_id[]` for the features that consume that domain.

The consumer still receives raw `PipelineEvent` from the pipeline (no pipeline changes). It maps them to entity-keyed state internally.

### 5. Unified Subscribe stream

Same Akka Streams topology as today, different payload:

```
Source.From(snapshot)           // all current EntityState values as EntityStateUpdate
    .Concat(_clientHubSource)   // live EntityEvent from hub
    .KeepAlive(30s, Heartbeat)
    .Buffer(256, Backpressure)
    .IdleTimeout(90s)
    .Where(not Heartbeat)
    .RunWith(StreamRefs.SourceRef<EntityEvent>())
```

`EntityEvent` is a wrapper with a oneof: `EntitySnapshot` (batch of all states at connect time), `EntityStateUpdate` (single entity value change), `EntityUnavailable` (staleness), `EntityAvailable` (recovery).

The `NjordGrpcService.Subscribe` method gets the SourceRef from the consumer, applies the KillSwitch + CancellationToken bridge, and writes each `EntityEvent` to the gRPC response stream — same pattern as today's `StreamDomainData`.

**SubscribeRequest** accepts an optional `location` filter (empty = all locations). No domain filter needed since everything is entity-based.

### 6. Generic per-OutputKind mapping

Instead of 65 per-feature `ToProto()` extension methods and a 65-arm switch in `EnrichmentEventMappingExtensions`, one mapper per OutputKind:

- `AlertValueMapper`: `AlertFeatureResult` -> `AlertValue` (maps common fields; feature-specific extra fields go into `extra` map)
- `ScoreValueMapper`: `ScoreFeatureResult` -> `ScoreValue`
- `ConditionValueMapper`: `ConditionFeatureResult` -> `ConditionValue`
- `TrendValueMapper`: `TrendFeatureResult` -> `TrendValue`
- `ForecastValueMapper`: `ForecastFeatureResult` -> `ForecastValue`
- `ObservationValueMapper`: `ObservationResult` -> `ObservationValue`
- `WeatherValueMapper`: `WeatherResult` -> `WeatherValue`

The consumer or service calls the appropriate mapper based on `FeatureResult` base type. No switch on concrete feature types.

**Alternative considered**: a single generic mapper using reflection. Rejected because the 7 base types have different structures (alerts have severity/threshold, scores have numeric value/label) that need explicit mapping.

### 7. Availability tracking

The `GrpcConsumer` tracks `DateTimeOffset` of the last update per entity. A timer (Akka `IWithTimers` or `ReceiveTimeout`) periodically checks all entities against a staleness threshold: `poll_interval * staleness_factor` (e.g. 60min * 2.5 = 150min). When an entity crosses the threshold, `EntityUnavailable` is emitted to the hub. When fresh data arrives for a previously-stale entity, `EntityAvailable` is emitted.

**Why in GrpcConsumer, not in ha-njord**: njord knows the poll interval and the last successful fetch per domain. ha-njord can only guess based on stream silence. Staleness is a server-side concern.

The staleness factor is configurable via `Njord:Health:StaleFactor` (default 2.5), using the existing `HealthOptions` pattern.

### 8. v2/v3 coexistence

Both `WeatherGrpcService` (v2) and `NjordGrpcService` (v3) are registered in the host. They share the same `GrpcConsumer` actor — the consumer continues to cache `PipelineEvent` data and serve both v2-style queries (unchanged) and v3-style entity streams.

The consumer needs to support both `RequestGrpcStream` (v2, returns `ISourceRef<PipelineEvent>`) and `RequestEntityStream` (v3, returns `ISourceRef<EntityEvent>`). Internally both read from the same hub; the v3 path adds the entity-mapping layer.

Once ha-njord migrates to v3, the v2 service, its proto, and the v2-specific consumer handlers are removed in a follow-up change.

### 9. Weather feature

`WeatherRule` is a new `FeatureRule<ForecastInput>` in `Njord.Features` with:

- `OutputKind.Weather`, `FeatureCategory.Forecast`, `Name: "Weather"`
- Requirements: `ForecastParams.Temperature | WeatherCode | Humidity | WindSpeed | WindDirection | Pressure | ApparentTemperature`
- `EntityMetadata(Platform: EntityPlatform.Weather)`
- Always enabled (no config toggle)

`WeatherResult` is a new `FeatureResult` subclass carrying:
- Current conditions: temperature, humidity, wind speed, wind bearing, pressure, condition string (mapped from weather_code), apparent temperature
- Hourly forecast: `IReadOnlyList<WeatherForecastPoint>` with the same fields plus timestamp

This replaces the current path where ha-njord reads raw `ForecastDataReceived` domain data and maps it to HA's weather schema. Now njord does that mapping in the `WeatherRule.Evaluate`, and `WeatherValue` in the proto carries the HA-ready schema.

**Secondary models**: each enabled secondary model gets its own `WeatherRule` instance (or a parameterized rule) with `entity_id = {location}_{model}_weather`. The manifest lists them explicitly.

### 10. Observation features

`ObservationResult` is a new `FeatureResult` subclass: `ObservationResult(Location, double Value)`.

An observation feature is a trivial `FeatureRule` that extracts one field from a domain point:

```
public sealed class Pm25ObservationRule : FeatureRule<AirQualityInput>
{
    public override FeatureDescriptor Descriptor => new(
        Output: OutputKind.Observation,
        Category: FeatureCategory.AirQuality,
        Name: "Pm25",
        Requirements: new DomainRequirements { AirQuality = AirQualityParams.Pm25 },
        EntityMetadata: new(EntityPlatform.Sensor, "pm25", null, "µg/m³"));

    public override ObservationResult? Evaluate(FeatureContext<AirQualityInput> ctx)
        => ctx.Input.Current.Pm25 is { } v ? new(ctx.Location, v) : null;
}
```

**Pattern for bulk creation**: each observation feature is a small class. For the initial change, a handful of representative observations are created (PM2.5, wave height, flood discharge, UV index raw) to validate the pattern. Bulk migration of all current domain sensors is a follow-up.

**Alternative considered**: a generic `ObservationRule<TInput>` parameterized by a field selector delegate, registered N times. Rejected because each observation still needs its own `FeatureDescriptor` with specific requirements, device_class, and unit — a class is barely longer than the registration and is explicit.

## Risks / Trade-offs

- **Generic values lose per-feature type safety in the proto** -> Mitigated: the 7 OutputKind value types are still strongly typed. Feature-specific extra fields (e.g. `FrostAlertResult.ExpectedLow`) move to a `map<string, double> extra` field, losing proto-level typing for those extras. Acceptable trade-off: ha-njord already doesn't use most feature-specific fields, and they can be added to `extra_state_attributes` by key name.
- **v2/v3 coexistence adds temporary complexity** -> The consumer must handle both stream request types. Mitigated by sharing the same underlying hub and keeping v2 code untouched. Bounded: v2 is removed once ha-njord migrates.
- **Weather entity is special** -> `OutputKind.Weather` maps to HA's `weather` platform with its fixed schema (temperature, condition, forecast). ha-njord needs one Weather-specific entity class, but it's driven by the manifest (platform = Weather), not by feature-specific knowledge. This is the only OutputKind where ha-njord has platform-specific logic beyond `sensor`/`binary_sensor`.
- **Result type routing in GrpcConsumer** -> The consumer must map `FeatureResult.GetType()` to entity_id. This requires a startup-built lookup from the registered rules. If a result type is not in the lookup (bug), it falls through silently. Mitigated: integration tests verify that every registered rule's result type is mapped.
- **Observation features add to the feature count** -> Each raw sensor becomes a feature in the rules array. With 65 existing features and potentially 20-30 observations, the `FeatureEngine` evaluates ~90 rules per poll cycle. The engine already skips rules whose domain didn't update, so the cost is one requirements-check per observation — negligible.

## Migration Plan

1. **Phase 1 (njord, this change)**: Add `OutputKind.Weather`/`Observation`, `EntityMetadata`, `EntityPlatform` to Core. Add `WeatherResult`, `ObservationResult` to Messages. Create `WeatherRule` and sample observation features. Create `protos/njord/v3/njord.proto`. Rewrite `Njord.Grpc`: new `NjordGrpcService` (v3), entity-keyed consumer state with v3 stream support, generic per-OutputKind mapping. Register v3 alongside v2. All existing tests still pass (v2 untouched).
2. **Phase 2 (ha-njord, follow-up change)**: Rewrite ha-njord to use v3: manifest-driven entity creation, unified Subscribe stream, generic entity classes. Remove ALERT_META, FEATURE_KEYS, per-feature registries.
3. **Phase 3 (cleanup)**: Remove v2 proto, v2 service, v2 consumer handlers, v2 mapping extensions. Remove now-unused per-feature `ToProto()` methods.
4. **Phase 4 (follow-up)**: Bulk-create Observation features for all domain sensor values. Remove domain-sensor code from ha-njord.

Rollback: v2 stays functional throughout. If v3 has issues, ha-njord reverts to v2 by pointing at the v2 service endpoint. No data migration needed.

## Open Questions

- **Event and Calendar platforms**: The current ha-njord creates alert event entities and a calendar entity. These could become their own OutputKinds (`OutputKind.Event`, `OutputKind.Calendar`) or remain ha-njord-derived from alert/condition features. Deferring this decision to the ha-njord rewrite phase; the v3 proto's generic structure supports either approach.
- **Secondary model weather entities**: Should each secondary model be a separate `WeatherRule` instance, or should the `WeatherRule` emit multiple results (one per model)? A separate rule per model is simpler and fits the "one entity = one feature" model, but requires dynamic rule registration based on `ActiveModels` config. The manifest already handles this (one EntityDefinition per model per location).
