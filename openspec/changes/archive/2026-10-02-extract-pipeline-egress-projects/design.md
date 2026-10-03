## Context

See proposal.md for motivation and `extract-core-projects` / `extract-leaf-feature-projects` for the layout (`Domain <- Messages <- Core <- feature libs <- host`; feature libs reference only Core and below, never each other). The follow-up `extract-enrichment-mqtt-projects` (stage 3b) handles Enrichment and Mqtt.

Facts from the current code (read, not built):

- Verified 2026-10-02 (after stages 1/2): `src/Njord/Pipeline/` holds 11 files: `BudgetThrottleStage`, `BudgetTrackerActor`, `BudgetTrackerDtoMapping` (namespace `Njord.Persistence`), `BudgetTrackerState`, `IBudgetGate` (with `WeightedBudgetGate`), `IBudgetProvider` (with `OptionsBudgetProvider`), `ModelPollState`, `PipelineActor`, `SchedulerActor`, `SchedulerDtoMapping` (namespace `Njord.Persistence`), `SchedulerState`. `src/Njord/Egress/` holds 4: `EgressActor`, `HorizonProjection`, `ModelStateActor`, `TopicSlug`. Stage 1 moved `StreamSupervision` (`Njord.Core/Actors`), `EgressEvent`, `EgressMessages`, `SchedulerMessages`, `PollPhase`, `WeightedTarget` (`Njord.Messages`). `IOpenMeteoClient` lives in Core (namespace `Njord.Ingest`), so Pipeline needs no reference to `Njord.Ingest`. `TopicSlug`/`HorizonProjection` stay in Egress (consumers are host Mqtt/Enrichment).
- `ModelStateActor` (Egress) calls `Context.GetActorAsync<PipelineActor>()`, sends `RequestPipelineSource`, receives `PipelineSourceResponse` and uses `StreamSupervision.LoggingDecider`. Verified: the lookup uses `IPipelineActor` and the messages/`StreamSupervision` live in Messages/Core; the only residue is an unused `using Njord.Pipeline;`. The ArchUnit rule is therefore green from the start and is proven red once with a temporary Pipeline type reference; stage 3a guards the edge rather than assuming.
- Neither `Pipeline` nor `Egress` references `Mqtt` or `Enrichment` types.
- `NjordActorSystemSetup` registers actors centrally after `.WithSqlPersistence(...)` (stage 1 groups them per domain under marker keys).

## Goals / Non-Goals

**Goals:**
- `Njord.Pipeline` and `Njord.Egress` compile as separate assemblies referencing only Core and below.
- Zero change in behavior, actor names, persistence IDs, metrics.
- Every step leaves `dotnet build` and the full test suite green.

**Non-Goals:**
- Anything about Enrichment/Mqtt (stage 3b); new abstractions; behavior or config changes.

## Decisions

### 1. Stage 3 is split: Pipeline/Egress now, Enrichment/Mqtt later

A critical review of the original stage 3 found: Pipeline and Egress do not depend on Mqtt, while Mqtt may be removed entirely after the user review on 2026-10-04. Coupling the safe, mechanical moves to the cycle-breaking presenter work (which only pays off if MQTT stays) would block them. Therefore 3a (this change) is independent of the MQTT decision; 3b waits for the review. Mqtt and Enrichment stay in the host between the two stages and reference the new libraries.

### 2. Extraction order: Pipeline first, then Egress

Egress depends on Pipeline through `ModelStateActor` (see Context). With the lateral edge removed (Decision 3) the two are independent, but moving Pipeline first keeps every intermediate state compilable even if a residual reference is found: Egress -> Pipeline would then be an ordinary project reference in a transitional commit, which the ArchUnit rule (red) flags before Egress is extracted. The earlier claim "leaf-most first, Egress first" is wrong and removed.

### 3. Remove the lateral edge Egress -> Pipeline before extraction (red first)

The compiler stops cycles and upward references but not lateral ones. If `ModelStateActor` still touched `PipelineActor`, `RequestPipelineSource`, `PipelineSourceResponse` or `StreamSupervision` as Pipeline types, extracting both would force `Njord.Egress -> Njord.Pipeline` and violate the new rule "Feature libraries do not reference each other". Resolution: actor lookup via the `IPipelineActor` marker (Core), messages in `Njord.Messages`, `StreamSupervision` in `Njord.Core`. If stage 1 left any of these in the Pipeline folder, a task moves it down first. Task order: add the ArchUnit rule (red), fix, green, then extract.

### 4. Actor registration stays central; persistence runs first

As in stage 1 Decision 6: registration stays in the host `NjordActorSystemSetup` per domain (`RegisterPipelineActors`: `ISchedulerActor`, `IBudgetTrackerActor` via `RegisterWithBackoff<TKey, TActor>`, `IPipelineActor`; `RegisterEgressActors`: `IEgressActor`, `IModelStateActor`); only visibility changes (`public`) and project references are added. Persistent actors (`scheduler`, `budget-tracker`) need the journal, so `WithSqlPersistence` must precede the registrations. This order is a silent-failure risk after moving code between files, so a spec (in `src/Njord.Tests/Configuration/`) boots the production setup and asserts the persistence configuration is applied before the actor registrations (e.g. by recording the order of the configuration callbacks or by resolving a persistent actor with the real setup and in-memory persistence and observing recovery). Added red-first before the Pipeline extraction.

### 5. ArchUnit: assembly-based rules for the libraries extracted so far

All `Njord.*` production assemblies are loaded in one architecture load. Rules: Pipeline, Egress, Grpc, Ingest, Sensors depend only on Core, Messages, Persistence, Domain; none on each other or the host; Core/Messages/Persistence/Domain do not reference feature libraries or the host. The sealed convention iterates all assemblies. The old namespace-based zone rules stay until stage 3b removes the last in-host features, so every intermediate state is covered. Stage 3b extends the same matrix with Enrichment and Mqtt (its own ADDED requirement, so the two changes do not collide at archive).

### 6. Per-library recipe

New csproj (plain SDK; `FrameworkReference Microsoft.AspNetCore.App` only if needed), move files, `AddNjordX()` service extension if the library has registrations, actor classes `public`, `InternalsVisibleTo Njord.Tests`, project reference from `Njord.Tests` and the host, slnx entry, ArchUnit set, build + tests. One library per commit.

### 7. Tests stay in one project

`Njord.Tests` keeps all specs. Per-library test projects (FunkArr style) are a follow-up change, not part of 3a or 3b.

## Risks / Trade-offs

- [Residual Egress -> Pipeline type reference stage 1 missed] -> red ArchUnit rule before extraction (Decision 3); task 0.3 lists unmet items.
- [Registration moved before `WithSqlPersistence` -> persistent actors fail at runtime only] -> order-guard spec (Decision 4) plus the stage-1 `ActorKeyRegistrationSpec`.
- [Actor lookup by marker misregistered] -> `ActorKeyRegistrationSpec` resolves every marker; startup smoke in validation.
- [Large diff] -> one library per commit, each green.
- [`architecture-zone-enforcement` delta is MODIFIED/ADDED against a spec that exists only after `zone-architecture-tests` is archived] -> stated prerequisite, checked in task 0.2.

## Migration Plan

Refactor only, no runtime behavior change; rollback per commit via `git revert`. Dockerfile (task 7.3): add `COPY` + `restore` lines for `Njord.Pipeline` and `Njord.Egress` csprojs before the full source copy to keep layer caching; CI is solution-based and needs no change.

## Open Questions

- None for this stage. (The consensus-discovery question and the Mqtt-removal alternative live in `extract-enrichment-mqtt-projects`.)
