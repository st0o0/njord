## Context

The OpenMeteo fetch path has grown organically into a shape that mixes concerns:

- **OpenMeteoGateway** (235 LOC singleton actor) owns budget gating, pipeline coupling via
  SinkRef/SourceQueue, request tracking, telemetry and unbounded concurrent fetching through
  raw `Task.PipeTo`. No backpressure, no concurrency limit.
- **DomainSetupContainer** registers three derived singletons (`IReadOnlySet<DomainKind>`,
  `EffectiveWeatherModels`, `ResolvedRequirements`) through separate factory lambdas that each
  iterate `IFeatureRule[]` independently. `BudgetGuard` then reassembles five parameters from
  these singletons and options classes.
- **WeatherResponseConverter** (108 LOC) uses reflection at static init to read
  `[JsonPropertyName]` attribute values from `WeatherHourlyData`, building a base-name lookup
  for multi-model JSON key splitting. The actual deserialization already uses the
  source-generated `OpenMeteoJsonContext`.
- **Six `*ParamsMappingExtensions`** files (472 LOC in `Mapping/`) map domain `[Flags]` enum
  members to API variable name strings via explicit switch statements. ~80 magic string
  literals total.
- **Six domain clients** (274 LOC total) are already clean thick clients implementing
  `IDomainClient`: each owns HTTP, URL building, response mapping and error handling via
  `FetchResult`. These stay structurally unchanged.
- **Project layout** is flat: 47 files in the project root mixing clients, DTOs, mappings,
  budget types, HTTP plumbing, configuration and the gateway.

## Goals / Non-Goals

**Goals:**

- Unified startup resolution: one `FetchPlan` record replaces three factory lambdas.
- Clean API-to-domain enum separation: domain enums in `Njord.Forecast` stay API-free;
  dedicated API enums in `Njord.OpenMeteo` carry the Open-Meteo variable names.
- Bounded concurrent fetching: FunkArr `Source.Queue` + `SelectAsyncUnordered` pattern with
  integrated budget gating as a stream stage.
- Organized project layout: one folder per domain, shared concerns grouped.

**Non-Goals:**

- Changing the six clients structurally.
- Replacing the response DTOs.
- Changing the `FetchDomain`/`FetchResult` contract.
- Hot-reload or runtime config changes.
- Touching `Njord.Forecast` domain enums.

## Decisions

### 1. FetchPlan as a single record

One record computed once at startup from enabled feature rules and options, registered as a
singleton.

```
FetchPlan
  Domains          : IReadOnlySet<DomainKind>
  Requirements     : DomainRequirements     // with base params applied
  Models           : EffectiveWeatherModels
  WeatherCallWeight: int                    // precomputed
```

`FeatureResolution.BuildFetchPlan(rules, weatherOptions, ...)` creates it.
`DomainSetupContainer` registers one `FetchPlan` singleton instead of three separate ones.
`BudgetGuard` takes `FetchPlan` instead of reassembling from five parameters. Clients receive
their API variable lists through `FetchPlan.Requirements`.

**Why over alternatives:** Keeping three separate singletons computed from a shared method
still forces consumers to combine them. One record, one computation, one registration.

### 2. Dedicated API variable enums in Njord.OpenMeteo

Per-domain enums (`WeatherApiVariable`, `AirQualityApiVariable`, `FloodApiVariable`,
`MarineApiVariable`, `SeasonalApiVariable`, `SatelliteApiVariable`) with
`[JsonStringEnumMemberName("temperature_2m")]` on each member. A cached
`FrozenDictionary<TEnum, string>` built once from the attributes provides programmatic string
lookup without serialization round-trips.

The mapping chain:

```
WeatherParam (Flags, Forecast)       WeatherApiVariable (OpenMeteo)       string
  .Temperature          ──switch──▶    .Temperature       ──cached──▶   "temperature_2m"
  .WindSpeed             (explicit)    .WindSpeed          (FrozenDict)  "wind_speed_10m"
```

Domain-to-API enum mapping uses the existing convention: explicit switch, default throws
`ArgumentOutOfRangeException`. This is an enum-to-enum mapping. The API-enum-to-string lookup
is a serialization concern, not the switch convention.

**Alternatives rejected:**

- (A) `[JsonStringEnumMemberName]` directly on `Njord.Forecast` enums: couples the API-free
  vocabulary to Open-Meteo naming.
- (B) `FrozenDictionary` lookup tables without enums: loses type safety and compile-time
  exhaustiveness checking from the switch.

### 3. WeatherResponseConverter: reflection removal only

Replace the reflected `BaseNames` set with a hand-written `FrozenSet<string>` constant listing
the ~30 API variable base names. The converter's `JsonDocument`-based multi-model key splitting
and source-generated deserialization via `OpenMeteoJsonContext.Default.WeatherHourlyData` stay
unchanged.

**Why not a full rewrite:** The converter's architecture is already correct. It uses
`JsonDocument` to parse, splits keys by model suffix, builds per-model `JsonObject`s, and
deserializes each with the source-generated context. Only the key-name discovery uses
reflection. A `FrozenSet` constant eliminates that in ~5 lines.

