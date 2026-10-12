## Context

The current forecast data flow is a single monolithic stream graph inside PipelineActor: `Source.Queue<WeightedTarget> → BudgetThrottleStage → SelectAsyncUnordered(fetch) → Select(scheduler feedback side-effect) → Collect(Success) → ZipWith(sensors) → StatefulSelectMany(analysis) → BroadcastHub<PipelineEvent>`. The budget gate is a DI singleton (`WeightedBudgetGate`) holding a raw `IActorRef` to `BudgetTrackerActor`. Actor naming uses `*Actor` suffix throughout, diverging from the FunkArr convention of domain-role names.

FunkArr's `MediathekViewWebManager` demonstrates the gateway pattern: an actor that owns `Source.Queue → SelectAsyncUnordered → Sink.ActorRef(Self)`, correlates requests by key, and replies to the original sender. FunkArr uses no `StopStreams` workaround — the ActorMaterializer lifecycle handles stream cleanup automatically.

## Goals / Non-Goals

**Goals:**
- Separate fetch, analysis, and budget into distinct domain boundaries (OpenMeteo, Pipeline, Budget)
- Apply FunkArr's proven gateway pattern for API interaction
- Connect stages via StreamRef for symmetric, backpressure-aware data flow
- Use built-in Akka.Streams operators (WireTap, DivertTo) instead of custom side-effect patterns
- Standardize actor resolution to `Context.GetActor<T>()` in actors and `GetAsync<T>()` in services
- Adopt FunkArr-style domain-role naming across all actors

**Non-Goals:**
- Changing analysis logic, PipelineEvent types, or enrichment features
- Changing persistence format or provider
- Adding new API endpoints or polling behavior

## Decisions

### Decision 1: Gateway pattern with internal stream and BroadcastHub output

**Choice:** OpenMeteoGateway owns `Source.Queue → BudgetThrottle → SelectAsyncUnordered(2, fetch) → WireTap(scheduler feedback) → DivertTo(failures) → BroadcastHub<Forecast>`. PipelineActor requests a `SourceRef<Forecast>` from the Gateway via Ask.

**Why:** FunkArr's MediathekViewWebManager proves this pattern. The Gateway encapsulates all API interaction: HTTP fetching, rate limiting, hash computation, and error routing. The stream stays inside the actor — no DI services hold raw IActorRef. StreamRef provides backpressure between Gateway and Pipeline, and the pattern is symmetric with Pipeline → Consumer (both use `BroadcastHub → SourceRef`).

**Alternative considered:** Direct Tell messages (Gateway → Pipeline) instead of StreamRef. Simpler, but loses backpressure and breaks the symmetric pattern. At 10 fetches per hour the throughput doesn't need backpressure, but consistency with the downstream SourceRef pattern is worth more than saving one StreamRef.

**Alternative considered:** Gateway as SourceRef-only (no scheduler feedback). Would require the Pipeline to forward HashResult/FetchFailed to the Scheduler, re-introducing coupling. WireTap in the Gateway's stream handles feedback cleanly without Pipeline involvement.

### Decision 2: Request correlation via location|model key

**Choice:** Gateway tracks pending requests in `Dictionary<string, IActorRef>` keyed by `$"{location}|{model}"`. When a stream result arrives via `Sink.ActorRef(Self)`, the Gateway looks up the requester and replies with `ForecastFetched(hash)` or `ForecastFetchFailed(reason)`.

**Why:** Simpler than FunkArr's `Dictionary<Guid, IActorRef>` pattern because the Scheduler never sends duplicate requests for the same location|model — it waits for feedback before scheduling the next poll for that key. No Guid generation needed.

### Decision 3: Budget as separate domain (Njord.Budget)

**Choice:** Extract `BudgetTracker` (persistent actor) into its own `Njord.Budget` project. The token bucket (rate limiting) stays Gateway-internal as in-memory state. The Gateway tells `BudgetTracker` via fire-and-forget `RecordApiCall` after each successful acquire.

**Why:** The token bucket and the usage tracker serve different purposes. The token bucket is a flow-control mechanism tied to the stream (Gateway-internal). The BudgetTracker is a persistent accounting service queried by gRPC admin (`QueryBudgetUsage`). Separating them means the Gateway doesn't need persistence, and Budget is independently testable and reusable.

**Alternative considered:** BudgetTracker as child of Gateway. Simpler actor hierarchy but couples persistence lifecycle to the Gateway's BackoffSupervisor — a Gateway restart would also restart the BudgetTracker, losing in-flight persistence. Independent registration is safer.

### Decision 4: Gateway sends all forecasts unconditionally

**Choice:** Every successful fetch goes through the BroadcastHub, regardless of whether the hash changed. The Gateway has no hash state — it computes the hash and forwards it to the Scheduler as feedback, but doesn't filter.

**Why:** Keeps the Gateway stateless (except pending requests). The Pipeline's `StatefulSelectMany` is idempotent — `ModelSnapshot.Update()` with an identical forecast produces identical consensus. The overhead is negligible (10 forecasts/hour, maybe 3 unchanged). Adding hash-based dedup in the Gateway would duplicate the Scheduler's logic and add state.

### Decision 5: WireTap for scheduler feedback, DivertTo for success/failure split

