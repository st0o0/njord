## Context

njord is a headless .NET service with no user-facing interface. All configuration happens through `appsettings.json` and environment variables, requiring container restarts for any change. The service already has rich telemetry counters (`NjordTelemetry`) and health checks, but they are only accessible via OTLP export or `/healthz`. The Akka.Streams pipeline is architecturally model-agnostic (MergeHub/BroadcastHub), but the SchedulerActor and DiscoveryActor snapshot options at construction time, making the model/location set static.

The existing ASP.NET `WebApplication` host already serves Kestrel on `:8080` with health endpoints. Adding static file serving and minimal API routes is a natural extension of the existing host — no new process or port needed.

## Goals / Non-Goals

**Goals:**
- Runtime config editing with immediate effect via `IOptionsMonitor<T>` (no container restart for most settings)
- Hot add/remove of weather models and locations without pipeline graph teardown
- In-process stats dashboard with Chart.js graphs, backed by a ring-buffer metrics collector
- Single-container deployment: Vue SPA built in Docker, served as static files

**Non-Goals:**
- Authentication (private network assumption)
- WebSocket/SSE real-time push (polling is sufficient for v1)
- Forecast data visualization (requires new actor query path)
- Durable metrics history (ephemeral is fine for an admin panel)
- Hot-reload of MQTT connection settings or persistence provider

## Decisions

### D1: WritableMemoryConfigurationProvider as last-in-chain overlay

**Choice:** Subclass `MemoryConfigurationProvider`, add `Update()` that calls `Set()` + `OnReload()`, register as last provider via `builder.Configuration.Add()`.

**Alternatives considered:**
- `IConfigurationRoot.Reload()` — re-reads all providers (files, env vars), blunt and may overwrite pending in-memory changes from other sources.
- Akka.Persistence for config state — couples config to domain state, hard to inspect externally, overkill.
- `appsettings.override.json` with file watcher — works but adds file-system coupling for what is fundamentally a runtime operation. The writable provider is cleaner; file flush is an optional persistence detail.

**Rationale:** `OnReload()` is `protected` in `ConfigurationProvider`, so a subclass is needed. This is the minimal-surface solution: one class, one registration, full `IOptionsMonitor` integration. The file flush for restart persistence is a debounced background detail, not the primary path.

### D2: ConfigChangeCoordinator as IHostedService with debounce

**Choice:** A hosted service that listens on `IOptionsMonitor<NjordOptions>.OnChange`, debounces rapid changes (2s), computes diffs, validates, and dispatches actor messages.

**Alternatives considered:**
- Actors polling `IOptionsMonitor` on each message — spreads config-awareness across many actors, hard to coordinate.
- API controller directly sending actor messages — skips validation and diffing, couples HTTP layer to actor protocol.

**Rationale:** Centralizes the config→actor translation. Debounce is essential because the UI may update multiple keys in rapid succession (e.g., adding a location sets Name, Latitude, Longitude, Models as separate config keys, each triggering `OnChange`).

### D3: SchedulerActor AddTargets/RemoveTargets as persistent commands

**Choice:** New `TargetsAdded` / `TargetsRemoved` Akka.Persistence events alongside existing `DataChanged` events. The actor's `_states` dictionary becomes mutable at runtime.

**Alternatives considered:**
- Re-initializing `_states` from `IOptionsMonitor` on change — loses persisted poll rhythms (hash, cycle, phase) for existing targets. New targets start fresh in Discovery, but existing ones must retain their learned state.
- Separate "config actor" managing targets — adds actor coordination complexity for no clear benefit.

**Rationale:** Persistence ensures that dynamically added/removed targets survive restarts. The existing recovery loop (`OnRecover`) already rebuilds `_states` from events; adding two new event types is a natural extension.

### D4: DiscoveryActor diff-based refresh with tombstoning

**Choice:** `DiscoveryActor` maintains an in-memory `HashSet<string>` of published device IDs. On `RefreshDiscovery`, it computes the new device set from `IOptionsMonitor.CurrentValue`, diffs, publishes new entries, and sends empty retained messages for removed entries.

**Alternatives considered:**
- Republish everything on every refresh — wasteful MQTT traffic but simpler. However, it cannot remove old entries.
- HA-side cleanup via `expire_after` only — leaves stale entities until timeout, bad UX.

**Rationale:** Tombstoning (empty retained message on the discovery topic) is the HA-documented way to remove entities. The diff ensures minimal MQTT traffic.

### D5: StatsCollector using MeterListener with in-process ring buffer

**Choice:** `MeterListener` subscribes to the `"njord"` meter, captures instrument values every 30 seconds into a ring buffer (24h retention, ~2880 entries/metric).

