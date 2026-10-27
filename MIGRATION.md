# Migration: Servus / Servus.Akka to Terp / Terp.Akka

Plan only. Nothing described here has been applied. Language of the repo is English (CLAUDE.md), no em dashes.
All commands run from `src/` (where `global.json` lives), as in AGENTS.md. Never `git push`; commit per step with a
single-line Conventional Commit message (e.g. `refactor: replace Servus with Terp`).

## 1. Summary, scope, prerequisites

Servus 0.35.0 and Servus.Akka 0.3.15 are replaced by Terp (`Terp`, which includes the hosting setup API, and `Terp.Akka`). What changes:

- Setup containers (`IServiceSetupContainer`, `ApplicationSetupContainer<WebApplication>`, `ActorSystemSetupContainer`, `AppBuilder`/`AppRunner`)
  become `ISetup<T>` classes applied with `builder.Setup(...)` / `app.Setup(...)`.
- Actor helpers (`GetActor`, `GetActorAsync`, `TryGetActor`, `BackoffOnFailure`) keep their shape; only the namespace changes.
- `Servus.Resilience.Backoff` becomes `Terp.Resilience.Backoff` (same API, plus an optional `Random`).
- `NjordHost.Create(builder, build)` (returns `AppRunner`) is split into `Configure(builder)` and `Configure(app)`; the build step moves to the caller.
- Persisted data and HOCON contain no Servus type names (repo-wide grep: Servus appears only in csproj, props, `.cs` usings, CLAUDE.md and two specs), so no data migration.

Out of scope: `openspec/changes/archive` and `CHANGELOG.md` stay untouched. Also not part of this repo: the skill
`akka-skills:setup-container` (see section 5).

Prerequisites:

1. Terp has a first published release (0.x). Look up the actual versions on NuGet; `Terp` and `Terp.Akka` are released independently, so their
   version numbers differ. Below, `<TERP>` and `<TERP_AKKA>` stand for them. Do not guess.
2. `Terp.Akka` depends on `Terp` with a minimum version (`>=` the `Terp` version it was packed against; no hand-managed floors). The `Terp` version
   njord pins (CPM transitive pinning is on) must be at least that, otherwise NU1109 (package downgrade).
3. Terp API is final for this migration. Setups are applied by explicit calls only (see "Order of setups" in section 4).
4. Renovate: njord uses `local>st0o0/renovate-config:dotnet`. Check in that preset repo that groups `Terp` and `Terp.*` exist (and drop any Servus group);
   `renovate.json` in njord needs no change.

NuGet rename table:

| Old | New | Notes |
|-----|-----|-------|
| `Servus` 0.35.0 | `Terp` `<TERP>` | contains the setup API (`Terp.Hosting`), Diagnostics, Resilience, Network and the date helpers; others get it transitively through Terp.Akka |
| `Servus.Akka` 0.3.15 | `Terp.Akka` `<TERP_AKKA>` | brings `Terp` and `Akka.Hosting` (floor 1.5.0) |

Transitive differences to expect: Servus (via Servus.Akka) carried a `FrameworkReference Microsoft.AspNetCore.App`; Terp has no ASP.NET dependency (only the Microsoft.Extensions abstractions).
See step 1.

## 2. Before/after mapping

