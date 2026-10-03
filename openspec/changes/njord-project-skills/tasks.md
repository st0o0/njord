## 1. Preconditions

- [x] 1.1 Confirm `restructure-agent-docs` is applied (`AGENTS.md` exists and `CLAUDE.md` has the skill-routing section); if not, apply it first
- [x] 1.2 Re-read the cited source files so examples match current code: `src/Njord/Grpc/EnrichmentSnapshotActor.cs`, `src/Njord/Pipeline/BudgetTrackerActor.cs`, `src/Njord/Persistence/EnrichmentSnapshotDtos.cs`, `src/Njord/Persistence/BudgetTrackerDtos.cs`

## 2. njord-persistent-actor skill

- [x] 2.1 Create `.claude/skills/njord-persistent-actor/SKILL.md` with frontmatter `name: njord-persistent-actor` and a `description` containing "Use when creating or modifying a persistent actor or a persistence DTO"
- [x] 2.2 Body: decision table snapshot-only vs event+snapshot; constants (`SnapshotInterval`, `PersistenceId`); recovery handlers (`Recover<SnapshotOffer>`, `Recover<XxxDto>`); `Persist`/`SaveSnapshot`; `SaveSnapshotSuccess` cleanup (`DeleteSnapshots`, plus `DeleteMessages` for the event variant); failure/no-op handlers
- [x] 2.3 Worked example from `EnrichmentSnapshotActor` + `EnrichmentSnapshotDto`/`EnrichmentSnapshotMapping`; cite `BudgetTrackerActor` + `BudgetTrackerDtos.cs` for the event variant
- [x] 2.4 DTO shape section (`[JsonProperty]` short names, `"v"` Version, `UtcTicks`, static mapping class); say DTOs live in folder `src/Njord/Persistence/`, namespace `Njord.Persistence`; link to the extend-only rules in `AGENTS.md` instead of restating them

## 3. njord-enrichment-feature skill

- [x] 3.1 Re-read `src/Njord/Enrichment/IEnrichmentFeature.cs`, `IStatelessEnrichment.cs`, `IStatefulEnrichment.cs`, `IActorEnrichment.cs`, `Features/AlertEnrichment.cs`, `Features/TrendEnrichment.cs`, `Features/HistoryEnrichment.cs`, `src/Njord/Configuration/EnrichmentOptions.cs`, `NjordServiceSetup.cs`
- [x] 3.2 Create `.claude/skills/njord-enrichment-feature/SKILL.md` with `name` and a `description` containing "Use when adding or changing an enrichment feature (alerts, derived, trends, indices, history)"
- [x] 3.3 Body: interface choice (stateless / stateful / actor), the checklist from design.md Decision 4 (options + `Enabled`, feature class, `NjordServiceSetup.cs` registration, `StatePayloadBuilder.FromXxx`, spec, `EnrichmentFeatureContractSpec` update), static-entity-set reminder, device-per-feature naming via `TopicScheme.EnrichmentDeviceId`
- [x] 3.3a Verify `StatePayloadBuilder` location and `TopicScheme` API by reading the source before citing them
- [x] 3.4 Worked example from `AlertEnrichment` (stateless); cite `HistoryEnrichment` for the actor variant and `TrendEnrichment` for stateful

## 4. njord-actor-spec skill

- [x] 4.1 Re-read `src/Njord.Tests/Pipeline/BudgetTrackerActorSpec.cs`, `src/Njord.Tests.Shared/TestPersistenceConfig.cs`, `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs`, `src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs`, and the tests `ModuleInitializer.cs` for Verify
- [x] 4.2 Create `.claude/skills/njord-actor-spec/SKILL.md` with `name` and a `description` containing "Use when writing or changing tests for actors, enrichment features or persistence DTOs"
- [x] 4.3 Body: TestKit base class + `ConfigureAkka`/`ConfigureServices`; `AddTestPersistence()`; framework `FakeTimeProvider` registered as `TimeProvider`; unique actor names; `[Fact(Timeout = 5000)]`, `TestContext.Current.CancellationToken`, BDD-style method names, `Spec` suffix, sealed classes; nested fake actors; Verify snapshot pattern; run command `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "<FQN>"` (not `dotnet test`)
- [x] 4.4 Worked example from `BudgetTrackerActorSpec`; note the private `FakeTimeProvider` in `AlertEnrichmentSpec` is not the pattern to copy; link to the test assertion rules in `AGENTS.md`

## 5. Routing

- [x] 5.1 Add a "Project skills (njord-specific)" block to the skill routing in `CLAUDE.md` listing the three skills with "load before …" triggers
- [x] 5.2 Confirm `CLAUDE.md` still imports `AGENTS.md` and no `sepp:*` routes reappeared

## 6. Validation

- [x] 6.1 Each `SKILL.md` has frontmatter `name` equal to its directory name and a `description` containing "Use when"
- [x] 6.2 Every file path cited in the three skills exists (`ls`/`grep` each path); each worked example matches the current source by reading
- [x] 6.3 No rule text is copied from `AGENTS.md`; no `Status.Failure` / `ContinueWith` / `EventStream` appears in examples
- [x] 6.4 `openspec validate njord-project-skills` passes
- [x] 6.5 `git diff --stat` shows only the three new skill directories, `CLAUDE.md` and this change's files; nothing under `src/`
- [ ] 6.6 Sanity from `src/`: `dotnet build Njord.slnx` (no code changed; confirms the tree still builds) — skipped by the apply agent (concurrent build in same checkout); parent runs it
- [x] 6.7 Commit with a Conventional Commit message (`docs: add njord project skills`); do not push
