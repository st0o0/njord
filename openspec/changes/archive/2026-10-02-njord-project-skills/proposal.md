## Why

Three tasks recur whenever njord grows and are done by copying from an existing file: adding a persistent actor with DTOs, adding an enrichment feature, and writing an actor spec. Today an agent has to rediscover the pattern each time (snapshot cadence, DTO versioning rules, the three enrichment interfaces plus DI registration, TestKit setup), and FunkArr shows project skills work well for exactly this. `restructure-agent-docs` creates `AGENTS.md` for rules; these skills hold the templates that do not belong in a rules file.

## What Changes

- Add `.claude/skills/njord-persistent-actor/SKILL.md`: persistent actor + persistence DTO template (`SnapshotInterval`, `PersistenceId`, `Recover<SnapshotOffer>` / `Recover<EventDto>`, `Persist`, `SaveSnapshot`, snapshot cleanup on `SaveSnapshotSuccess`, `[JsonProperty]` short names, `Version`, `UtcTicks`, static `XxxDtoMapping`, extend-only rules).
- Add `.claude/skills/njord-enrichment-feature/SKILL.md`: how to add an enrichment feature (interface choice, options + `Enabled` toggle, DI registration, discovery payload per feature device, state messages, contract spec).
- Add `.claude/skills/njord-actor-spec/SKILL.md`: actor spec template (Akka.Hosting TestKit, `AddTestPersistence()`, `FakeTimeProvider`, `[Fact(Timeout = 5000)]`, nested fake actors, `TestContext.Current.CancellationToken`, Verify snapshots).
- Add the three skills to the skill routing in `CLAUDE.md` (after `restructure-agent-docs` has landed).
- Each skill: frontmatter with `name` and a `description` containing explicit "Use when" triggers, a concise body, one worked example extracted from real files, and no restatement of rules that live in `AGENTS.md` (link to them instead).

## Capabilities

### New Capabilities

None. Tooling/docs only; no runtime behavior changes.

### Modified Capabilities

None. The change sets `skip_specs: true`.

## Impact

- Files: three new `.claude/skills/<name>/SKILL.md`; one edit to `CLAUDE.md` routing. No code under `src/` changes.
- API budget: 0 additional requests/month (no polling added or altered).
- Depends on: `restructure-agent-docs` (CLAUDE.md routing section and AGENTS.md rules must exist first). Related, independent: `akka-failure-hygiene` (the skills' examples must not reintroduce `Status.Failure`/`ContinueWith`).

## Non-goals

- Other deferred skill ideas: `njord-grpc-service`, `njord-e2e`, `njord-metric` (the metric pattern is covered by the AGENTS.md metrics section).
- Any change to production or test code, including refactoring the actors the examples are taken from.
- Introducing a state-record / `Apply` (Pathfinder) pattern: skills document the existing mutable-field + DTO-mapping approach.
- Skills for FunkArr-only concerns (sharding, endpoints, Servus registration).