| Servus | Terp | Files |
|--------|------|-------|
| `Servus.Application.Startup.IServiceSetupContainer.SetupServices(services, configuration)` | `Terp.Hosting.ISetup<IServiceCollection>.Setup(services, context)` (package `Terp`), configuration = `context.Configuration` | the 9 service containers below |
| `ApplicationSetupContainer<WebApplication>.SetupApplication(app)` | `ISetup<WebApplication>.Setup(app, context)`, applied with `app.Setup(...)` | `Njord/Configuration/NjordApplicationSetup.cs` |
| `Servus.Akka.Startup.ActorSystemSetupContainer` | `Terp.Akka.ActorSystemSetup` (abstract, `ISetup<IServiceCollection>`; `GetActorSystemName()` becomes the property `ActorSystemName`, `BuildSystem` unchanged) | `Njord/Configuration/AkkaSetupContainer.cs` |
| `AppBuilder.Create(builder, build).WithSetup<T>()...Build()` returns `AppRunner` | normal builder plus `builder.Setup(new T())` in the wanted order; caller does `Build()` and `RunAsync()` | `Njord/NjordHost.cs`, `Njord/Program.cs` |
| `AppRunner.RunAsync()` / `StartAsync(token)` | `WebApplication.RunAsync()` / `StartAsync(token)` | `Program.cs`, `Njord.IntegrationTests/NjordTestHost.cs` |
| `WithStartupGate`, `OnApplication*` | `services.AddStartupGate<T>()` (`using Terp.Hosting.Gates;`), host lifetime | not used in njord |
| `using Servus.Akka;` (`GetActor`, `GetActorAsync`, `TryGetActor`, `BackoffOnFailure`) | `using Terp.Akka;` | see list below |
| `using Servus.Resilience;` (`Backoff.Create`, `BackoffPolicy`) | `using Terp.Resilience;` | `Njord.Polling/PollManager.cs` |
| `using Servus.Application.Startup;` | `using Terp.Hosting;` (package `Terp`) | containers, `NjordHost.cs` |

Files with `using Servus.Akka;`: `Njord.Grpc/GrpcConsumer.cs`, `Njord.OpenMeteo/OpenMeteoGateway.cs`, `Njord.Polling/PollManager.cs`,
`Njord.Polling/PollWorker.cs`, `Njord.Polling/Configuration/PollingRegistrationExtensions.cs`, `Njord.Budget/Configuration/BudgetSetupContainer.cs`,
`Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs`, `Njord.Pipeline.Tests/Helpers/ProductionLikeSchedulerActor.cs`,
`Njord.Pipeline.Tests/Helpers/SinkRefPersistentConsumerActor.cs`.

Do NOT migrate njord's own lookalikes: `Njord.Tests.Shared/NjordActorSpec.cs` (`GetActor<TKey>()` on `ActorRegistry`),
`Njord.Polling/FailureBackoff.cs`, `Njord.Polling.Tests/FailureBackoffSpec.cs`, `Njord.Polling/DomainPollStateExtensions.cs` (its `RetryBackoff`).
`OpenMeteoGatewayRoutingSpec.cs` only calls the `NjordActorSpec` method. `Njord/Configuration/AkkaLogLevel.cs` and `ActorCategoryLogger.cs` have no Servus dependency: unchanged.

Service containers (all `IServiceSetupContainer`; host: `CoreSetupContainer`, `ConfigSetupContainer`, `HealthSetupContainer`, `DomainSetupContainer`;
libraries: `OpenMeteoSetupContainer`, `BudgetSetupContainer`, `PollingSetupContainer`, `PipelineSetupContainer`, `GrpcSetupContainer`).
Plus `AkkaSetupContainer` and `NjordApplicationSetup`. Rename every `XxxSetupContainer` to `XxxSetup` (Terp convention, class and file) with `git mv`.

## 3. Step-by-step plan

Each step ends green. Run the full acceptance block after every step that touches code:

```powershell
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
# all test projects, sequentially (AGENTS.md)
```
```bash
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```

### Step 1: packages

Files: `Directory.Packages.props`, `Njord/Njord.csproj`, `Njord.Core/Njord.Core.csproj`, `Njord.Core.Tests/Njord.Core.Tests.csproj`,
`Njord.Budget`, `Njord.Grpc`, `Njord.OpenMeteo`, `Njord.Pipeline`, `Njord.Polling` csproj.

1. Unused references (verified, no `Servus` identifier in any `.cs` of these projects): `Servus` in `Njord.Core` and `Servus.Akka` in `Njord.Core.Tests`
   can simply be dropped. Also drop `Servus` from `Njord.csproj` (host uses only `Servus.Application.Startup`, replaced in step 4). The host needs no direct Terp reference: `Terp.Akka` brings `Terp`. Add `Terp` directly only to a project that uses `ISetup` or the other Terp helpers without `Terp.Akka`.
   ```powershell
   dotnet remove Njord.Core/Njord.Core.csproj package Servus
   dotnet remove Njord.Core.Tests/Njord.Core.Tests.csproj package Servus.Akka
   ```
