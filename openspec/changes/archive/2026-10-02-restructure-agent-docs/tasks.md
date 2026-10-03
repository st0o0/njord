## 1. Create AGENTS.md (move, verbatim)

- [x] 1.1 Create `AGENTS.md` with `# AGENTS.md` and move these `CLAUDE.md` sections verbatim: Project, Architecture guardrails, Decisions, Build & test (incl. config layers, MQTT-disabled default, `dotnet slopwatch` line), Open-Meteo API, Conventions, References
- [x] 1.2 Verify no text of the moved sections changed (diff the section bodies of old `CLAUDE.md` against `AGENTS.md`)

## 2. Add adopted sections to AGENTS.md

- [x] 2.1 Add "Language": English only for code, specs, docs and communication
- [x] 2.2 Add "Solution structure" tree: `src/Njord.slnx`, `Njord/` (Ingest, Domain/{Weather,Sensors,Analysis}, Egress, Mqtt, Pipeline, Enrichment/Features, Grpc, Sensors, Persistence, Configuration, Diagnostics, Health, Actors), `Njord.Tests/`, `Njord.Tests.Shared/`; re-check folder names against `src/Njord/` before writing
- [x] 2.3 Add "C# conventions": style enforced by `src/.editorconfig`; no `async void`, `.Result`, `.Wait()`; pass `CancellationToken`; no XML docs
- [x] 2.4 Add "Akka conventions": no `ContinueWith` (use `PipeTo` with success/failure mappers), no `Status.Failure` (project-owned `XxxFailed(Exception Cause)` records), no `EventStream` in production code (TestKit DeadLetter/Warning probes allowed), no `IActorRef` constructor parameters, `XxxActor` naming; include the "Known deviations (tracked in `akka-failure-hygiene`)" list from design.md Decision 3
- [x] 2.5 Add "Test assertion conventions": no `!.` (use `Assert.NotNull` first), `Assert.Single`, count guard before indexing; first grep `src/Njord.Tests` for existing `!.` violations and list them as known deviations if any exist
- [x] 2.6 Add "Metrics conventions" derived from `src/Njord/Diagnostics/NjordMetrics.cs` and `*MetricsExtensions.cs`: `AddX` extension on `NjordMetrics`, `private static readonly` instrument in the actor, `njord_<area>_<name>_total` snake_case, bounded tag cardinality

## 3. Slim CLAUDE.md

- [x] 3.1 Rewrite `CLAUDE.md` to: `@AGENTS.md`, Workflow section (OpenSpec `/opsx:*`), Skill routing
- [x] 3.2 Remove the five `sepp:*` routes (`actor-pattern-library`, `resilience-patterns`, `message-driven-designer`, `domain-modeling-patterns`, `complexity-guardian`); keep `dotnet-skills:*` entries and the two specialist agents; keep `dotnet-skills:slopwatch` under Quality gates
- [x] 3.3 Confirm every remaining `dotnet-skills:*` skill/agent name in routing resolves (against the installed `dotnet-skills` plugin)
- [x] 3.4 Keep the `akka-skills:*` block (user decision: plugin is expected to be installed elsewhere/later); add a one-line note above it that it requires the `akka-skills` plugin and is not installed on every machine; it is deliberately excluded from 3.3

## 4. Slim openspec/config.yaml

- [x] 4.1 Replace `context:` in `openspec/config.yaml` with a short description, a pointer to `AGENTS.md`, and the two hard constraints (never `git push`; `dotnet run`, not `dotnet test`)
- [x] 4.2 Check `src/Njord/Enrichment/Features/` for an "energy" feature; leave it out of the description unless it exists
- [x] 4.3 Leave the `rules:` block unchanged

## 5. Validation

- [x] 5.1 `openspec validate restructure-agent-docs` passes
- [x] 5.2 `git diff --stat` shows only `AGENTS.md`, `CLAUDE.md`, `openspec/config.yaml` (and this change's files); nothing under `src/`
- [x] 5.3 `grep -rn "sepp:" CLAUDE.md AGENTS.md openspec/config.yaml` returns nothing
- [x] 5.4 Sanity run from `src/`: `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj` (no suites are touched; confirms nothing changed)
- [x] 5.5 Commit with a Conventional Commit message (`docs: split CLAUDE.md into AGENTS.md`); do not push

## 6. Follow-up (not part of this change)

- [x] 6.1 Create change `akka-failure-hygiene` per design.md Decision 4 (order: `ContinueWith` + `Sink.ActorRef` messages, then request/response failure records with requester handling, then drop the "Known deviations" note) — done: change created and applied (commits c162a8a, e81792a, fee13a6)
