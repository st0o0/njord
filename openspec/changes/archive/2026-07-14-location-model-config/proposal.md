## Why

Models are configured globally — every location gets every model. Regional
models like `icon_d2` (DE+CH+AT only) or `knmi_harmonie_arome_netherlands`
(NL+BE only) fail with HTTP 400 for locations outside their geographic
coverage. The SchedulerActor retries these failures with backoff, wasting
budget on models that will never produce data. There is no validation to
catch configuration mistakes — a user adding `meteoswiss_icon_ch1` for a
Berlin location discovers the problem only through repeated error logs.

## What Changes

- **Per-location model list** — `LocationOptions` gains a `Models` property.
  Effective models for a location = global `Models` merged with
  location-specific `Models` (deduplicated). Locations without a `Models`
  property get only the global list.
- **`ModelCoverageRegistry`** — static registry mapping model IDs to
  coverage tiers (`Global`, `Europe`, `Regional`) with bounding boxes for
  regional models. Based on Open-Meteo documentation.
- **Startup validation** — `NjordOptionsValidator` resolves effective models
  per location and checks each pair against the coverage registry. A
  location outside a model's bounding box produces a startup warning log.
  Unknown model IDs (not in the registry) are allowed with a warning — new
  Open-Meteo models work without code changes.
- **Budget calculation updated** — accounts for per-location model counts
  instead of `locations.Count × models.Count`.
- **SchedulerActor and DiscoveryActor** iterate resolved models per
  location instead of the global model list.

## Non-goals

- Runtime probing (sending test requests at startup) — the static registry
  is sufficient and costs zero API calls.
- Exact bounding box precision — boxes are intentionally generous. Borderline
  cases fall through to the existing runtime 400 handling.
- Per-location poll intervals or horizons — only models vary per location.
- API-budget impact: zero additional HTTP requests.

## Capabilities

### New Capabilities

- `location-model-resolution`: Per-location model merging, coverage
  registry with bounding boxes, and startup validation.

### Modified Capabilities

- `service-configuration`: `LocationOptions` gains `Models` property,
  budget calculation accounts for per-location model counts.
- `poll-scheduler`: Iterates resolved models per location instead of
  global model list.

## Impact

- **New:** `ModelCoverageRegistry.cs` (coverage tiers and bounding boxes).
- **Modified:** `LocationOptions.cs`, `NjordOptionsValidator.cs`,
  `SchedulerActor.cs`, `DiscoveryActor.cs`, `appsettings.json`.
- **Tests:** Coverage registry tests, validation tests, scheduler tests
  with per-location models.
- **No new packages.**