2. Replace in the six remaining projects (`Njord`, `Njord.Budget`, `Njord.Grpc`, `Njord.OpenMeteo`, `Njord.Pipeline`, `Njord.Polling`):
   ```powershell
   foreach ($p in 'Njord','Njord.Budget','Njord.Grpc','Njord.OpenMeteo','Njord.Pipeline','Njord.Polling') {
     dotnet remove "$p/$p.csproj" package Servus.Akka
     dotnet add "$p/$p.csproj" package Terp.Akka --version <TERP_AKKA>
   }
   dotnet remove Njord/Njord.csproj package Servus
   ```
   `using Terp.Hosting;` resolves through `Terp.Akka` in all of these projects; a project that needs only `Terp.Resilience` and does not use `Terp.Akka` would reference `Terp` explicitly.
   Njord.Pipeline.Tests uses `Servus.Akka` through project references only; it gets `Terp.Akka` transitively, no direct reference needed.
3. In `Directory.Packages.props` delete `<PackageVersion Include="Servus" .../>` and `Servus.Akka` (dotnet remove does not), and make sure both exist:
   `<PackageVersion Include="Terp" Version="<TERP>" />` (pins the transitive package) and `<PackageVersion Include="Terp.Akka" Version="<TERP_AKKA>" />`.
4. Expected build breaks:
   - `using Servus...` unresolved: intentional, fixed in step 2. To keep this step green, do steps 1 and 2 in one commit.
   - Projects that lost the transitive `Microsoft.AspNetCore.App` framework reference: `Budget`, `Grpc`, `OpenMeteo`, `Pipeline`, `Polling` declare their own
     `FrameworkReference` (verified), host is `Sdk.Web`. `Njord.Core` and `Njord.Core.Tests` (and projects that referenced Core and relied on it) declared none and may
     now miss ASP.NET types: if the build reports missing `Microsoft.AspNetCore.*`/`HealthChecks` types, add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to
     exactly that project (or the one specific package). Do not re-add Servus.

Acceptance: `dotnet restore Njord.slnx` has no NU1109/NU1605; full build after step 2.

### Step 2: using renames

Mechanical, outside the containers (those change in step 4):

| File | Edit |
|------|------|
| `Njord.Grpc/GrpcConsumer.cs`, `Njord.OpenMeteo/OpenMeteoGateway.cs`, `Njord.Polling/PollWorker.cs`, `Njord.Polling/Configuration/PollingRegistrationExtensions.cs`, `Njord.Pipeline.Tests/Helpers/ProductionLikeSchedulerActor.cs`, `Njord.Pipeline.Tests/Helpers/SinkRefPersistentConsumerActor.cs` | `using Servus.Akka;` to `using Terp.Akka;` |
| `Njord.Polling/PollManager.cs` | `using Servus.Akka;` to `using Terp.Akka;` and `using Servus.Resilience;` to `using Terp.Resilience;` |

`Terp.Resilience` is in package `Terp` (transitive). Keep `using` blocks sorted (`dotnet format` enforces). Call sites do not change (`Context.GetActorAsync<IForecastPipeline>()`, `Context.GetActor<IBudgetTracker>()`,
`Context.TryGetActor<IPollRegion>(out var region)`, `Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30))`,
`.BackoffOnFailure("poll-manager", MinBackoff, MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1))`: signatures are identical).
`Budget`/`OpenMeteo` containers contain the same `BackoffOnFailure` calls; they get their usings in step 4.

Acceptance: build still red only in the host and the five library containers; `dotnet build Njord.Polling/Njord.Polling.csproj` style per-project builds are not used (AGENTS.md: solution only), so finish step 4 before the first green full build, or do 1 to 4 in one working session.

### Step 3: ActorSystemSetup

File: `Njord/Configuration/AkkaSetupContainer.cs` to `AkkaSetup.cs`.

Before: `using Servus.Akka.Startup;` and `public sealed class AkkaSetupContainer : ActorSystemSetupContainer`.
After:

```csharp
using Terp.Akka;

namespace Njord.Configuration;

public sealed class AkkaSetup : ActorSystemSetup
{
    protected override string ActorSystemName => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        // body unchanged: ConfigureLogging, ConfigureInfrastructure, foreach IActorRegistration
    }
    // ConfigureLogging / ConfigureInfrastructure unchanged
}
```

`ActorSystemSetup` already implements `ISetup<IServiceCollection>` (no `SetupServices` override exists to port). The name `njord` passes the eager validation
(letters, digits, `-`, `_`). The `IActorRegistration` loop stays in this step (replaced only in the optional step 5; it stays the only place where actors are added). Update the references to the class:
`NjordHost.cs` (step 4). Acceptance with step 4.

### Step 4: containers, NjordHost, Program.cs, NjordTestHost (one block)

4a. Convert the nine service containers. Rename class and file (`git mv`), change usings, signature and `configuration` use. Template, real code from `ConfigSetupContainer`:

Before:
```csharp
using Servus.Application.Startup;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        ...
```
After:
```csharp
using Terp.Hosting;

public sealed class ConfigSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        ...
```
Bodies stay unchanged. Per-container notes:
- None of the nine containers reads the `configuration` parameter (verified in the listed sources). If one does after all, use `context.Configuration`.
- `DomainSetupContainer` uses `typeof(DomainSetupContainer)` as logger category (`CreateLogger<DomainSetupContainer>()`): rename to `CreateLogger<DomainSetup>()`; the log category changes from `Njord.Configuration.DomainSetupContainer` to `Njord.Configuration.DomainSetup`. Check `appsettings*.json` for a Serilog override on the old name (none expected).
- `CoreSetupContainer.NjordMeters` becomes `CoreSetup.NjordMeters` (referenced in AGENTS.md and a spec, see section 5). Its `Metrics.ConfigureMeterAdapter(...)` call is global state, unchanged.
- `BudgetSetup` and `OpenMeteoSetup`: `using Servus.Akka;` to `using Terp.Akka;` (for `BackoffOnFailure`). The nested `*ActorRegistration` classes stay.
- Library files that become `using Terp.Hosting;`: `Njord.Budget`, `Njord.Grpc`, `Njord.OpenMeteo`, `Njord.Pipeline`, `Njord.Polling` `Configuration/*Setup.cs`.
- `stryker-config.json` (in `src/`): `"!**/*SetupContainer.cs"` to `"!**/*Setup.cs"` (matches `NjordApplicationSetup.cs` and `AkkaSetup.cs` as well, as before for the containers; check `mutate` intent: setup wiring is excluded).

4b. `NjordApplicationSetup`. Before: `ApplicationSetupContainer<WebApplication>` with `protected override void SetupApplication(WebApplication app) => Configure(app);`.
After (keep `Configure` and `ExposeHealthEndpoints` unchanged, they stay `internal static`; no test calls them today, verified by grep):

```csharp
using Terp.Hosting;

public sealed class NjordApplicationSetup : ISetup<WebApplication>
{
    internal static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    public void Setup(WebApplication app, SetupContext context) => Configure(app);

    internal static void Configure(WebApplication app) { /* unchanged */ }
}
```

4c. `NjordHost` split. Public test seam is now two calls instead of `Create(builder, build)`.

Before (real code, abridged):
```csharp
public static AppRunner Create(WebApplicationBuilder builder, Func<WebApplicationBuilder, WebApplication>? build = null)
{
    builder.Services.AddSerilog(config => { ... });
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => { options.Limits.MinResponseDataRate = null; });

    return AppBuilder.Create(builder, build ?? (b => b.Build()))
        .WithSetup<CoreSetupContainer>()
        ... .WithSetup<NjordApplicationSetup>()
        .Build();
}
```
After:
```csharp
using Terp.Hosting;

public static class NjordHost
{
    // The composition of the service in two halves; the caller builds in between. Integration tests override services and
    // add the test server between the two calls.
    public static WebApplicationBuilder Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog(config => { /* unchanged */ });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => { options.Limits.MinResponseDataRate = null; });

        return builder
            .Setup(new CoreSetup())
            .Setup(new ConfigSetup())
            .Setup(new HealthSetup())
            .Setup(new OpenMeteoSetup())
            .Setup(new BudgetSetup())
            .Setup(new PollingSetup())
            .Setup(new PipelineSetup())
            .Setup(new DomainSetup())   // needs logging from AddSerilog above, already applied
            .Setup(new GrpcSetup())
            .Setup(new AkkaSetup());     // the one actor system of the host; a second one throws
    }

    public static WebApplication Configure(WebApplication app) => app.Setup(new NjordApplicationSetup());
}
```
Order is the same as before (Core, Config, Health, OpenMeteo, Budget, Polling, Pipeline, Domain, Grpc, Akka, Application). Call on a `WebApplicationBuilder`-typed
expression so `Setup<TBuilder>` keeps the type; do not use `builder.Setup<X>()` here (returns `IHostApplicationBuilder`; use `Setup<WebApplicationBuilder, X>()` if ever needed).
Kestrel and Serilog stay inline; they may later move into `ISetup<WebApplicationBuilder>` / `ISetup<ILoggingBuilder>` classes, not required.

4d. `Program.cs`. Before: `await NjordHost.Create(builder).RunAsync();` After:
```csharp
using Njord;

var builder = NjordHost.Configure(WebApplication.CreateBuilder(args));
var app = NjordHost.Configure(builder.Build());

await app.RunAsync();
```

4e. `Njord.IntegrationTests/NjordTestHost.cs`. Replace the `WebApplication? app = null; var runner = NjordHost.Create(builder, b => {...});` block and the
`runner.StartAsync` call:

```csharp
NjordHost.Configure(builder);

builder.Services.RemoveAll<IReadOnlyDictionary<DomainKind, IDomainClient>>();
builder.Services.AddSingleton<IReadOnlyDictionary<DomainKind, IDomainClient>>(new Dictionary<DomainKind, IDomainClient>
{
    [DomainKind.Weather] = weather,
    // ... unchanged entries
});
builder.WebHost.UseTestServer();

var app = NjordHost.Configure(builder.Build());
await app.StartAsync(cancellationToken);
return new NjordTestHost(app, weather, airQuality, flood, marine, dataDirectory);
```
The comment above the class ("real setup containers", "the call Program.cs makes") becomes "NjordHost.Configure". `Njord` keeps `InternalsVisibleTo Njord.IntegrationTests`.
Old `AppRunner` printed an unhandled startup exception with `Console.WriteLine` before rethrowing; now the exception propagates from `Program.cs` (the runtime prints it).

Acceptance (first green full build of steps 1 to 4): build, `dotnet format --verify-no-changes`, ALL test projects, especially `Njord.IntegrationTests`
(`HostSmokeSpec`, `PersistenceRestartSpec` expect `OptionsValidationException` from `StartAsync`, which still comes from the options validators started with the host).
Optional: `dotnet run --project Njord/Njord.csproj` and call `/healthz`, `/alive`, `/metrics`.

### Step 5 (optional): actors move into BuildSystem, IActorRegistration goes away

Terp has no contributor mechanism: `ActorSystemSetup` owns the one actor system per host and every actor is added in `BuildSystem`. The `IActorRegistration` indirection can be replaced by one public extension per library that `AkkaSetup.BuildSystem` calls. Do this only after steps 1 to 4 are green.

Before (`BudgetSetup` after step 4): `Setup` registers `services.AddSingleton<IActorRegistration>(new BudgetActorRegistration())` plus the health check; the nested `BudgetActorRegistration.Configure(builder, provider)` calls `builder.WithSingleton<IBudgetTracker>(...)`.
After:
```csharp
public sealed class BudgetSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context) =>
        services.AddHealthChecks().AddCheck<BudgetHealthCheck>("budget", failureStatus: HealthStatus.Unhealthy);
}

public static class BudgetAkkaExtensions
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private const double RandomFactor = 0.2;

    public static AkkaConfigurationBuilder WithBudget(this AkkaConfigurationBuilder builder) =>
        builder.WithSingleton<IBudgetTracker>("budget-tracker-supervisor",
            (_, _, resolver) => resolver.Props<BudgetTracker>().BackoffOnFailure("gateway", MinBackoff,
                MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)));
}
```
(`using Akka.Hosting; using Terp.Akka;`; `BackoffOnFailure` comes from `Terp.Akka`. Put the extension in its own file `BudgetAkkaExtensions.cs`.) Same for OpenMeteo (gateway), Pipeline, Grpc, and Polling (`WithPolling()`). The `provider` parameter of `IActorRegistration.Configure` is unused in all five; if one needs it, take it from the `BuildSystem` argument and pass it in.

Then:
1. `AkkaSetup.BuildSystem`: replace the `foreach (var registration in serviceProvider.GetServices<IActorRegistration>())` loop by the calls in the old registration order (the Polling shard region before the manager is inside `WithPolling`):
   ```csharp
   builder.WithOpenMeteo().WithBudget().WithPolling().WithPipeline().WithGrpc();
   ```
2. Delete `Njord.Core/Actors/IActorRegistration.cs` and all `*ActorRegistration` nested classes; update AGENTS.md line 25 (`Actors/ # IActorRegistration, ...`).
3. `NjordHost.Configure(builder)` needs no reordering: `AkkaSetup` only registers the actor system, and `BuildSystem` runs later when the service provider exists.

Risks: (a) exactly one actor system per host; a second `ActorSystemSetup` (or a test adding its own `AddAkka`) throws. (b) Integration tests and `Njord.Pipeline.Tests` do not depend on `IActorRegistration`; the full test run is the proof.
Acceptance: full acceptance block; `HostSmokeSpec` proves all singletons resolve.

### Step 6 (optional): metrics replacements

Not required; each changes metric names or is a pure cleanup.
- `Njord.OpenMeteo/HttpMetricsHandler.cs` (counter `njord_openmeteo_http_requests_total{status_class}`, histogram `njord_openmeteo_http_duration_seconds`) can be replaced by
  `new TerpHttpMetricsHandler("openmeteo")` (adds tag `api` to the built-in HttpClient metrics; series `http_client_request_duration_seconds_*{api, http_response_status_code, error_type}`).
  In `OpenMeteoSetup.AddDomainClient`: `builder.AddHttpMessageHandler(() => new HttpMetricsHandler());` to `builder.AddHttpMessageHandler(() => new TerpHttpMetricsHandler("openmeteo"));`; delete `HttpMetricsHandler.cs` and the two
  `Telemetry.Http*` instruments. The prometheus-net adapter in `CoreSetup` filters by meter name (`NjordMeters`); the built-in meter is `System.Net.Http`, so add the literal `"System.Net.Http"` to `NjordMeters`
  (this also exports its other instruments, e.g. open connections). Dashboards, alerts and `docs/` that use the old names must be updated (none in the repo besides TODO.md lines 38 and 52, which mention the handler). Metric-name change: do it only deliberately.
- `Telemetry.FetchDuration` in `OpenMeteoGateway.cs` (line ~130, `Stopwatch` + `Record(sw.Elapsed.TotalSeconds, tags...)`) and the `Stopwatch` in `ForecastPipeline.cs` (~line 110) could use `histogram.StartTimer(...)`, but `StartTimer` adds an `outcome` tag: series change. Skip unless wanted.
- `Njord.Budget/Telemetry.cs` `ObservableGauge<long>` with `Volatile` fields could become BCL `Gauge<long>` (.NET 9+, `Record` on change). That is a BCL feature, not Terp; optional cleanup only.

### Step 7: docs and specs

See section 5. Acceptance: `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh` (repo root); `grep -rIn "Servus" . --exclude-dir=archive --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude=CHANGELOG.md` returns nothing except this file.

## 4. Behaviour differences and risks

