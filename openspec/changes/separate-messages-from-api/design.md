## Context

See proposal.md for motivation. The pattern this change borrows from FunkArr
(`FunkArr.Messages` has an empty csproj; `FunkArr.Api` = endpoints + `Models/` +
`Extensions/*MappingExtensions` with `ToApi()`; endpoints only resolve, ask and map). In
njord, the proto-generated types already play the role of `Api/Models`.

Current gRPC dependencies outside messages:

| Service | Reads outside messages |
|---|---|
| `WeatherGrpcService` | `IOptions<NjordOptions>` (catalog, location/model validation), `CatalogMapper` → `ModelCoverageRegistry`, nested `GrpcConsumer.*` messages |
| `OpsGrpcService` | `IOptionsMonitor<NjordOptions>`, `BudgetCalculator.GetEffectiveBudget`, enrichment toggles from options |
| `AdminGrpcService` | options monitor + config writer (after change 2: section writer), `BudgetCalculator.Validate` |
| `SensorGrpcService` | `IOptions<NjordOptions>` |
| `GrpcConsumer` | an actor in the API project |

This change assumes `split-options-per-feature` delivered `ConfigManager`, the section
writer and `ConfigReloadPolicy`, and `typed-analysis-models` delivered `LocationId` and
the golden masters.

## Goals / Non-Goals

**Goals:** one contract project without Akka; an API that only asks and maps; no actor
or configuration access in the API project.

**Non-Goals:** proto changes, a REST API, MQTT changes.

## Decisions

### D1: Messages references Weather, Sensors and Compute
The result records consumers need (`ConsensusSnapshot`, `AlertResult`, …) are Compute
contracts. Messages references Compute instead of duplicating them.
*Alternative:* FunkArr-style, with results physically in Messages and Compute referencing
Messages. Rejected because it would invert the pure-computation layer (Compute would depend
on the messaging contract), and `restructure-compute` already isolates the contracts in
`Compute/Contracts/`. `LayerReferenceSpec` is updated to allow Messages → Compute.

### D2: Config travels as contract records, not options
`Messages/Config/ConfigSnapshot` is an immutable record tree (locations, models, horizons,
forecast days, parameters, polling, budget, analysis settings) built by `ConfigManager`
from the options. Update commands carry contract inputs, and `ConfigManager` applies them
through the section writer. This keeps options types (Core) out of Messages and lets the
API map from one stable shape.
*Alternative:* let the API keep reading options. Rejected because that is exactly the
coupling this change removes.

### D3: `ForecastReadModel` lives in `Njord.Pipeline`
It is a projection of the pipeline's output. Placing it next to the pipeline keeps the
API free of actors and gives the read model an owner that understands `PipelineEvent`.
Its marker `IForecastReadModel` goes in `ActorKeys.cs`, and it is registered in
`NjordActorSystemSetup`.
*Alternative:* its own library. Rejected as too small to justify a project.

### D4: Stream access via Core handshakes
`RequestForecastStream` moves to `Njord.Core/Actors/StreamWiringMessages.cs`. New
handshakes: `RequestReadModelStream` (secondary hub, replaces
`GrpcConsumer.RequestGrpcStreamSource`) and `RequestConfigStream` (for `StreamConfig`, a
`BroadcastHub<ConfigSnapshot>` in `ConfigManager`).

### D5: One ask helper with status mapping
`Njord.Grpc/ActorAsk.cs` resolves a marker via `ActorRegistry` and asks with the timeout.
It maps resolution failure to `Unavailable` and `AskTimeoutException` to
`DeadlineExceeded`, and it forwards `TestServerCallContext` cancellation. All RPCs use it,
which also removes the per-service `AskTimeout` constants.

### D6: Per-domain mapping extensions
`Njord.Grpc/Mapping/ForecastProtoMappings.cs`, `AnalysisProtoMappings.cs`,
`ConfigProtoMappings.cs`, `CatalogProtoMappings.cs`, `OpsProtoMappings.cs`,
`SensorProtoMappings.cs`. They are `internal static` with `ToProto()`/`ToCommand()`, and
the existing mapper specs and golden masters move along.

### D7: Architecture rules
- `Njord.Messages` has no Akka dependency.
- `Njord.Grpc` contains no `ActorBase` subtypes.
- `Njord.Grpc` does not depend on `IOptions*`, `IConfigWriter`, `BudgetCalculator` or
  `ModelCoverageRegistry`.
- No public record nested in an actor in any production assembly.

## Risks / Trade-offs

- [`ConfigSnapshot` duplicates the options shape] → It is the one deliberate mapping
  point. A spec builds a full options set and asserts that every value reaches the
  snapshot, so new options fields can't be silently dropped.
- [Error behavior changes: an unresolved actor previously surfaced as an unhandled
  exception] → Mapping to `Unavailable`/`DeadlineExceeded` is the documented target. The
  ha-njord integration treats both as retryable, so verify this before release.
- [Moving `PipelineEvent` touches Mqtt, Grpc, Pipeline] → It is a namespace-only change,
  done in one task with the golden masters as the guard.

## Migration Plan

Internal only; no proto, config or persistence change. Roll back by reverting.

## Deviations

Adaptations made during implementation, where the code differed from what the tasks assumed.

### Group 1 (contract project)
- `SubscribeInbound` carried an `IActorRef Listener`. To keep it in `Messages/Mqtt/` without an
  Akka type it is now a parameterless record; `MqttConnection` registers the `Sender` as the
  inbound listener.
- `EnrichmentKind` (with `ToWireName`) moved with `PipelineEvent` to `Messages/Pipeline/`, since
  `IEnrichmentEvent.Kind` needs it. `Njord.Core/Egress/` no longer exists.
- `LocationId` replaces the raw location string in `FetchForecast*`, `DataChanged`,
  `PollStateEntry`, `ScheduledPoll`, `TriggerImmediatePoll`, `QuerySensorSnapshot`,
  `SensorSnapshotNotAvailable`, `QueryForecast` and `PipelineEvent.ForecastReceived`.
  `TriggerImmediatePoll.Location` is `LocationId?` (null = all locations, which the empty
  string meant before), and its `Targets` became `IReadOnlyList<string>`. Model ids stay
  strings. Domain records outside Messages (`Forecast.Location`, `SensorReading.Location`) and
  persistence DTOs keep strings; actors convert at their edges (`.Name` / `new LocationId(...)`).

### Group 2 (read model)
- `NjordActorSystemSetup` does not exist. `ForecastReadModel` is registered as a singleton under
  `IForecastReadModel` by the existing `IActorRegistration` in `PipelineSetupContainer`; the
  gRPC setup container no longer registers an actor. `IGrpcConsumer` is replaced by
  `IForecastReadModel`.
- The analysis query is `QueryLatestAnalysis` → `LatestAnalysisResult(Location, Analysis)` or
  `LatestAnalysisNotAvailable(Location)` (the "no data yet" case), following the existing
  `ForecastResponse` shape. `QueryForecast` stays answered with `ForecastResult` /
  `ForecastNotAvailable`.
- The read model keeps its state keyed by the location name as the pipeline reports it
  (ordinal), as `GrpcConsumer` did, so lookups behave exactly as before.
- There were no `GrpcConsumer` specs to port; `ForecastReadModelSpec` is new. The dead-client
  scenario is covered by cancelling one streaming client and asserting that another one still
  receives events (the hub removes cancelled subscribers); it does not wait for a real idle
  timeout.
