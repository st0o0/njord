# CLAUDE.md

## Project

njord — Multi-model weather intelligence for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: polls the Open-Meteo API for
multiple weather models per location, processes forecasts through an enrichment
pipeline (consensus, alerts, derived values, trends, indices, energy, history),
and publishes Home Assistant entities via MQTT Discovery (Mosquitto broker on
the HA host).

### Architecture guardrails

- **Three zones that only meet in the domain model:** Ingest (Open-Meteo client,
  DTOs, parsing) → Domain (`ModelForecast`, enrichment, consensus) → Egress (HA
  entity definitions, topic builder, discovery payloads). Ingest and Egress never
  reference each other.
- **Streams for data flow, actors for lifecycle.** The poll pipeline is
  Akka.Streams (tick → fan-out → throttle → HTTP → aggregate → enrich → MQTT).
  Actors own connection management and the HA birth-message subscription.
- **Discovery and telemetry are separate flows.** Discovery (retained config
  payloads) is lifecycle-driven: startup, HA birth on `homeassistant/status`,
  config change. Telemetry (state topics) is tick-driven.
- **The entity set is static, derived from config** (locations × models ×
  parameters × horizons) — never from what the API happens to return. A missing
  value is an `unavailable` state, not a missing entity.
- **Never `Zip` model sub-streams for consensus.** Aggregate per poll cycle
  (cycle id = tick timestamp) with timeout and quorum — consensus from whatever
  arrived, plus `models_used`/spread diagnostics.
- **`TimeProvider` everywhere**, never `DateTime.Now`/`UtcNow` directly.

### Decisions

- Open-Meteo free tier (non-commercial) is the assumed plan: the request budget
  defaults to its soft limits (300k/month, 600/min) with an optional
  `BudgetOverride` for self-throttling. Startup validates projected usage
  against 80 % of the resolved monthly budget. Commercial use would need the
  paid tier (out of scope).
- Poll interval and request budget are config values (default 60 min). Any change
  that adds or alters polling needs a budget estimate against the free-tier soft
  limits (300k/month, 10k/day) — and politeness toward a free service.
- Feels-like comes from the API (`apparent_temperature` per model) — no own
  Steadman computation.
- HA device cut: per location, one device per weather model plus one device per
  enabled enrichment feature. Enrichment features (consensus, alerts, derived,
  trends, indices, energy, history) are independently toggleable.
- Entity grid per model device: one sensor per (parameter, horizon) — horizons
  configurable, default +3/+6/+12/+24/+48/+72 h. No JSON series attribute, no
  `state_class` (forecasts are not measurements). Recommend a `recorder:` exclude
  for `sensor.njord_*` in HA docs/snippets.
- MQTT egress: device-based discovery (one retained config per device,
  `homeassistant/device/<id>/config`), one retained state JSON per device per
  cycle, availability via LWT on `njord/status` + per-component
  `availability_template` + `expire_after` (2× poll interval). MQTTnet sits
  behind the `IMqttPublisher` seam; the connection actor owns lifecycle, HA
  birth handling, and tombstoning of stale retained configs.

## Build & test

All commands run from `src/` (where `global.json` lives):

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj                                    # unit + actor tests (no Docker)
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "<FullyQualifiedName>"   # single class
```

Tests are xUnit v3 on Microsoft.Testing.Platform — `dotnet run`, **not** `dotnet test`.
Shared test infrastructure (fixtures, fakes, helpers) lives in `Njord.Tests.Shared`.

Run the service itself from `src/Njord/` (`dotnet run`). Configuration layers:
- `appsettings.json` — production logging only (no `Njord:` section).
- `appsettings.Development.json` — dev overrides (locations, models, MQTT
  disabled). Loaded automatically by `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`).
- `data/njord-config.json` — optional runtime override (Docker volume mount).
- Environment variables — Docker-compose / `docker run` (`Njord__Mqtt__Host`, etc.).

MQTT is disabled by default (`Mqtt:Enabled = false`). Enable explicitly with
`Njord__Mqtt__Enabled=true` + `Njord__Mqtt__Host=...`.

`dotnet slopwatch` runs from the repo root (baseline in `.slopwatch/`).

## Open-Meteo API (verified 2026-07-11 via live probes)

- Endpoint `GET https://api.open-meteo.com/v1/forecast?latitude=&longitude=` —
  **no API key, no auth header**. Free tier is non-commercial; soft limits
  600/min, 5k/h, 10k/day, 300k/month. Calls are weighted: >10 hourly variables
  or >2 weeks of data count fractionally as multiple calls (njord requests
  exactly 9 variables × 4 days = weight 1.0).