- Ordering: a direct `builder.Setup(x)` runs immediately, in call order; inside one setup the targets run in a fixed order (configuration, logging, services, metrics, builder).
  Servus ran phase-major across all containers (all logging, then configuration, then services, each over all containers). For njord this is equivalent: only service containers exist, and the Serilog/Kestrel code runs before the first `Setup`, as before.
- `DomainSetup` relies on logging (`AddSerilog`) set up earlier: still true because that code stays at the top of `Configure(builder)`.
- Second actor system: `ActorSystemSetup` throws `InvalidOperationException` if an `ActorSystem` is already registered. Test code that adds its own `AddAkka` on the same service collection must be checked (none in `NjordTestHost`).
- `ActorSystemSetup` validates the name eagerly at `Setup` time (`ArgumentException`), not at host start.
- Backoff: `Terp.Resilience.BackoffPolicy` fixes the overflow with `TimeSpan.MaxValue` in `DelayWithJitter` and accepts an injectable `Random`. `PollManager` uses `Random.Shared` (no injection) with `maxDelay` 30 s, so production behaviour is unchanged and no existing PollManager test pins an exact jittered delay (none references `RetryPolicy`; verify with the full Polling test run). If a future test wants determinism, pass `random:` to `Backoff.Create`.
- Registry lookups are unchanged in semantics: `GetActor<T>` throws `MissingActorRegistryEntryException` when the marker is not registered, `GetActorAsync<T>` waits until it is registered, `TryGetActor<T>` returns false and `Nobody`. `PollManager` and `PollWorker` rely on `TryGetActor` plus a retry timer; `OpenMeteoGateway` on `GetActor<IBudgetTracker>` in its constructor (the budget singleton must be registered, which both old and new order guarantee).
- Lifecycle callbacks and startup gates: `AppBuilder.OnApplication*`/`WithStartupGate` are not used in njord; nothing to port. Gates, if added later: `services.AddStartupGate<T>()` (`using Terp.Hosting.Gates;`; also `AddStartupGate(name, ct => ...)`; `IStartupGate.CheckAsync(CancellationToken)` returns `Task<bool>`; they run in `StartingAsync`, before Kestrel and the actor system, with fixed timing: 1 s doubling to 30 s, 10 s per check, not configurable).
- Order of setups: a direct `builder.Setup(x)` runs immediately, so setups run in call order; inside one class that implements several targets the order is fixed (configuration, logging, services, metrics, builder). There is no discovery and no ordering attribute: setups are listed explicitly. njord keeps its fixed order in `NjordHost.Configure(builder)` (`DomainSetup` after the inline Serilog code). A setup that matches no target throws `InvalidOperationException`.
- Trimming of dependencies: Terp has only floors (`[1.5.0,)` for Akka.Hosting); njord's own pins in `Directory.Packages.props` still win.

## 5. Text updates

`CLAUDE.md` line 44: `| Servus setup containers | \`akka-skills:setup-container\` |` to `| Terp setups (ISetup, ActorSystemSetup) | \`akka-skills:setup-container\` |` (keep the skill name until the skill is renamed; see below).

`AGENTS.md`:
- Line 43: `Njord/ # Host: Program.cs, NjordHost, Configuration/ (setup containers, PersistenceOptions), Health/` to `... Configuration/ (setups: ISetup<T> classes, PersistenceOptions), Health/`.
- Line 48 stays. Line 58 `Registration stays central in the Host.` stays.
- Line 25 (only if step 5 is done): drop `IActorRegistration,` from the `Actors/` description.
- Metrics section (~line 297): `CoreSetupContainer.NjordMeters` to `CoreSetup.NjordMeters`.
- Add under Akka a short rule (replacing any container wording): "Setups are `XxxSetup : ISetup<T>` (Terp.Hosting), applied explicitly, in order, in `NjordHost.Configure(builder)`; `AkkaSetup : ActorSystemSetup` (Terp.Akka) is the single actor system of the host and adds all actors in `BuildSystem`."

