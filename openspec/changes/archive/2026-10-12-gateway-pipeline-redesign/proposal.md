## Why

The current PipelineActor is a monolith that mixes API fetching, budget throttling, scheduler feedback, and analysis into a single stream graph. WeightedBudgetGate holds a raw IActorRef resolved eagerly at DI time — fragile and inconsistent with the lazy GetAsync pattern used elsewhere. Actor naming diverges from the FunkArr convention (domain roles vs *Actor suffix). The StopStreams/StreamsStopped CoordinatedShutdown pattern is a workaround for noisy shutdown logs that FunkArr has proven unnecessary. This refactoring separates concerns into distinct domains, each with a clear boundary and a clean streaming interface.

## What Changes

- **BREAKING**: Rename `Njord.Ingest` project to `Njord.OpenMeteo`; move `IOpenMeteoClient` from `Njord.Core/Ingest/` to `Njord.OpenMeteo` (public, for test access); remove `Njord.Core.Ingest` namespace
- **BREAKING**: Rename all actor classes to FunkArr-style domain roles (no `*Actor` suffix): `SchedulerActor`→`PollScheduler`, `PipelineActor`→`ForecastPipeline`, `BudgetTrackerActor`→`BudgetTracker`, `SensorHubActor`→`SensorHub`, `GrpcConsumerActor`→`GrpcConsumer`, `MqttConnectionActor`→`MqttConnection`, `MqttStateActor`→`MqttState`
- **BREAKING**: Rename all actor keys: `ISchedulerActor`→`IPollScheduler`, `IPipelineActor`→`IForecastPipeline`, `IBudgetTrackerActor`→`IBudgetTracker`, `ISensorHubActor`→`ISensorHub`, `IGrpcConsumerActor`→`IGrpcConsumer`; add `IForecastGateway`
- Add `OpenMeteoGateway` actor in `Njord.OpenMeteo` — owns the fetch stream (`Source.Queue → BudgetThrottle → SelectAsyncUnordered → WireTap(scheduler feedback) → DivertTo(failures) → BroadcastHub<Forecast>`), token bucket rate limiting, and hash computation
- Create `Njord.Budget` project — extract `BudgetTracker` persistent actor (usage accounting) from `Njord.Pipeline`; receives fire-and-forget `RecordApiCall` from Gateway, queried by gRPC admin
- Simplify `ForecastPipeline` — receives `SourceRef<Forecast>` from Gateway, owns only analysis stream (`ZipWith(sensors) → StatefulSelectMany → BroadcastHub<PipelineEvent>`)
- Simplify `PollScheduler` — sends `FetchForecast(location, model)` to Gateway instead of `WeightedTarget` to Pipeline; no longer computes API call weight
- New messages: `FetchForecast`, `ForecastFetched`, `ForecastFetchFailed`, `RequestForecastStream`/`ForecastStreamReady`/`ForecastStreamFailed`
- Delete: `WeightedBudgetGate`, `IBudgetGate<T>`, `IBudgetProvider`, `OptionsBudgetProvider`, `WeightedTarget`, `HashResult`, `FetchFailed`, `StopStreams`/`StreamsStopped`, `KillSwitch` usage, `AddStreamShutdownTask`, `PipelineActorShutdownSpec`
- Replace `Select` side-effects with `WireTap` for scheduler feedback and `DivertTo` for success/failure split
- Standardize IActorRef resolution: `Context.GetActor<T>()` in actors, `GetAsync<T>()` in services; remove raw `IActorRef` from DI service constructors (`SensorGrpcService`)

**API-budget impact**: No change — polling frequency, request weight calculation, and Open-Meteo API usage remain identical. The token bucket and budget tracking move between actors but the behavior is preserved.

## Non-goals

- Changing analysis logic (consensus, alerts, derived, trends, indices)
- Changing PipelineEvent types or egress consumer behavior
- Changing MQTT/gRPC consumer patterns
- Changing persistence provider, DTO format, or wire format
- Adding new enrichment features or API endpoints
- Changing poll interval or budget limits

## Capabilities

### New Capabilities
- `forecast-gateway`: OpenMeteoGateway actor that encapsulates all Open-Meteo API interaction (fetch stream, rate limiting, hash computation, scheduler feedback) and exposes forecasts via StreamRef
- `budget-domain`: Separate Njord.Budget project with BudgetTracker persistent actor for usage accounting, independent of the fetch mechanism
- `actor-domain-naming`: FunkArr-style actor naming convention (domain roles, no *Actor suffix) applied consistently across all actors and keys

### Modified Capabilities
- `poll-scheduler`: PollScheduler sends FetchForecast to Gateway instead of WeightedTarget to Pipeline; no longer computes weight; watches Gateway instead of Pipeline
- `poll-pipeline`: ForecastPipeline receives SourceRef<Forecast> from Gateway; no longer owns fetch stream, budget gate, or scheduler feedback; no StopStreams
- `openmeteo-client`: IOpenMeteoClient moves from Njord.Core/Ingest to Njord.OpenMeteo; OpenMeteoClient and DTOs rename from Njord.Ingest to Njord.OpenMeteo
- `dynamic-budget-throttle`: BudgetThrottleStage moves from Njord.Pipeline to Njord.OpenMeteo (Gateway-internal); token bucket becomes Gateway-internal state
- `budget-tracker-persistence`: BudgetTracker moves to Njord.Budget project; no longer a child of Pipeline registration; registered independently
- `consumer-model`: Consumer SourceRef request pattern unchanged but ForecastPipeline no longer has StopStreams/KillSwitch shutdown

## Impact

- **Projects**: Njord.Ingest renamed to Njord.OpenMeteo; new Njord.Budget project; Njord.Pipeline loses budget/throttle code
- **References**: All projects referencing Njord.Ingest update to Njord.OpenMeteo; Njord.Core loses Ingest namespace; test projects update FakeOpenMeteoClient imports
- **Architecture tests**: LayerReferenceSpec and ZoneArchitectureSpec updated for new project names and reference directions
- **Actor registration**: PipelineSetupContainer restructured — Gateway + BudgetTracker registration moves to OpenMeteoSetupContainer and BudgetSetupContainer
- **Messages**: New message types in Njord.Messages; old types deleted (WeightedTarget, HashResult, FetchFailed, StopStreams, StreamsStopped)
- **Test projects**: Njord.Ingest.Tests renamed to Njord.OpenMeteo.Tests; Njord.Budget.Tests created; Njord.Pipeline.Tests updated (fewer tests — shutdown spec deleted, budget/throttle specs move)
- **AGENTS.md / CLAUDE.md**: Solution structure, actor naming convention, test counts updated
