## Why

The gRPC layer has grown organically around a data-centric model: njord streams raw domain data and enrichment results separately, and ha-njord must assemble entities from catalog + two data streams + active_enrichments across 11 touchpoints per feature (6 in njord, 4+ in ha-njord). Adding a single feature requires editing files in both repos with fragile string-based contracts (FEATURE_KEYS, Proto oneof field numbers). The Weather entity disappears when njord is down at HA startup because entity creation is data-driven rather than declared. This change rewrites the gRPC layer as a v3 entity-first API where njord declares all entities via a Manifest, serves one unified stream, and ha-njord becomes a generic manifest-driven client.

## What Changes

- **BREAKING**: New `protos/njord/v3/` proto package replacing the v2 WeatherService/OpsService split with a unified NjordService (GetManifest, Subscribe, GetStatus, TriggerPoll)
- v2 proto stays alongside v3 until ha-njord is migrated, then removed
- Two new `OutputKind` values: `Weather` (maps to HA weather platform) and `Observation` (raw sensor pass-through)
- `FeatureDescriptor` gains `EntityMetadata` (platform, device_class, icon, unit) as single source of truth for entity definitions
- Generic proto value types per OutputKind (7 types: Weather, Alert, Score, Condition, Trend, Forecast, Observation) replacing 65 per-feature oneof cases
- `GetManifest` RPC returns entity definitions built from registered FeatureRules x Locations; ha-njord creates all entities immediately
- One unified `Subscribe` stream replacing `StreamDomainData` + `StreamEnrichments`, with snapshot-on-connect and availability events
- Raw domain values (PM2.5, wave height, flood discharge) become `Observation` features; the Weather entity becomes a `Weather` feature. One path from njord to HA.
- `Njord.Grpc` rewrite: NjordGrpcService (v3), entity-id-keyed GrpcConsumer state, generic per-OutputKind mapping
- Akka Streams wiring preserved: MergeHub/BroadcastHub, GrpcConsumer actor with SourceRef, snapshot-on-connect, KillSwitch, keep-alive/idle-timeout/buffer
- Availability tracking: njord sends availability events based on poll staleness; ha-njord no longer guesses
- ha-njord rewrite: manifest-driven generic entities, no ALERT_META/FEATURE_KEYS/per-feature classes

## Non-goals

- Changing the Pipeline, FeatureEngine, or FeatureRule abstraction (only FeatureDescriptor is extended)
- Changing PollWorker, PollManager, or polling behavior
- Changing OpenMeteo clients or domain data fetching
- Changing the budget system
- Hot-reload of the manifest (catalog remains static per process, consistent with current design)
- Adding new features (Observation features and WeatherRule are created to validate the pattern; bulk migration of existing domain sensors to Observation features is a follow-up)

## Capabilities

### New Capabilities

- `entity-manifest`: Manifest RPC that declares all entities from FeatureDescriptors x Locations, with EntityMetadata (platform, device_class, icon, unit)
- `unified-entity-stream`: Single Subscribe stream replacing StreamDomainData + StreamEnrichments, with snapshot-on-connect, availability events, and generic per-OutputKind value types
- `v3-proto`: The v3 proto package with unified NjordService, generic value messages, and entity-first structure
- `observation-features`: OutputKind.Observation for raw domain values as trivial pass-through features (PM2.5, wave height, etc.)
- `weather-feature`: OutputKind.Weather for the HA weather platform entity as a feature with its own FeatureRule

### Modified Capabilities

- `feature-rule-abstraction`: FeatureDescriptor gains EntityMetadata; two new OutputKind values (Weather, Observation)
- `grpc-mapping-layer`: Rewrite from per-feature mapping (65 switch arms) to per-OutputKind generic mapping (7 types)

## Impact

- **Proto**: New `protos/njord/v3/` package; v2 stays until ha-njord migrates
- **Njord.Core**: OutputKind enum (2 new members), EntityMetadata record, FeatureDescriptor extension
- **Njord.Messages**: New WeatherResult and ObservationResult types
- **Njord.Features**: EntityMetadata on all existing descriptors; new WeatherRule and sample Observation features; AddFeatures registration
- **Njord.Grpc**: Full rewrite of service, consumer state model, and mapping layer
- **Njord (Host)**: ManifestBuilder, v3 service registration alongside v2
- **ha-njord**: Full rewrite of coordinator, entity platforms, and gRPC client (separate repo, D:\GIT\ha-njord)
- **API budget**: No impact (polling unchanged; only the gRPC serving layer changes)
- **Breaking**: Wire protocol v3 is incompatible with v2; ha-njord and njord must be upgraded together once v2 is removed
