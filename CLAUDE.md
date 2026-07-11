# CLAUDE.md

## Project

njord — Kachelmann weather API → MQTT bridge for Home Assistant. A .NET service
(Docker container) on Akka.NET + Akka.Streams: polls the Kachelmannwetter API for
multiple weather models per location, computes a consensus forecast, and publishes
Home Assistant entities via MQTT Discovery (Mosquitto broker on the HA host).

### Architecture guardrails

- **Three zones that only meet in the domain model:** Ingest (Kachelmann client,
  DTOs, parsing) → Domain (`ModelForecast`, consensus) → Egress (HA entity
  definitions, topic builder, discovery payloads). Ingest and Egress never
  reference each other.
- **Streams for data flow, actors for lifecycle.** The poll pipeline is
  Akka.Streams (tick → fan-out → throttle → HTTP → aggregate → consensus → MQTT).
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

### Decisions (2026-07-11 — migrate into `openspec/specs/` as changes land)

- Multiple locations from v1; topics and devices carry a location level.
- All Kachelmann plans supported (Hobby/Smart-Home, Business Starter/Standard/
  Professional/Enterprise): plan is a config preset (budget defaults) with an
  optional raw-budget override. Development targets Hobby/Smart-Home
  (20k/month, 60/min). Startup validates projected usage against the budget.
- Poll interval and request budget are config values (default 60 min). Any change
  that adds or alters polling needs a budget estimate against the 20k/month limit.
- HA device cut: per location, one consensus device + one device per weather model.
- Forecast series: horizon sensors (e.g. +3 h/+6 h/+12 h/+24 h) plus the full
  series as a JSON attribute.

## Build & test

All commands run from `src/` (where `global.json` lives):

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj              # all tests
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "<FullyQualifiedName>"
```

Tests are xUnit v3 on Microsoft.Testing.Platform — `dotnet run`, **not** `dotnet test`.

## Kachelmann API (verified 2026-07-11)

- Base URL `https://api.kachelmannwetter.com/v02`, auth header `X-API-Key`,
  OpenAPI spec at `/v02/_doc.json`.
- Limits: 20,000 requests/month and 60/min.
- windSpeed/windGust are m/s. No feels-like temperature — compute Steadman
  ourselves. `model=ALL` response shape is undocumented. No warnings endpoint.
- Forecast endpoint: `/forecast/{lat}/{lon}/advanced/{timeSteps}` +
  `?model=<id>`; timeSteps coverage: 1h→24 h, 3h→120 h, 6h→240 h.
- `model` is a free-string query param; documented example ids include
  `ICON-D2`, `ECMWF`, `GFS`, `UKMO`, `HRRR`, `DWD-MOSMIX`, `MULTIMOD`,
  `SWISS1X1`. "Super HD" has no documented id — `SWISS1X1` (1 km) is the
  likely candidate; verify with a probe request before relying on it.
- API key comes from env var `Njord__ApiKey` — never in the repo, never in test
  fixtures.

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

- Kachelmann API: https://api.kachelmannwetter.com (OpenAPI: `/v02/_doc.json`)
- HA MQTT Discovery: https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery
