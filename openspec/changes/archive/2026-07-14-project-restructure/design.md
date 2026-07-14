## Context

The njord service currently uses four top-level namespaces for application code: `Configuration`, `Domain`, `Egress`, `Enrichment`, `Ingest`, and `Pipeline`. The `Egress/` namespace contains all MQTT-related code (transport, connection, discovery, payloads, topics) and the monolithic `MqttEgressActor`. The `Enrichment/` namespace mixes pure domain computation (7 computer/analyzer classes, 7 result records) with actor lifecycle code. The `*Result` records contain `ToMqttMessages()` methods, creating an architectural dependency from domain types to MQTT serialization.

The `MqttEgressActor` (350 lines) carries three responsibilities: MQTT connection lifecycle (connect, reconnect, LWT), HA discovery config publishing (birth handling, 7 enrichment-type if-blocks), and state telemetry egress (FetchOutcome → state payloads → MQTT).

## Goals / Non-Goals

**Goals:**
- Clean namespace boundaries: domain logic owns no transport knowledge, MQTT concerns are isolated, egress routing is publisher-agnostic
- Split `MqttEgressActor` into focused single-responsibility actors
- Establish a publisher registration protocol so future publishers (InfluxDB, webhook, etc.) can plug into `EgressActor` without modifying it
- Move `ToMqttMessages()` out of domain result records into the MQTT namespace
- Mirror the new structure in the test project

**Non-Goals:**
- Changing MQTT topic schemes, payload formats, or discovery structure
- Adding new publishers beyond MQTT
- Modifying the pipeline graph or poll logic
- Changing enrichment computation algorithms

## Decisions

### D1: Folder/namespace layout

```
Njord/
├── Configuration/              (unchanged)
├── Domain/
│   ├── Weather/                ModelForecast, ForecastSeries, ForecastPoint,
│   │                           DailyForecastSeries, DailyForecastPoint,
│   │                           WeatherModel, CycleId, ForecastDataHash,
│   │                           ModelSnapshot, ParameterDef, ParameterRegistry
│   └── Analysis/               ConsensusComputer, ConsensusResult,
│                               TrendAnalyzer, TrendResult,
│                               AlertEvaluator, AlertResult,
│                               DerivedComputer, DerivedResult,
│                               EnergyForecaster, EnergyResult,
│                               IndexScorer, IndexResult,
│                               HistoryAnalyzer, HistoryResult,
│                               ForecastHistory, ForecastRecord
├── Ingest/                     (unchanged)
├── Pipeline/                   (unchanged)
├── Egress/
│   ├── EgressActor.cs          publisher-agnostic router
│   └── EgressMessages.cs       RegisterPublisher, PublishResults, etc.
├── Mqtt/
│   ├── MqttConnectionActor.cs  connection lifecycle, MergeHub owner
│   ├── MqttPublisherActor.cs   domain results → MqttMessage via MergeHub sink
│   ├── DiscoveryActor.cs       HA birth subscription, config payloads
│   ├── MqttMessage.cs
│   ├── TopicScheme.cs
│   ├── DiscoveryPayloadBuilder.cs
│   ├── StatePayloadBuilder.cs  (gains ToMqttMessages logic from *Result records)
│   └── Transport/
│       ├── IMqttConnection.cs
│       ├── IMqttTransport.cs
│       └── MqttNetPublisher.cs
└── Enrichment/
    ├── EnrichmentActor.cs
    └── ForecastHistoryActor.cs
```

**Why**: Maps directly to the architectural zones (Domain → Egress → Mqtt). `Egress/` knows domain types but not MQTT. `Mqtt/` knows both. `Domain/` knows neither.

**Alternative**: Flat namespace with no Weather/Analysis split in Domain. Rejected because Domain currently has 12 weather types and would gain 14 analysis types — 26 files in one folder is too many.

### D2: Actor topology

```
                  EnrichmentActor
                       │
                  enrichment results (domain records)
                       │
                       ▼
               ┌───────────────┐
               │  EgressActor  │  Njord.Egress
               │  (router)     │
               └───────┬───────┘
                       │ broadcasts to registered publishers
                       ▼
              ┌────────────────┐
              │ MqttPublisher  │  Njord.Mqtt
              │ Actor          │
              └────────┬───────┘
                       │ MqttMessage via SinkRef
                       ▼
              ┌────────────────┐
              │ MqttConnection │  Njord.Mqtt
              │ Actor          │
              │ (MergeHub)     │
              └────────┬───────┘
                       │
              ┌────────────────┐
              │ DiscoveryActor │  Njord.Mqtt  (also feeds MergeHub)
              └────────────────┘
```

**Why**: Single-responsibility actors. `MqttConnectionActor` owns the physical connection and MergeHub — it doesn't know about payloads. `MqttPublisherActor` transforms domain results into MqttMessages — it doesn't manage connections. `DiscoveryActor` handles HA-specific lifecycle (birth subscription, config payloads) — independent of state telemetry.

### D3: Publisher registration protocol

```csharp
// In Njord.Egress
sealed record RegisterPublisher(IActorRef Publisher);
sealed record UnregisterPublisher(IActorRef Publisher);
sealed record PublishStateResult(string Location, object Result);
```

`EgressActor` maintains a `HashSet<IActorRef>` of registered publishers. On receiving a `PublishStateResult`, it forwards to every registered publisher. `EgressActor` watches registered publishers and auto-unregisters on `Terminated`.

**Why registration over child-creation**: EgressActor stays in `Njord.Egress` with no reference to `Njord.Mqtt`. The publisher is created externally (by the actor system setup or the Mqtt module) and registers itself. This inverts the dependency.

**Alternative**: EgressActor creates publishers as children via DI. Rejected because it would force `Njord.Egress` to reference `Njord.Mqtt`, breaking the clean namespace boundary.

### D4: ToMqttMessages migration

The 7 `*Result` records currently each have a `ToMqttMessages(baseTopic)` method that builds `MqttMessage` instances with topics and JSON payloads. This logic moves to static methods on `StatePayloadBuilder` in `Njord.Mqtt`:

```csharp
// Njord.Mqtt.StatePayloadBuilder
static IReadOnlyList<MqttMessage> FromConsensus(ConsensusResult result, string baseTopic, string location);
static IReadOnlyList<MqttMessage> FromAlerts(AlertResult result, string baseTopic);
static IReadOnlyList<MqttMessage> FromDerived(DerivedResult result, string baseTopic);
// ... etc
```

`MqttPublisherActor` calls these when it receives domain results from `EgressActor`.

**Why**: Result records become pure data. The MQTT serialization concern lives entirely in the MQTT namespace. The `TopicScheme` and `MqttMessage` dependencies are removed from domain types.

### D5: EnrichmentActor stream consumer changes

Currently, each enrichment stream consumer ends with:
```csharp
foreach (var msg in result.ToMqttMessages(baseTopic))
    // push to MergeHub sink
```

After restructuring, each stream consumer ends with:
```csharp
egressActor.Tell(new PublishStateResult(location, result));
```

The enrichment streams no longer produce `MqttMessage` — they produce domain results. The MQTT transformation happens downstream in `MqttPublisherActor`.

**Why**: EnrichmentActor drops its dependency on `MqttMessage` and `TopicScheme`. It only knows about domain types and the publisher-agnostic `EgressActor`.

### D6: MqttConnectionActor owns MergeHub and connection

The current `MqttEgressActor.MaterializeEgressGraph()` creates a MergeHub with discovery, availability, and tombstone source queues. In the new topology:

- `MqttConnectionActor` materializes the MergeHub and the publish sink
- It vends `SinkRef<MqttMessage>` to `MqttPublisherActor` and `DiscoveryActor` on request
- It owns `IMqttConnection` (connect/reconnect/LWT) and `IMqttTransport` (send)
- HA birth subscription (`homeassistant/status`) moves to `DiscoveryActor`, which tells `MqttConnectionActor` to subscribe on its behalf

### D7: Test structure mirrors source

Test folders change to match:
```
Njord.Tests/
├── Configuration/    (unchanged)
├── Domain/
│   ├── Weather/      (existing domain tests)
│   └── Analysis/     (moved from Enrichment/)
├── Egress/           (EgressActor tests)
├── Mqtt/             (MqttPublisherActor, DiscoveryActor, payload, topic tests)
├── Enrichment/       (EnrichmentActorSpec, ForecastHistoryActorSpec)
├── Ingest/           (unchanged)
├── Pipeline/         (unchanged)
└── Health/           (unchanged)
```

## Risks / Trade-offs

- **[Large diff]** → Every file gets a namespace change. Mitigate by doing the mechanical move first (folder rename), then the actor split. Two logical phases in one change.
- **[Enrichment-pipeline change conflict]** → The `enrichment-pipeline` change (81 tasks, in-progress) touches many of the same files. Mitigate by completing this restructure first, then rebasing enrichment-pipeline tasks onto the new structure. The enrichment-pipeline change has 0 completed tasks, so there's no work to reconcile.
- **[Actor registration complexity]** → Registration protocol adds a message hop vs. direct actor references. Acceptable cost for decoupling; in-process Tell is sub-microsecond.
- **[StatePayloadBuilder grows]** → Absorbing ToMqttMessages from 7 result types makes this file larger. Acceptable: it's a cohesive set of serialization methods in one place, and each method is a simple mapping.
