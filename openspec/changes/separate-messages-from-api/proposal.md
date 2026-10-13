## Why

The gRPC layer is not a thin adapter, and the message contracts are scattered.
`Njord.Grpc`:
- owns an actor (`GrpcConsumer`) holding read-model state,
- defines public messages nested in that actor,
- reads `IOptions<NjordOptions>` in four services,
- calls `BudgetCalculator` and `ModelCoverageRegistry` directly,
- and (until `split-options-per-feature`) wrote configuration itself.

Messages live in three places: `Njord.Messages`, `Njord.Core` (`PipelineEvent`,
`StreamWiringMessages`) and `Njord.Grpc`. `Njord.Messages` depends on Akka and
Akka.Streams because one record carries an `ISourceRef`, which the existing
`message-domain-separation` spec already forbids.

FunkArr shows the target: a dependency-free messages project holding all commands,
queries and results, and an API project that only resolves an actor, asks with a message,
and maps the result. This is change 4 of 4.

## What Changes

- **`Njord.Messages` becomes the single contract project.**
  - It references only Weather, Sensors and Compute (contracts). It has no Akka or
    Akka.Streams package.
  - `PipelineEvent` moves here from `Njord.Core/Egress/`.
  - All public commands, queries and results live here, organized by domain folder
    (`Budget/`, `Config/`, `Forecast/`, `Mqtt/`, `Pipeline/`, `Sensors/`, `Common/`).
- **Stream handshake records live in `Njord.Core/Actors/`** with the existing
  `RequestPipelineOutput`: `RequestForecastStream` (moved out of Messages) and new ones for
  the read-model and config streams.
- **`GrpcConsumer` becomes `ForecastReadModel` in `Njord.Pipeline`.** It is the same
  behavior (latest results per location, secondary hub for streaming RPCs), owned by the
  pipeline side. `Njord.Grpc` contains no actors.
- **`ConfigManager` (from change 2) answers config queries and commands:**
  - queries: `QueryConfig`, `QueryCatalog` (locations, models, coverage),
  - commands: `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, `UpdateBudget`,
  - a config stream for `StreamConfig`.
  - Config travels as immutable contract records in `Messages/Config/`, not as options
    objects.
- **Budget status (usage plus effective budget)** comes from `BudgetTracker` via
  `QueryBudgetStatus`, so the API no longer calls `BudgetCalculator`.
- **gRPC services inject only `ActorRegistry`, `ActorSystem` (streaming RPCs) and
  `TimeProvider`.** Each RPC resolves an actor marker, asks with a message, and returns
  `result.ToProto()`. Mapping moves to `Njord.Grpc/Mapping/<Domain>ProtoMappings.cs` and
  replaces `CatalogMapper`, `PollStatusMapper` and `EnrichmentProtoMapper`.
- Messages and results use `LocationId` (from change 1) instead of raw location strings.
- Architecture tests enforce the boundary.

## Capabilities

### New Capabilities
- `api-adapter-boundary`: the gRPC API reaches the system only through messages and
  produces unchanged responses.

### Modified Capabilities
- `message-domain-separation`: Messages has no Akka dependency at all. Stream handshake
  records live in `Njord.Core/Actors/`. The stale enrichment/history namespace requirement
  is removed.
- `message-conventions`: the domain folders change, and the forecast query is answered by
  `ForecastReadModel`.
- `consumer-model`: the gRPC read model moves out of the API project and becomes
  `ForecastReadModel`.
- `grpc-mapping-layer`: mapping is done by per-domain extension methods over message
  results, and catalog mapping no longer reads the coverage registry itself.

## Non-goals

- No proto change. Every RPC returns byte-identical responses for the same state (pinned
  by the golden masters from change 1 plus the admin/ops/weather service specs).
- No change to MQTT output, the analysis logic or the options classification.
- No new RPCs, and no REST API.
- No change to polling; API budget impact: 0 additional requests/month.

## Impact

- `src/Njord.Messages` (csproj references, new folders, moved records),
  `src/Njord.Core/Egress/PipelineEvent.cs` (moved), `src/Njord.Core/Actors/` (stream
  handshake records, new markers `IForecastReadModel`).
- `src/Njord.Grpc/*` (all services, new `Mapping/`, `GrpcConsumer` removed),
  `src/Njord.Pipeline` (new `ForecastReadModel`), `src/Njord/Configuration/ConfigManager.cs`
  (queries, commands, config stream), `src/Njord.Budget` (`QueryBudgetStatus`).
- `src/Njord.Mqtt` (namespace updates for `PipelineEvent`).
- Tests: `Njord.Grpc.Tests`, `Njord.Pipeline.Tests`, `Njord.Budget.Tests`, `Njord.Tests`,
  `Njord.Architecture.Tests`. AGENTS.md (solution structure, reference direction).