- Single-model requests (`&models=icon_d2`) return flat arrays under `hourly`
  with **unsuffixed** variable names + an `hourly_units` object; multi-model
  requests suffix every variable with the model id.
- Wind defaults to km/h — always send `wind_speed_unit=ms`. Times default to
  naive ISO strings — always send `timeformat=unixtime` (epoch seconds).
- Values beyond a model's horizon are `null` entries (e.g. `icon_d2` ends
  ~+48–64 h). A single requested model outside its geographic coverage →
  HTTP 400 `{"error":true,"reason":"No data is available for this location"}`;
  invalid model ids → HTTP 400 with an "invalid String value" reason.
- Model ids verified working: `icon_d2`, `icon_eu`, `icon_global`,
  `ecmwf_ifs025`, `gfs_seamless`, `ukmo_global_deterministic_10km`,
  `meteoswiss_icon_ch1`, `meteoswiss_icon_ch2` (~1 km, Alps coverage — in
  range for the default Lucerne location). No warnings endpoint.

## Conventions

- **Git: NEVER `git push`** — the user pushes. Commit messages are Conventional
  Commits (commitlint-enforced).
- Versioning is release-please. Never edit `<Version>` in
  `src/Directory.Build.props` by hand.
- Central package management with transitive pinning: versions live only in
  `src/Directory.Packages.props`; add packages via `dotnet add package`, never
  edit csproj XML for versions.
- Tests: `Spec` suffix, `sealed` classes, `[Fact(Timeout = 5000)]`, BDD-style
  method names.
- C#: records for messages/DTOs, value objects in the domain, `sealed` by
  default, nullable enabled.

## Workflow

Changes go through OpenSpec: `/opsx:explore` to think → `/opsx:propose` to create
a change (proposal/design/specs/tasks) → `/opsx:apply` to implement → `/opsx:archive`.
Implementation is TDD; run `dotnet slopwatch` (local tool) after substantial code
changes.

## Skill routing (invoke by name)

Prefer retrieval-led reasoning: consult these before implementing.

- Actors & supervision: `dotnet-skills:akka-best-practices`,
  `dotnet-skills:akka-hosting-actor-patterns`, `sepp:actor-pattern-library`,
  `sepp:resilience-patterns`
- Messages & pipeline design: `sepp:message-driven-designer`,
  `dotnet-skills:csharp-concurrency-patterns`
- Domain modeling: `sepp:domain-modeling-patterns`,
  `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`
- Config & DI: `dotnet-skills:microsoft-extensions-configuration`,
  `dotnet-skills:microsoft-extensions-dependency-injection`
- Serialization (discovery/state payloads): `dotnet-skills:serialization`
- Testing: `dotnet-skills:akka-testing-patterns`,
  `dotnet-skills:testcontainers` (Mosquitto integration tests),
  `dotnet-skills:snapshot-testing` (discovery payloads via Verify)
- Packages & structure: `dotnet-skills:package-management`,
  `dotnet-skills:project-structure`
- Quality gates: `dotnet-skills:slopwatch` (after code changes),
  `sepp:complexity-guardian` (before review)
- Specialist agents: `dotnet-skills:akka-net-specialist`,
  `dotnet-skills:dotnet-concurrency-specialist`

## References

- Open-Meteo API: https://open-meteo.com/en/docs (endpoint: `https://api.open-meteo.com/v1/forecast`)
- HA MQTT Discovery: https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery
