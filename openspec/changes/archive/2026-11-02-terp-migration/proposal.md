## Why

Servus (`Servus` 0.35.0, `Servus.Akka` 0.3.15) has been superseded by Terp (`Terp` 0.1.1, `Terp.Akka` 0.1.1). Terp drops the ASP.NET framework dependency, simplifies the setup API (`ISetup<T>` replaces `IServiceSetupContainer` / `ApplicationSetupContainer<T>` / `AppBuilder`), and adds stream supervision helpers (`ResumeOnError`). The migration is mechanical: no persisted data contains Servus type names, so no data migration is needed.

## What Changes

- **BREAKING**: Replace `Servus` and `Servus.Akka` NuGet packages with `Terp` 0.1.1 and `Terp.Akka` 0.1.1.
- Rename all setup containers (`XxxSetupContainer` to `XxxSetup`), changing their base type from `IServiceSetupContainer` to `ISetup<IServiceCollection>`.
- Convert `AkkaSetupContainer : ActorSystemSetupContainer` to `AkkaSetup : ActorSystemSetup` (Terp.Akka).
- Convert `NjordApplicationSetup : ApplicationSetupContainer<WebApplication>` to `NjordApplicationSetup : ISetup<WebApplication>`.
- Split `NjordHost.Create(builder, build)` (returns `AppRunner`) into `NjordHost.Configure(builder)` + `NjordHost.Configure(app)` with explicit `builder.Setup(...)` calls.
- Replace `using Servus.*` with `using Terp.*` across the codebase.
- Replace `ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(...))` with `.ResumeOnError(log)` (Terp.Akka extension). Delete `StreamSupervision.cs`.
- Update AGENTS.md, CLAUDE.md, and OpenSpec specs to reflect the new names.

## Non-goals

- Removing `IActorRegistration` (optional step 5 in MIGRATION.md) -- can be done as a follow-up change.
- Replacing `HttpMetricsHandler` with `TerpHttpMetricsHandler` (optional step 6) -- metric name change, do deliberately.
- Replacing `ObservableGauge` with BCL `Gauge<long>` -- BCL feature, not Terp-specific.
- Updating the `akka-skills:setup-container` skill (lives in `D:\GIT\akka-skills`, separate repo).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `servus-bootstrap`: Rename to `terp-bootstrap`. Replace Servus setup containers with Terp `ISetup<T>` classes, `AppBuilder`/`AppRunner` with direct `WebApplicationBuilder`/`WebApplication`, `ActorSystemSetupContainer` with `ActorSystemSetup`.
- `domain-setup-containers`: Rename to `domain-setups`. All `XxxSetupContainer` become `XxxSetup : ISetup<IServiceCollection>`, `SetupServices` becomes `Setup`.
- `stream-supervision`: Replace `StreamSupervision.LoggingDecider` with `ResumeOnError` from Terp.Akka.

## Impact

- **Dependencies**: `Servus` 0.35.0 / `Servus.Akka` 0.3.15 removed; `Terp` 0.1.1 / `Terp.Akka` 0.1.1 added. Transitive loss of `FrameworkReference Microsoft.AspNetCore.App` from Servus (projects already declare their own).
- **Code**: 11 setup classes renamed, `NjordHost` split, `Program.cs` rewritten (3 lines), `NjordTestHost` updated, ~10 files get using renames, `StreamSupervision.cs` deleted.
- **APIs**: No gRPC or HTTP API changes. Actor system name `njord` unchanged. Persistence records unchanged.
- **Config**: No configuration changes. No env var changes.
- **Metrics**: No metric name changes (step 6 is a non-goal).
- **Polling/Budget**: No impact on API call patterns or budget.
