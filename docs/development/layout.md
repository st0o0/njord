# Repository layout

## Solution structure

10 production projects, one test project each (except Host), plus `Njord.Tests.Shared`.

```
src/
  Njord.slnx                     # Solution file (.slnx format)
  Directory.Build.props           # Shared build properties, version (release-please)
  Directory.Packages.props        # Central package management
  global.json                     # SDK pin

  Njord.Core/                     # Foundation: vocabulary, domain logic, actor infrastructure
  Njord.Messages/                 # Inter-actor contracts (refs Core + Akka.Streams)
  Njord.Persistence/              # Extend-only persistence records (no Njord refs)
  Njord.OpenMeteo/                # 6 typed API clients, gateway, budget gate
  Njord.Budget/                   # BudgetTracker actor, usage accounting
  Njord.Polling/                  # PollManager, PollWorker, polling telemetry
  Njord.Pipeline/                 # ForecastPipeline, FeatureEngine, stream materialization
  Njord.Features/                 # 89 feature rules organized by OutputKind
  Njord.Grpc/                     # NjordGrpcService, GrpcConsumer, entity mapping
  Njord/                          # Host: Program.cs, setup chain, health checks

  Njord.Core.Tests/               # One test project per library
  Njord.Messages.Tests/
  Njord.Persistence.Tests/
  Njord.OpenMeteo.Tests/
  Njord.Budget.Tests/
  Njord.Polling.Tests/
  Njord.Pipeline.Tests/
  Njord.Features.Tests/
  Njord.Grpc.Tests/
  Njord.IntegrationTests/         # Whole host via TestServer
  Njord.Architecture.Tests/       # ArchUnitNET dependency rules
  Njord.Tests.Shared/             # Shared fakes and builders

protos/njord/                     # gRPC contract (njord.proto)
docs/                             # VitePress documentation site
openspec/                         # OpenSpec change management
```

## Project responsibilities

| Project | Role | References |
|---------|------|------------|
| `Njord.Core` | Vocabulary (`DomainKind`, `Location`, point types), `FeatureDescriptor`, `DomainRequirements`, configuration, actor markers | Persistence |
| `Njord.Messages` | All inter-actor messages (Fetch, Polling, Stream, Results namespaces) | Core, Akka.Streams |
| `Njord.Persistence` | Extend-only event and snapshot records (`DomainPolled`, `Persisted*State`) | none |
| `Njord.OpenMeteo` | Per-domain clients, `OpenMeteoGateway`, `GatewayBudgetGate`, API variable enums | Core |
| `Njord.Budget` | `BudgetTracker` (persistent actor), health check | Core, Messages |
| `Njord.Polling` | `PollManager`, `PollWorker`, `BudgetGuard`, `PollingOptionsValidator` | Core, Messages |
| `Njord.Pipeline` | `ForecastPipeline`, `FeatureEngine`, stream materialization | Core, Messages |
| `Njord.Features` | 89 `FeatureRule<TInput>` implementations, per-feature config classes | Core |
| `Njord.Grpc` | `NjordGrpcService`, `GrpcConsumer`, `ManifestBuilder`, `EntityIdLookup`, per-OutputKind mappers | Core, Messages |
| `Njord` (Host) | `Program.cs`, setup chain (`ISetup<T>`), health endpoints | all libraries |

## Features layout

Features are organized by OutputKind, then by feature name:

```
Njord.Features/
  Alerts/
    Frost/FrostAlert.cs, FrostConfig.cs, FrostConfigValidator.cs
    Heat/HeatAlert.cs, HeatConfig.cs, HeatConfigValidator.cs
    ...
    Shared/SeverityThresholdsExtensions.cs, ...
  Scores/
    Bbq/BbqScore.cs, BbqConfig.cs
    ...
  Conditions/
    CommuteSafety/CommuteSafetyCondition.cs, CommuteSafetyConfig.cs
    ...
  Trends/, Forecasts/, Weather/, Observations/
  Shared/Scoring/
  ServiceCollectionExtensions.cs    # AddFeatures: one line per feature
```

Each feature has a `FeatureRule<TInput>` (the rule) and a `<Name>Config : IConfigSection` (the config). Features with array thresholds also implement `IHasArrayDefaults` and have a `<Name>ConfigValidator`.