**Alternatives considered:**
- OTLP self-scrape via Prometheus — requires running Prometheus in the container, massively increases image size and complexity.
- Custom counters alongside `NjordTelemetry` — duplicates instrumentation, divergence risk.
- EventCounters instead of `System.Diagnostics.Metrics` — legacy API, less capable.

**Rationale:** `MeterListener` is the .NET-native way to observe meters in-process. Zero external dependencies. The ring buffer is simple (circular array per metric) and bounded.

### D6: Vue 3 + Chart.js SPA built in Docker, served via UseStaticFiles

**Choice:** Separate Dockerfile stage (`node:22-alpine`) builds the Vue app, output copied to `wwwroot/`. ASP.NET serves via `UseStaticFiles()` + SPA fallback. No Node runtime in final image.

**Alternatives considered:**
- Blazor WebAssembly — stays in .NET ecosystem but WASM download is large (~5-10 MB), cold start is slow, ecosystem for charts is weaker.
- React — equally viable, but Vue's SFC model is more self-contained for a small admin panel.
- Server-side rendered (Razor Pages) — simpler for forms but poor fit for interactive charts and SPA-style navigation.

**Rationale:** Vue 3 + Vite builds fast, produces small bundles, Chart.js is ~60 KB gzipped. The chiseled base image stays minimal — just a few MB of static files added.

### D7: Minimal API over Controllers

**Choice:** ASP.NET minimal APIs (`app.MapGet`, `app.MapPut`) rather than MVC controllers.

**Rationale:** The admin API has ~10 endpoints, all simple request/response. Minimal APIs avoid the controller abstraction overhead and fit the existing `Program.cs` pattern. Endpoint groups can be organized via `MapGroup("/api/config")`.

### D8: EnrichmentActor locations via closure over IOptionsMonitor

**Choice:** Replace the captured `locations` list at graph materialization time with a lambda that reads `IOptionsMonitor<NjordOptions>.CurrentValue.Locations` on each `SelectMany` invocation.

**Alternatives considered:**
- Rematerializing the enrichment graph on location change — heavy, loses scan state, disrupts in-flight computations.
- Actor message to update a shared mutable list — thread-safety concerns in stream context.

**Rationale:** `IOptionsMonitor.CurrentValue` is thread-safe and always returns the latest snapshot. Reading it inside the stream operator is safe because Akka.Streams operators execute on a dispatcher (no concurrent invocations of the same operator). The locations list changes infrequently, so the overhead of re-reading on each element is negligible.

## Risks / Trade-offs

**[Config consistency during rapid updates]** → The debounce in `ConfigChangeCoordinator` (2s) means that intermediate states are invisible to actors. This is intentional — the UI should batch section updates. The mitigation is: the API accepts full-section updates (PUT with complete section payload), not individual key PATCH operations.

**[Stale actor state after validation failure]** → If the coordinator rejects a config change, the `WritableConfigProvider` still has the new values (IOptionsMonitor has fired, but no actor messages were sent). The UI must show the validation error and revert the values. Mitigation: the API endpoint validates *before* writing to the provider.

**[MeterListener histogram percentiles]** → `MeterListener` receives individual measurements, not pre-computed percentiles. The `StatsCollector` must maintain its own percentile computation (e.g., t-digest or simple sorted buffer per window). This adds implementation complexity. Mitigation: start with simple min/max/avg and add percentiles as a refinement.

**[Docker image size increase]** → Adding Vue/Chart.js static files adds ~2-5 MB to the image (gzipped). The Node build stage is only used during build and does not affect runtime image size. Acceptable trade-off.

**[No auth]** → Anyone with network access to `:8080` can change config. Acceptable for private HA networks (same trust model as HA itself). A future change can add basic auth or HA ingress integration.

**[Enrichment streams don't hot-toggle]** → If an enrichment feature is toggled via the UI (e.g., Trends enabled → disabled), the stream is already materialized and cannot be unmaterialized without restarting the EnrichmentActor. For v1, enrichment enable/disable takes effect on container restart. The feature reads the `Enabled` flag at materialization time, not per-element. This is a known limitation — document in the UI.

## Open Questions

1. **Vue project location** — `src/Njord/ui/` (inside the .NET project, close coupling) vs. `ui/` at repo root (clean separation, separate toolchain). Leaning toward `ui/` at repo root since it has its own `package.json` and build pipeline.

2. **Chart.js vs. lightweight alternative** — Chart.js is the default choice. If bundle size becomes a concern, uPlot (~35 KB) is a minimal alternative but has a steeper API.

3. **Config section granularity** — The API uses section-level PUT (`/api/config/locations`). Should individual field updates be supported (`PATCH /api/config/enrichment/alerts/frostThreshold`)? Section-level is simpler and avoids partial-update consistency issues.
