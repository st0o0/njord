## Context

njord uses Servus 0.35.0 and Servus.Akka 0.3.15 for structured startup (setup containers, AppBuilder/AppRunner, actor helpers). Terp 0.1.1 and Terp.Akka 0.1.1 replace these with a simpler API. The migration is fully described in `MIGRATION.md` at the repo root. No persisted data contains Servus type names, so no data migration is needed. The actor system name (`njord`), actor names, and all persistence records are unchanged.

## Goals / Non-Goals

**Goals:**
- Replace all Servus dependencies with Terp (0.1.1)
- Rename setup containers to Terp conventions (`XxxSetup : ISetup<T>`)
- Split `NjordHost.Create` into `Configure(builder)` / `Configure(app)`
- Replace `StreamSupervision.LoggingDecider` with `ResumeOnError` from Terp.Akka
- Update docs (AGENTS.md, CLAUDE.md) and OpenSpec specs

**Non-Goals:**
- Removing `IActorRegistration` (follow-up change)
- Replacing `HttpMetricsHandler` with Terp's version (metric name change)
- Updating the `akka-skills:setup-container` skill (separate repo)

## Decisions

### D1: Steps 1-4 as one atomic commit block

Steps 1 (packages), 2 (using renames), 3 (ActorSystemSetup), and 4 (containers + NjordHost + Program.cs + NjordTestHost) are interdependent -- the build is only green after step 4. They form one implementation task and one commit.

**Alternative**: Commit per step with intermediate broken builds. Rejected because a broken intermediate commit has no value and complicates bisect.

### D2: Keep Serilog inline in NjordHost.Configure

The existing `LoggingSetupContainer` spec says Serilog should be in a setup container, but the actual code has always kept it inline in `NjordHost.Create` because `DomainSetup` needs logging available before it runs. Terp's `ISetup<T>` has no logging-phase hook, so the pragmatic choice is to keep the 3 lines of Serilog configuration inline at the top of `Configure(builder)`, before the setup chain. The spec's `LoggingSetupContainer` requirement is removed.

### D3: Package versions pinned via CPM

`Directory.Packages.props` gets `Terp` 0.1.1 and `Terp.Akka` 0.1.1. CPM transitive pinning is on, so the `Terp` pin covers the transitive dependency from `Terp.Akka`. Projects that use only `Terp.Hosting` (without Akka) would reference `Terp` directly, but all njord library projects need `Terp.Akka`.

### D4: NjordHost.Configure returns the builder/app for chaining

`Configure(WebApplicationBuilder)` returns the builder so callers can chain. `Configure(WebApplication)` returns the app. This keeps `Program.cs` at 3 lines and lets integration tests insert overrides between the two calls.

### D5: Spec renames via git mv

`servus-bootstrap` -> `terp-bootstrap` and `domain-setup-containers` -> `domain-setups` are done with `git mv` on the spec directories in `openspec/specs/`. The archive step handles the delta specs.

## Risks / Trade-offs

- **[Transitive ASP.NET loss]** Terp has no `FrameworkReference Microsoft.AspNetCore.App`. All production projects already declare their own. If a test project fails to build, add the framework reference to that specific project. -> Mitigation: build after step 1, fix immediately.
- **[Backoff behavior change]** `Terp.Resilience.BackoffPolicy` fixes an overflow in `DelayWithJitter`. No PollManager test pins exact jittered delays, so this is safe. -> Mitigation: full test run catches regressions.
- **[Second ActorSystem guard]** `ActorSystemSetup` throws if a second `ActorSystem` is registered. `NjordTestHost` does not add its own `AddAkka`, so this is safe. -> Mitigation: integration tests validate.
- **[Stale skill reference]** `akka-skills:setup-container` still documents Servus. CLAUDE.md will reference a stale skill until the akka-skills repo is updated separately.