**Why not `[JsonExtensionData]`:** Still requires a base-name list to group dynamic keys into
model buckets. Same core problem, more indirection.

### 4. Budget gate as a stream stage

Budget acquisition (`TryAcquire`) runs as a `MapAsync(1, ...)` stage before
`SelectAsyncUnordered`. Rejected requests reply inline with `FetchDomainRejected` and do not
enter the concurrent fetch stage.

**Why in the stream:** Keeps the budget check in the same backpressure-aware pipeline as the
fetch. The alternative (check in actor before queue offer) splits the concern across actor
message handling and stream processing.

### 5. Pipeline offer as internal stream branch

The gateway's fetch stream branches after successful fetches: one leg offers
`DomainDataReceived` to the pipeline's `Source.Queue`, the other returns the reply to the
gateway actor (which forwards to the original requester).

**Why not move to PollWorker:** The PollWorker receives only `FetchDomainCompleted` (a hash,
no data). Data goes to the pipeline through a SinkRef branch today; keeping that in the
gateway stream is a natural fit. Moving it adds a data hop and a new concern to PollWorker.

### 6. Gateway stream shape

```
Source.Queue<(FetchDomain request, IActorRef sender)>
  .Via(MapAsync(1, async req =>
      gate.TryAcquire(req.Domain)
        ? (req, acquired: true)
        : reply Rejected, (req, acquired: false)))
  .Where(acquired)
  .Via(SelectAsyncUnordered(maxConcurrent, async (req, sender) =>
      var result = await clients[req.Domain].FetchAsync(...)
      return (result, req, sender)))
  .Via(Branch: if Success, offer DomainDataReceived to pipeline queue)
  .To(Sink.ActorRef(Self, completed message))
```

`maxConcurrent` defaults to 4. With 6 domains, at most 4 in-flight HTTP calls. The rate limit
is 600/min; at 60-min poll intervals with a handful of locations, this is never the bottleneck.

The actor receives completed/failed results from `Sink.ActorRef`, resolves the original
sender, and replies with `FetchDomainCompleted`/`FetchDomainFailed`/`FetchDomainRejected`. The
actor body is lifecycle (PreStart materializes the stream), queue offering, and reply routing.
Target ~100 LOC.

### 7. Project layout

```
Njord.OpenMeteo/
  Weather/           WeatherClient, WeatherApiVariable, WeatherResponse,
                     WeatherHourlyData, WeatherResponseConverter,
                     WeatherApiVariableMappingExtensions,
                     WeatherResponseMappingExtensions
  AirQuality/        (same pattern per domain)
  Flood/
  Marine/
  Seasonal/
  Satellite/
  Gateway/           OpenMeteoGateway
  Budget/            GatewayBudgetGate, IGatewayBudgetGate,
                     IBudgetProvider, OptionsBudgetProvider, BudgetRate
  Http/              ClientRequest, HttpMetricsHandler, OpenMeteoJsonContext
  Configuration/     Options, OpenMeteoOptionsValidator, OpenMeteoSetupContainer
  Mapping/           DomainKindMappingExtensions, ForecastTrimmingExtensions,
                     SeriesAccess (shared utilities)
  Telemetry.cs
```

Each domain folder is self-contained: client, API enum, DTOs, and both mapping directions
(domain-to-API enum, response-to-domain-forecast).

### 8. AGENTS.md convention clarification

The existing convention "enum to wire text: explicit switch, one arm per member, default throws
`ArgumentOutOfRangeException`" applies to enum-to-enum mappings (`ToProto()`, `ToDomain()`,
`ToApiVariable()`). API variable string names are a JSON serialization concern handled by
`[JsonStringEnumMemberName]` and a cached `FrozenDictionary`, outside the scope of the switch
convention. AGENTS.md is updated to clarify this distinction.

## Risks / Trade-offs

- **[FetchPlan computed at startup]** If a feature rule is added after startup, `FetchPlan` is
  stale. Mitigation: options use `IOptions<T>` (not `IOptionsMonitor`); startup-only resolution
  is by design, and hot-reload is a stated non-goal.

- **[API enum member count must match domain enum flags]** A new domain enum flag without a
  corresponding API enum member compiles but fails at runtime. Mitigation: compile-time
  exhaustive switch in the mapping extension catches additions. A `Theory` test asserts every
  domain flag member has a corresponding API enum member.

- **[FrozenSet for converter base names is a manual duplicate]** The ~30 strings duplicate the
  `[JsonPropertyName]` values on `WeatherHourlyData`. Mitigation: a test asserts every
  `[JsonPropertyName]` in `WeatherHourlyData` appears in the `FrozenSet`.

- **[Project restructuring produces a large diff]** Phase 4 touches every file in the project.
  Mitigation: done last, after all functional changes are tested. Purely mechanical (file moves
  and namespace updates), no logic changes.

- **[Stream-based gateway adds complexity]** `SelectAsyncUnordered` is less obvious than
  `PipeTo`. Mitigation: the FunkArr `MediathekViewWebManager` (104 LOC) is the proven
  reference; the pattern is well-understood in this codebase.

## Open Questions

None. All design decisions were resolved during the explore session.