**Choice:** Replace the current `Select` side-effect pattern with built-in operators:
- `WireTap(Sink.ForEach<PollOutcome>(outcome => { schedulerFeedback }))` — explicit side-channel
- `DivertTo(failureSink, o => o is Failure)` — failures routed out, main flow continues with successes

**Why:** `WireTap` makes the side-channel explicit — the main flow is unaffected. `DivertTo` replaces the `Select + Collect` pattern with a cleaner declarative split. Both are built-in Akka.Streams operators, reducing custom code.

### Decision 6: Remove StopStreams/StreamsStopped shutdown workaround

**Choice:** Delete `StopStreams`, `StreamsStopped`, `AddStreamShutdownTask`, `KillSwitch` usage, and `PipelineActorShutdownSpec`. Let ActorMaterializer lifecycle handle stream cleanup during CoordinatedShutdown.

**Why:** FunkArr uses the same streaming patterns and has no shutdown workaround. When an actor stops, its ActorMaterializer stops, which terminates all stream stages. `AbruptTerminationException` is logged but harmless — it can be suppressed via Akka log config (`akka.log-dead-letters-during-shutdown = off`) if needed.

### Decision 7: FunkArr-style actor naming

**Choice:** Rename all actors to domain roles (no `*Actor` suffix):

| Current | New | Key |
|---|---|---|
| `SchedulerActor` | `PollScheduler` | `IPollScheduler` |
| `PipelineActor` | `ForecastPipeline` | `IForecastPipeline` |
| `BudgetTrackerActor` | `BudgetTracker` | `IBudgetTracker` |
| `SensorHubActor` | `SensorHub` | `ISensorHub` |
| `GrpcConsumerActor` | `GrpcConsumer` | `IGrpcConsumer` |
| `MqttConnectionActor` | `MqttConnection` | `IMqttConnection` |
| `MqttStateActor` | `MqttState` | `IMqttState` |
| _(new)_ | `OpenMeteoGateway` | `IForecastGateway` |

**Why:** FunkArr uses `DownloadManager`, `SearchManager`, `MediathekViewWebManager` — domain roles that describe what the actor does, not that it's an actor. The key describes the logical role (`IForecastGateway`), not the implementation (`IOpenMeteoGateway`).

### Decision 8: IOpenMeteoClient moves to Njord.OpenMeteo

**Choice:** Move `IOpenMeteoClient` from `Njord.Core/Ingest/` to `Njord.OpenMeteo` (public). Remove the `Njord.Core.Ingest` namespace. The key `IForecastGateway` in Core is the abstraction boundary — downstream code never touches `IOpenMeteoClient`.

**Why:** `IOpenMeteoClient` is an implementation detail of the OpenMeteo domain. Core should not reference API-specific types. Tests that need `FakeOpenMeteoClient` already reference the test shared project, which can reference `Njord.OpenMeteo`. The Gateway actor key `IForecastGateway` in Core is the stable contract.

### Decision 9: Standardize IActorRef resolution

**Choice:** All actor-to-actor resolution uses `Context.GetActor<TKey>()` (Servus extension). All service-to-actor resolution uses `IActorRegistry.GetAsync<TKey>(ct)` + `Ask<TResponse>`. No raw `IActorRef` in DI service constructors.

**Why:** `SensorGrpcService` currently uses eager `ActorRegistry.Get<T>()` — fragile if the actor isn't registered yet. `OpsGrpcService` already uses the correct lazy pattern. Standardizing eliminates a class of startup-ordering bugs.

### Decision 10: Project Njord.Ingest renamed to Njord.OpenMeteo

**Choice:** Rename the project, namespace, csproj, and test project. Update all references.

**Why:** FunkArr names projects after the domain concept (`Search`, `Download`), not the technical role (`Ingest`). `Njord.OpenMeteo` says exactly what's inside.

## Risks / Trade-offs

**[StreamRef overhead between Gateway and Pipeline]** → StreamRef adds materialization overhead vs direct Tell messages. Acceptable because: (a) throughput is ~10 forecasts/hour, (b) provides backpressure, (c) symmetric with downstream pattern.

**[Startup ordering: Pipeline asks Gateway for SourceRef]** → Pipeline must wait for Gateway to materialize its BroadcastHub before the SourceRef request succeeds. Mitigation: Pipeline resolves Gateway via `Context.GetActor<IForecastGateway>()` and retries the Ask with backoff (same pattern as current Scheduler → Pipeline resolution).

**[Large rename scope]** → Renaming all actors + keys + projects touches many files and test projects. Mitigation: Mechanical rename (find-replace), architecture tests validate reference directions, full test suite validates behavior.

**[BudgetTracker persistence ID unchanged]** → `BudgetTracker`'s `PersistenceId` stays `"budget-tracker"` despite moving projects. Existing journals remain valid. No migration needed.

**[Gateway token bucket state lost on restart]** → The in-memory token bucket resets when the Gateway restarts (BackoffSupervisor). The BudgetTracker's persistent monthly/daily counts survive. Token bucket refills naturally — a brief burst after restart is bounded by `SelectAsyncUnordered(2)` concurrency.

## Open Questions

_(none — all design questions were resolved during explore)_