OpenSpec (edit, no archive): rename `openspec/specs/servus-bootstrap` to `terp-bootstrap` (`git mv` the directory; check `scripts/check-openspec-root.sh`).
- `terp-bootstrap/spec.md` Purpose: "Structured startup using Terp setups (`ISetup<T>`): service DI registrations and Akka.NET actor system configuration are encapsulated in domain-specific setup classes, keeping Program.cs minimal." Title `# terp-bootstrap Specification`.
  Replace `AkkaSetupContainer extending ActorSystemSetupContainer` by `AkkaSetup extending Terp.Akka.ActorSystemSetup`; `setup container`/`IActorRegistration from a setup container` by `setup` (and, after step 5, "actor extension called from `AkkaSetup.BuildSystem`"); `actor system setup container` by `actor system setup`. Scenario texts that say `IActorRegistration` follow step 5 wording: "registered in `AkkaSetup.BuildSystem` through the library's `WithXxx` extension".
- `domain-setup-containers/spec.md` (rename to `domain-setups`): Purpose "Domain-specific Terp `ISetup<IServiceCollection>` implementations ..."; every `XxxSetupContainer` to `XxxSetup`, `SetupServices` to `Setup`; the first requirement ("IActorRegistration interface") becomes, after step 5, "Domain libraries expose their actors as `AkkaConfigurationBuilder` extensions" with the scenarios: library exposes a `WithXxx` extension for its actors, central `AkkaSetup.BuildSystem` calls them, deterministic order (the old registration order). Without step 5 keep the `IActorRegistration` requirement and only rename classes. `AkkaSetupContainer configures only infrastructure` to `AkkaSetup ...`. Note: this spec also still mentions MQTT (`MqttSetupContainer`), `OpenMeteoOptions`, `StorageOptions`, which are stale; fix or remove those requirements while editing.
- One-word renames (`AkkaSetupContainer` to `AkkaSetup`, `CoreSetupContainer` to `CoreSetup`, "setup container" to "setup"): `architecture-zone-enforcement/spec.md` lines 79 to 83, `health-checks/spec.md` line 18, `options-conventions/spec.md` line 7, `per-domain-telemetry/spec.md` line 19 (`CoreSetup.NjordMeters`), `persistence-provider/spec.md` line 8, `service-configuration/spec.md` line 215. Read each line first; change only the identifiers.

Skill `akka-skills:setup-container` (`D:\GIT\akka-skills`): separate change, not part of this repo. It documents Servus AppBuilder containers; rewrite it for `ISetup<T>`, `builder.Setup`, `ActorSystemSetup` (actors added in `BuildSystem`), `NjordHost.Configure` split. Until then CLAUDE.md routes to a stale skill: mention this in the commit or TODO.

## 6. Verification checklist and rollback

Checklist:
- [ ] `grep -rIn "Servus" src` finds nothing (excluding `bin`/`obj`); `Directory.Packages.props` has no Servus entries.
- [ ] `Directory.Packages.props` has `Terp` and `Terp.Akka`; every `using Terp.Hosting;` resolves; `Terp` is referenced directly only where it is used without `Terp.Akka`.
- [ ] `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes` clean.
- [ ] All test projects green, sequentially, including `Njord.IntegrationTests` and `Njord.Architecture.Tests` (all classes sealed: new setups are `sealed`).
- [ ] `dotnet run` the host: `/healthz` 200, `/alive`, `/metrics` shows `njord_*` series; logs show the actor system `njord` and all singletons (`poll-manager`, `gateway-supervisor`, `budget-tracker-supervisor`, `pipeline`, `grpc-consumer`, shard region `poll-worker`).
- [ ] Optional full-stack check with the `e2e-test` skill.
- [ ] Docs and specs updated (section 5); `openspec validate --all --no-interactive` passes.

Rollback: one commit per step (steps 1 to 4 may be one). `git revert` the commits in reverse order; Servus 0.35.0 / Servus.Akka 0.3.15 stay available on NuGet. Persisted data
is unaffected (no Servus type names in journal or snapshots, actor names and the system name `njord` unchanged), so a rollback needs no data migration. Do not push; the owner pushes.
