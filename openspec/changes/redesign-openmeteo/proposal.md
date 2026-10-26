## Why

The OpenMeteo fetch path has three structural problems: fragmented DI resolution (three
factory lambdas independently iterate feature rules to derive domains, requirements and models),
an overloaded gateway actor (235 LOC mixing budget, pipeline coupling, request tracking and
unbounded concurrency), and 80 magic string literals for API variable names scattered across
six switch-based mapping files. These problems are interconnected and best solved as one
coherent redesign rather than piecemeal fixes.

## What Changes

- **FetchPlan singleton.** One record (`FetchPlan`) computed once from enabled features and
  options, replacing the three separate factory lambdas in `DomainSetupContainer` and the
  five-parameter reassembly in `BudgetGuard`.
- **Dedicated API variable enums.** Per-domain enums in `Njord.OpenMeteo` (e.g.
  `WeatherApiVariable`) with `[JsonStringEnumMemberName]` for API string names. Domain enums
  in `Njord.Forecast` stay API-free. Replaces six `*ParamsMappingExtensions` switch files with
  explicit domain-to-API enum switches plus a cached `FrozenDictionary` for string lookup.
- **Converter reflection removal.** Replace the reflected `BaseNames` set in
  `WeatherResponseConverter` with a hand-written `FrozenSet<string>` constant. No structural
  rewrite; the converter's `JsonDocument`-based multi-model splitting and source-generated
  deserialization stay.
- **Client query constants.** Named constants for query keys (`hourly`, `models`,
  `timeformat`) and endpoint paths, replacing inline string literals.
- **Gateway rewrite (FunkArr pattern).** `Source.Queue` + budget gate stage +
  `SelectAsyncUnordered(maxConcurrent)` for bounded concurrent fetching. Pipeline offer stays
  as an internal stream branch. Actor reduced to lifecycle, queue ref and pending tracking
  (~100 LOC target from 235).
- **Project restructuring.** Folder per domain (`Weather/`, `AirQuality/`, `Flood/`,
  `Marine/`, `Seasonal/`, `Satellite/`) with client, API enum, DTOs and mappings together.
  Shared concerns in `Gateway/`, `Budget/`, `Http/`, `Configuration/`.

## Non-goals

- Changing the six clients structurally (they are already good thick clients).
- Replacing the response DTOs (the `[JsonPropertyName]` records are clean).
- Changing the `FetchDomain`/`FetchResult` contract between PollWorker and gateway.
- Hot-reload or runtime config changes.
- Touching `Njord.Forecast` domain enums beyond what they already expose.

## API budget impact

No change to polling frequency, API call patterns or call weight formula. The gateway rewrite
adds bounded concurrency (max N in-flight) which may slightly smooth request timing but does
not increase total calls. Budget estimate unchanged from current: locations x required domains
x poll cycles x call weight, validated by `BudgetGuard` at startup against 80% of the 300k
monthly limit.

## Capabilities

### New Capabilities

- `fetch-plan`: Unified resolution of enabled features into a single startup-computed plan
  (domains, requirements, models, call weights).
- `api-variable-enums`: Per-domain API variable enums with `[JsonStringEnumMemberName]` and
  cached string lookup, replacing switch-based param-to-string mappings.
- `gateway-stream-dispatch`: Stream-based concurrent fetch dispatch with bounded parallelism
  and integrated budget gating.
- `openmeteo-project-layout`: Domain-folder-based project structure for `Njord.OpenMeteo`.

### Modified Capabilities

- `domain-setup-containers`: `DomainSetupContainer` registrations replaced by `FetchPlan`;
  `OpenMeteoSetupContainer` clients take `FetchPlan` instead of `ResolvedRequirements`.
- `domain-clients`: Clients receive API variable lists from `FetchPlan` through the new API
  enums instead of `ResolvedRequirements.ToApiNames()`.
- `domain-requirements`: `ResolvedRequirements` wrapper type removed; its data moves into
  `FetchPlan`.
- `param-enums`: Domain-side `XxxParams` enums unchanged; the mapping to API strings moves
  from switch extensions to API enum mapping extensions.

## Impact

- **Njord.Core**: new `FetchPlan` record in `Domain/`, `FeatureResolution` gains a
  `BuildFetchPlan` method.
- **Njord.OpenMeteo**: new API variable enums and mapping extensions per domain, gateway
  rewrite, converter fix, project restructuring (file moves).
- **Njord.Polling**: `BudgetGuard` simplified to take `FetchPlan`.
  `PollingOptionsValidator` takes `FetchPlan`.
- **Njord (Host)**: `DomainSetupContainer` simplified to register one `FetchPlan` singleton.
- **Test projects**: `Njord.Core.Tests`, `Njord.OpenMeteo.Tests`, `Njord.Polling.Tests`
  updated for new types and simplified setup.
