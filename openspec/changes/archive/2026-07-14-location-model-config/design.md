## Context

njord polls multiple weather models per location. Models have geographic
coverage limits — regional models return HTTP 400 outside their domain.
Today all models are global config, causing wasted requests and confusing
error logs when a regional model is paired with an out-of-range location.

## Goals / Non-Goals

**Goals:**

- Per-location model lists with merge-from-global semantics.
- Static coverage validation at startup with clear log messages.
- Forward compatibility with unknown model IDs (warn, don't block).
- Budget calculation reflects actual per-location model counts.

**Non-Goals:**

- Runtime API probing at startup.
- Exact pixel-precise bounding boxes.
- Per-location poll intervals or horizons.

## Decisions

### Decision 1: Merge semantics (global + location-specific)

```
effective_models(location) = deduplicate(global_Models + location.Models)
```

If `location.Models` is null/empty, the location gets only the global list.
This means the global `Models` list is the baseline, and locations add
regionals on top. The user never has to repeat global models per location.

**Why not override:** Override forces the user to repeat global models in
every location. Merge is additive and less error-prone.

### Decision 2: Three-tier coverage registry

```csharp
enum CoverageTier { Global, Europe, Regional }

record ModelCoverage(CoverageTier Tier, BoundingBox? Bounds);

record BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}
```

- `Global` models: no bounds check needed.
- `Europe` models: checked against a generous European box (~34–72°N,
  -12–45°E).
- `Regional` models: checked against model-specific boxes.

The registry is a `Dictionary<string, ModelCoverage>` built statically.
Unknown model IDs return `null` — treated as "assumed global" with a
log warning.

### Decision 3: Bounding boxes from Open-Meteo documentation

All boxes are intentionally generous (1-2° padding) based on documented
coverage. If a model actually covers more than documented, the generous
box lets it through. If it covers less, the runtime 400 handler catches it.

| Model ID | Documented Coverage | Bounding Box |
|---|---|---|
| `icon_d2` | DE, CH, AT | 43–57°N, 1–18°E |
| `knmi_harmonie_arome_netherlands` | NL, BE | 49–55°N, 2–9°E |
| `metno_nordic` | NO, DK, SE, FI | 53–73°N, -1–33°E |
| `arome_france` / `arome_france_hd` | FR | 40–53°N, -6–10°E |
| `meteoswiss_icon_ch1` / `ch2` | CH & Central Europe | 44–50°N, 4–12°E |
| `geosphere_arome_austria` | AT | 45–50°N, 8–19°E |
| `arpae_2i` | IT | 35–49°N, 5–20°E |
| `ukmo_uk_2km` | UK, IE | 48–62°N, -12–4°E |
| `hrrr_us_conus` / `nbm` / `nam` | US, Canada | 23–51°N, -131–-59°E |
| `jma_msm` | Japan, Korea | 24–47°N, 122–151°E |
| `kma_ldps` | Korea | 32–44°N, 123–133°E |
| `gem_regional` | North America | 39–86°N, -146–-49°E |
| `gem_hrdps_continental` | Canada, North US | 39–65°N, -146–-49°E |

### Decision 4: Validation is warning, not error

A location outside a model's bounding box produces a log warning at
startup, not a hard failure. Reasons:

- Bounding boxes are approximate — the real domain might be larger.
- The user might have insider knowledge (e.g. a new Open-Meteo expansion).
- The runtime 400 handler is the safety net.

The warning message is actionable:
```
Model 'meteoswiss_icon_ch1' may not cover location 'berlin' (51.84°N, 13.41°E)
— documented coverage: CH & Central Europe (44–50°N, 4–12°E).
If this model does not return data, consider removing it from this location.
```

### Decision 5: LocationOptions resolves effective models

A new method `LocationOptions.ResolveModels(IList<string> globalModels)`
returns the deduplicated merged list. This is called by the validator,
SchedulerActor, and DiscoveryActor — single source of truth.

### Decision 6: SchedulerActor iterates per-location models

Today:
```csharp
foreach (var location in _options.Locations)
    foreach (var modelId in _options.Models)  // global list
```

After:
```csharp
foreach (var location in _options.Locations)
    foreach (var modelId in location.ResolveModels(_options.Models))
```

Same for DiscoveryActor's `PublishDiscovery`.

## Risks / Trade-offs

- **[Stale bounding boxes]** Open-Meteo could expand a model's coverage
  without us updating the registry. Mitigated by generous padding and the
  "unknown model = allow with warning" policy.

- **[Budget calculation complexity]** Per-location model counts make the
  budget estimate a sum-per-location instead of a simple multiply. Minor
  code change.
