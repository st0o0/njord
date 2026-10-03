## Why

Stage 3a of the FunkArr-style split of the single `Njord` assembly (stage 3 was split in two on 2026-10-02, see design.md Decision 1). After `extract-core-projects` (stage 1: `Njord.Domain`, `Njord.Persistence`, `Njord.Messages`, `Njord.Core`) and `extract-leaf-feature-projects` (stage 2: `Njord.Grpc`, `Njord.Ingest`, `Njord.Sensors`), `Pipeline` and `Egress` can leave the host: neither depends on `Mqtt` or `Enrichment`, so they move without touching the Enrichment/Mqtt cycle. That cycle, and the possible removal of MQTT after the user review on 2026-10-04, is handled separately in `extract-enrichment-mqtt-projects` (stage 3b).

This stage is safe to apply before the MQTT review: `Pipeline` and `Egress` never reference `Mqtt`, so nothing here is wasted if MQTT is removed.

## What Changes

- New projects `src/Njord.Pipeline/` (12 files of `src/Njord/Pipeline`) and `src/Njord.Egress/` (6 files of `src/Njord/Egress`), each referencing only `Njord.Core` (and the Messages/Persistence/Domain chain below it), never each other and never the host. File counts are the host folders today; types stage 1 already moved to Core/Messages (`StreamSupervision`, `EgressEvent`, `EgressMessages`, `TopicSlug`, `HorizonProjection`) are not moved again.
- **Extraction order: Pipeline first, then Egress.** `ModelStateActor` (Egress) uses `PipelineActor`, `RequestPipelineSource`, `PipelineSourceResponse` and `StreamSupervision` (Pipeline), so Egress depends on Pipeline today. Earlier text claiming "leaf-most first, Egress first" was wrong.
- **Remove the lateral edge Egress -> Pipeline first** (red-first task, still one assembly): `ModelStateActor` reaches the pipeline only through the `IPipelineActor` marker (`src/Njord.Core/ActorKeys.cs`, stage 1) and the request/response messages and `StreamSupervision` that live in Messages/Core. An ArchUnit rule "no type in `Njord.Egress` depends on a type in `Njord.Pipeline`" is added red first and turns green with that change; otherwise the new assembly rule against lateral library references would fail after extraction.
- Cross-library actor access uses FunkArr-style `IXxxActor` markers, registered centrally in the host `NjordActorSystemSetup` (per-domain `RegisterPipelineActors` / `RegisterEgressActors`); actor classes become `public`; libraries expose only `AddNjordPipeline()` / `AddNjordEgress()` (services).
- **Registration-order guard:** `WithSqlPersistence(...)` must run before the actor registrations (journal configured before persistent actors start). A spec fails if the persistent actors (`scheduler`, `budget-tracker`) are registered first.
- ArchUnit assembly-reference rules: libraries reference only Core and below, never each other, never the host; the sealed convention covers all `Njord.*` production assemblies.
- `AGENTS.md` (structure and guardrails) and the `njord-*` skills: paths of what moved.
- Plan only (executed as tasks, no behavior change): Dockerfile restore/COPY lines for the two new csprojs; CI is unaffected.
- `Mqtt` and `Enrichment` stay in the host and simply reference the new libraries.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-zone-enforcement`: sealed convention covers all `Njord.*` production assemblies; new requirements for lateral-reference rules between feature libraries, marker-only actor access between libraries, and persistence configured before actor registration.

## Impact

- Prerequisites: `extract-core-projects` and `extract-leaf-feature-projects` applied (Core owns options, `StreamSupervision`, `NjordHealthState`, `NjordMetrics`, `StreamConsumerActor`, `TopicSlug`, `HorizonProjection`, `ActorKeys.cs`; Messages owns `EgressEvent`, `Request*/...Response` messages and Pipeline/Budget messages). `architecture-zone-enforcement` exists in `openspec/specs` (archived `zone-architecture-tests`).
- Code: `src/Njord/{Egress,Pipeline}`, `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs`, `src/Njord/Njord.csproj`, `src/Njord.Tests/{Architecture,Egress,Pipeline,Configuration}`, `src/Njord.slnx`, `Dockerfile`, `AGENTS.md`, `.claude/skills/njord-*`.
- API budget: 0 requests/month; no polling is added or altered (the free-tier limits of 300k/month, 10k/day are untouched).
- Wire/persistence formats unchanged: persistence IDs, DTO `[JsonProperty]` names, actor names (`scheduler`, `budget-tracker`, `egress`, `model-state`, `pipeline`) and metric names stay identical.

## Non-goals

- Any behavior or wire-format change, new features, new MQTT entities.
- Extracting `Njord.Enrichment` or `Njord.Mqtt`, presenters, golden masters (all in `extract-enrichment-mqtt-projects`).
- Creating Core/Domain/Messages/Persistence (stage 1) or Grpc/Ingest/Sensors (stage 2).
- Per-library test projects: tests stay in the single `Njord.Tests` project (`InternalsVisibleTo` plus a project reference per new library). FunkArr-style per-library test projects are a separate later change.
- Analyzer/`BannedSymbols.txt` rollout and CI workflow changes.
