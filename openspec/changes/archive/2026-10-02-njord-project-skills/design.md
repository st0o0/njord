## Context

See proposal.md for motivation. Facts verified against the source:

- Persistence DTOs are **not** a separate project: they live in `src/Njord/Persistence/*.cs` under namespace `Njord.Persistence` (`BudgetTrackerDtos.cs`, `EnrichmentSnapshotDtos.cs`, `ForecastHistoryDtos.cs`, `ForecastSnapshotDtos.cs`, `SchedulerDtos.cs`). The project docs say "`Njord.Persistence`"; the skill must say "namespace/folder", not "project".
- Two persistence shapes exist:
  - **Snapshot-only**: `Grpc/EnrichmentSnapshotActor.cs` (`SnapshotInterval = 14`, `PersistenceId => "enrichment-snapshot"`, `Recover<SnapshotOffer>` + counter, `SaveSnapshot(Mapping.ToDto(_state))` every N updates, `SaveSnapshotSuccess` → `DeleteSnapshots(seqNr - 1)`).
  - **Event + snapshot**: `Pipeline/BudgetTrackerActor.cs` (`SnapshotInterval = 50`, `Recover<ApiCallRecordedDto>` + `Recover<SnapshotOffer>`, `Persist`, on `SaveSnapshotSuccess` also `DeleteMessages` + `DeleteSnapshots`).
- DTO conventions seen in `BudgetTrackerDtos.cs` / `EnrichmentSnapshotDtos.cs`: sealed classes with settable props, Newtonsoft `[JsonProperty("short")]`, `Version` (`"v"`) default 1, `DateTimeOffset` stored as `long UtcTicks` (`"utc"`), static `XxxDtoMapping`/`XxxSnapshotMapping` with `ToDto`/`ToDomain`/`ToSnapshot`.
- Enrichment features: `Enrichment/IEnrichmentFeature.cs` (`TypeName`, `Enabled`, `DeviceId`, `BuildDiscoveryPayload`, `ToStateMessages`) with three sub-interfaces `IStatelessEnrichment` (`Compute(consensus, sensors)`), `IStatefulEnrichment` (`Compute(consensus, previous, sensors)`), `IActorEnrichment` (`CreateFlow(IUntypedActorContext)`). Implementations in `Enrichment/Features/{Alert,Derived,Trend,Index,History}Enrichment.cs` (internal sealed). Registration: `Configuration/NjordServiceSetup.cs` (`services.AddSingleton<IEnrichmentFeature, XxxEnrichment>()`). Toggle: `EnrichmentOptions.<Feature>.Enabled` (`Configuration/EnrichmentOptions.cs`). `EnrichmentActor` filters by `OfType<I…>().Where(f => f.Enabled)`. Device id: `TopicScheme.EnrichmentDeviceId(location, TypeName)`. Consensus is deliberately not a feature (`EnrichmentFeatureContractSpec`).
- Test infra: `Njord.Tests.Shared/TestPersistenceConfig.cs` (`AddTestPersistence()` = in-memory journal + snapshot store). Actor specs inherit `Akka.Hosting.TestKit.TestKit`, override `ConfigureAkka`/`ConfigureServices`, register `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`) as `TimeProvider`, and spawn with `Sys.ActorOf(Props.Create(...), $"name-{Guid.NewGuid():N}")` (`Njord.Tests/Pipeline/BudgetTrackerActorSpec.cs`). Feature specs are plain classes (`Njord.Tests/Enrichment/Features/*Spec.cs`) plus the cross-feature `EnrichmentFeatureContractSpec.cs`. Verify snapshots: `Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs` (`using static VerifyXunit.Verifier`).

## Goals / Non-Goals

**Goals:**
- Each skill lets an agent add the artifact correctly by following one worked example, with file paths that exist.
- Skills load only when triggered (descriptions carry "Use when" phrases) and stay short.

**Non-Goals:**
- Prescribing a new actor architecture; skills mirror what the code does today.
- Covering `akka-failure-hygiene` migration details beyond "use project-owned `XxxFailed` records".

## Decisions

### 1. Three skills, split by task, not by technology
`njord-persistent-actor`, `njord-enrichment-feature`, `njord-actor-spec`. *Alternative:* one `njord-akka` skill. Rejected: loads ~3× the text for any task and triggers poorly.

### 2. Document both persistence shapes, recommend by need
The skill shows snapshot-only (derived/replaceable state, like `EnrichmentSnapshotActor`) and event+snapshot (accumulating counters, like `BudgetTrackerActor`) in one decision table, with the snapshot-cleanup step differing (`DeleteSnapshots` only vs `DeleteMessages` + `DeleteSnapshots`). Worked example: `EnrichmentSnapshotActor` + `EnrichmentSnapshotDto`/`EnrichmentSnapshotMapping` (short, complete), with `BudgetTrackerActor` as the event variant.

### 3. DTO rules are referenced, not restated
Extend-only / `Version` / nullable-or-default rules live in `AGENTS.md` Conventions. The skill shows the shape (`[JsonProperty("v")] Version`, `UtcTicks`, mapping class) and links to the rule.

### 4. Enrichment skill is a checklist anchored on one real feature per interface
`AlertEnrichment` (stateless) is the worked example; `HistoryEnrichment` is cited for the actor variant (`ResolveChildActor`, supervision via `StreamSupervision.LoggingDecider`); `TrendEnrichment` for stateful. Checklist: pick interface → add `XxxOptions` with `Enabled` + wire into `EnrichmentOptions` → implement feature → register in `NjordServiceSetup.cs` → add `StatePayloadBuilder.FromXxx` → add spec in `Njord.Tests/Enrichment/Features/` → update `EnrichmentFeatureContractSpec` counts/type names. Reminder that the entity set is static from config (CLAUDE/AGENTS guardrail), so discovery components are built from config/enums, never from data.

### 5. Spec skill covers TestKit actor specs and feature specs, shows the shared helpers
Worked example: `BudgetTrackerActorSpec` (TestKit + `AddTestPersistence` + `FakeTimeProvider` + `Fact(Timeout = 5000)`). Mentions nested fake actors, `TestContext.Current.CancellationToken`, and Verify (`ModuleInitializer`, `Verifier.Verify`) for serialized payloads. Notes that `AlertEnrichmentSpec` defines a private `FakeTimeProvider` while `BudgetTrackerActorSpec` uses the framework one; the skill recommends the framework `FakeTimeProvider`.

### 6. Skill format
Frontmatter `name` (matches directory) and `description` with explicit "Use when …" phrases; body ≤ ~120 lines; one worked example; link to `AGENTS.md` sections instead of copying rules. Directory/filename `SKILL.md` uppercase (FunkArr's `e2e-verify/skill.md` casing issue not repeated).

### 7. Routing edit is a task, gated on `restructure-agent-docs`
Add one "Project skills (njord-specific)" block to `CLAUDE.md` routing with the three skills and their load-before triggers. If `restructure-agent-docs` has not been applied yet, apply it first (the routing section it creates is the edit target).

## Risks / Trade-offs

- [Examples drift as the actors change] → examples cite file paths rather than pasting large blocks; validation task re-reads the cited files.
- [Skill duplicates AGENTS.md] → rules are linked, templates only.
- [`akka-failure-hygiene` changes error-handling idioms] → skills show no failure-mapping code beyond `PipeTo(success:, failure:)`; revisit if that change alters actor shapes.
- [Persistence DTO location mislabeled] → skill states "folder `src/Njord/Persistence/`, namespace `Njord.Persistence`".

## Migration Plan

Additive files plus one routing edit; ships in one commit. Rollback: `git revert`.
