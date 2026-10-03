## Context

See proposal.md for motivation. Current state:

- `CLAUDE.md` (~9 KB) is the only agent doc. No `AGENTS.md`, no project skills.
- `openspec/config.yaml` `context:` repeats the project blurb, build/test commands, API summary and git rule.
- FunkArr's layout (reference): `CLAUDE.md` = `@AGENTS.md` + workflow + skill routing; `AGENTS.md` = tool-agnostic rules.
- A code survey of `src/` found deviations from the FunkArr Akka conventions that njord would otherwise adopt (see Decision 3).
- `src/.editorconfig` already enforces: file-scoped namespaces, `using` placement/order, `_camelCase` private fields, Allman braces, `var`.

## Goals / Non-Goals

**Goals:**
- One source of truth per fact; `AGENTS.md` for rules, `CLAUDE.md` for Claude wiring, `openspec/config.yaml` for OpenSpec rules.
- Every rule written in `AGENTS.md` is either true of the code today or explicitly listed as a known deviation.
- No reference to a skill that cannot be resolved.

**Non-Goals:**
- Rewriting the content of decisions / API facts / guardrails (moved verbatim).
- Any code change.

## Decisions

### 1. Split mirrors FunkArr: `CLAUDE.md` = `@AGENTS.md` + Workflow + Skill routing

Sections moved to `AGENTS.md`: Project, Architecture guardrails, Decisions, Build & test, Open-Meteo API, Conventions, References. Stays in `CLAUDE.md`: the import, the `/opsx:*` workflow, skill routing. The `slopwatch` line is a CLI step, so it stays in `AGENTS.md` under Build & test.

*Alternative:* keep one file. Rejected: other agents/tools read `AGENTS.md`, and Claude-only slash commands are noise for them.

### 2. Do not duplicate `.editorconfig`; copy only what it cannot enforce

`AGENTS.md` C# section says "style is enforced by `src/.editorconfig`" and lists only `async void`/`.Result`/`.Wait()`, `CancellationToken` passthrough, `TimeProvider` (already present) and no XML docs. *Alternative:* copy FunkArr's full style list. Rejected: two sources of truth that drift.

### 3. Akka conventions: state as target rules plus a "Known deviations" list

Survey result (`src/`, verified by grep):

| Rule | Production deviations | Test deviations |
|---|---|---|
| No `Status.Failure` | `Egress/EgressActor.cs:33,48`, `Mqtt/MqttConnectionActor.cs:105`, `Pipeline/PipelineActor.cs:72,84`, `Pipeline/SchedulerActor.cs:249` | `Njord.Tests/Pipeline/PipelineConnectionSpec.cs:318` |
| No `ContinueWith` | `Mqtt/MqttConnectionActor.cs:156` | `Njord.Tests/Egress/EgressActorSpec.cs:49` |
| No `EventStream` | none | `StreamConsumerActorSpec.cs:199`, `SchedulerActorSpec.cs:180` (DeadLetter/Warning probes) |

`AGENTS.md` states the rules, scoped to production code, with a short "Known deviations (tracked in `akka-failure-hygiene`)" note listing the files. The EventStream rule is true for production already; test use of `EventStream.Subscribe` for DeadLetter/Warning probes is explicitly allowed (it is the standard TestKit idiom).

*Alternative:* omit the rules until migrated. Rejected: agents would keep adding new deviations.
*Alternative:* migrate in this change. Rejected: it changes message protocols (below) and deserves its own review and test run; this change must stay docs-only.

### 4. Migration assessment (result of the requested investigation)

**Feasible, moderate effort, one coherent follow-up change `akka-failure-hygiene`.** Findings:

- **`ContinueWith` (1 production site):** `MqttConnectionActor.Connect()` already defines `ConnectFailed`. Replace with `ConnectAsync(...).PipeTo(self, success: () => new Connected(), failure: ex => new ConnectFailed(ex))`. Mechanical, behavior-preserving. The test site (`EgressActorSpec.cs:49`) is replaced with `await`/`TaskCompletionSource` completion.
- **`Status.Failure` in `PipeTo(Sender, ...)` (5 sites: Egress ×2, MqttConnection, Pipeline ×2):** these are not internal self-messages; they are the *reply* to the requester of a `Request*Sink`/`Request*Source` message. Today failure surfaces to `Ask` callers as a faulted task (`WeatherGrpcService.cs:149,178`) and is silently unhandled for the `Tell`-based requesters (`ModelStateActor`, `DiscoveryActor`, `MqttEgressActor`, `EnrichmentActor`, `GrpcSnapshotConsumerActor`, `SchedulerActor`). Migration is therefore a **protocol change**: each request gets a project-owned failure response (e.g. `EgressSinkFailed(Exception Cause)`), and each requester must handle it (log + existing re-request/backoff path, or fault the gRPC call). Not mechanical, but bounded: 5 producers, ~8 requesters, specs for each.
- **`Status.Failure`/`Status.Success` as `Sink.ActorRef` termination messages (`SchedulerActor.cs:248-249`, test `PipelineConnectionSpec.cs:318`):** replace with project-owned `FailureConsumerCompleted` / `FailureConsumerFailed(Exception)` records and add `Receive` handlers (currently unhandled). Mechanical.
- **Recommended order:** (a) `ContinueWith` + `Sink.ActorRef` messages (mechanical, no protocol change), (b) request/response failure records with requester handling, (c) flip the "Known deviations" note in `AGENTS.md` to a plain rule.
- **Back-compat:** all messages are in-process (no wire/persistence use), so no versioning concern; persistence DTO extend-only rules are untouched.

### 5. `openspec/config.yaml` keeps `rules:`, slims `context:`

`context:` becomes: one-sentence project description, a pointer to `AGENTS.md` for stack, build/test commands, structure and API facts, and the two hard constraints that matter when writing artifacts (never `git push`; `dotnet run`, not `dotnet test`). *Alternative:* remove `context:` entirely. Rejected: it is injected verbatim into artifact generation, and OpenSpec is not known to read `AGENTS.md`. The "energy" entry is removed; verify against `src/Njord/Enrichment/Features/` first (the code survey found no such feature folder) and keep the entry out if absent.

### 6. Skill routing: delete dead entries, keep the rest

Delete the five `sepp:*` routes and the sentence-level references to them. `dotnet-skills:*` entries and the two specialist agents remain (verified to resolve). The "Quality gates" line keeps `dotnet-skills:slopwatch` only.

## Risks / Trade-offs

- [Rules in `AGENTS.md` that the code violates] → Known-deviations note plus follow-up change; the note is deleted when the migration lands.
- [Content lost or altered during the move] → Move verbatim; verify by diffing section text; no paraphrasing of decisions/API facts.
- [`@AGENTS.md` import not resolved by a tool] → `AGENTS.md` is the primary file, so such tools read it directly; Claude Code resolves the import.
- [`config.yaml` `context:` drifts from `AGENTS.md` again] → keep it minimal so there is little to drift.
- [Metrics section describes a moving target] → derive only the stable pattern (`NjordMetrics.Instance.AddX()` extension + static field in the actor, `njord_<area>_<name>_total` snake_case) from `Diagnostics/*MetricsExtensions.cs`.

## Migration Plan

Docs-only; ships in one commit. Rollback: `git revert`. Follow-up change `akka-failure-hygiene` is created separately after this one is applied.
