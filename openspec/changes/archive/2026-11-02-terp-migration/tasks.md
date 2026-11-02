## 1. Packages + usings + containers + NjordHost + Program.cs (Steps 1-4)

This is one atomic block: the build is only green after all four steps are done.

- [x] 1.1 Replace Servus packages with Terp in `src/Directory.Packages.props` and all csproj files. Drop unused `Servus` from `Njord.Core` and `Servus.Akka` from `Njord.Core.Tests`. Add `Terp` 0.1.1 and `Terp.Akka` 0.1.1. See MIGRATION.md step 1 for exact commands.
- [x] 1.2 Rename `using Servus.*` to `using Terp.*` in all non-container files (step 2): `Njord.Grpc/GrpcConsumer.cs`, `Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`, `Njord.Polling/PollManager.cs`, `Njord.Polling/PollWorker.cs`, `Njord.Polling/Configuration/PollingRegistrationExtensions.cs`, `Njord.Pipeline.Tests/Helpers/ProductionLikeSchedulerActor.cs`, `Njord.Pipeline.Tests/Helpers/SinkRefPersistentConsumerActor.cs`.
- [x] 1.3 Convert `AkkaSetupContainer` to `AkkaSetup : ActorSystemSetup` (step 3): `git mv` file, change base class, `ActorSystemName` property override, `using Terp.Akka;`. File: `src/Njord/Configuration/AkkaSetupContainer.cs` -> `AkkaSetup.cs`.
- [x] 1.4 Convert 11 service containers to `ISetup<IServiceCollection>` (step 4a): rename class and file (`git mv`), `using Terp.Hosting;`, `Setup(IServiceCollection, SetupContext)`. Files: `CoreSetupContainer`, `ConfigSetupContainer`, `HealthSetupContainer`, `DomainSetupContainer`, `OpenMeteoSetupContainer`, `BudgetSetupContainer`, `PollingSetupContainer`, `PipelineSetupContainer`, `GrpcSetupContainer`.
- [x] 1.5 Convert `NjordApplicationSetup` to `ISetup<WebApplication>` (step 4b): `using Terp.Hosting;`, `Setup(WebApplication, SetupContext)`.
- [x] 1.6 Split `NjordHost.Create` into `Configure(builder)` + `Configure(app)` with explicit `builder.Setup(new XxxSetup())` calls (step 4c). File: `src/Njord/NjordHost.cs`.
- [x] 1.7 Rewrite `Program.cs` to use `NjordHost.Configure` (step 4d). File: `src/Njord/Program.cs`.
- [x] 1.8 Update `NjordTestHost.cs` to use `NjordHost.Configure(builder)` + `NjordHost.Configure(app)` (step 4e). File: `src/Njord.IntegrationTests/NjordTestHost.cs`.
- [x] 1.9 Update `stryker-config.json`: `"!**/*SetupContainer.cs"` to `"!**/*Setup.cs"`.

### Validation

```bash
cd src
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```

## 2. Stream supervision (Step 7)

- [x] 2.1 Replace `ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log))` with `.ResumeOnError(_log)` in `src/Njord.Pipeline/Stream/ForecastPipeline.cs` and `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`. Add `using Terp.Akka;` where not already present.
- [x] 2.2 Replace inline `ActorAttributes.CreateSupervisionStrategy(_ => Directive.Resume)` with `.ResumeOnError()` in `src/Njord.Pipeline.Tests/Helpers/ProductionLikePipelineActor.cs` and `src/Njord.Pipeline.Tests/Helpers/SinkRefHubConsumerTests.cs`.
- [x] 2.3 Delete `src/Njord.Core/Actors/StreamSupervision.cs`. Remove `using Akka.Streams.Supervision;` where `Directive`/`Decider` was the only reason for the import.

### Validation

```bash
cd src
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
grep -rn "StreamSupervision\|CreateSupervisionStrategy" --include="*.cs" . | grep -v bin | grep -v obj
```

## 3. Docs and specs (Step 8)

- [x] 3.1 Update `CLAUDE.md`: replace Servus skill routing entry with Terp.
- [x] 3.2 Update `AGENTS.md`: rename container references to setup references, update `CoreSetup.NjordMeters`, add Terp setup rule.
- [x] 3.3 Rename `openspec/specs/servus-bootstrap` to `terp-bootstrap` (`git mv`). Update `spec.md` content.
- [x] 3.4 Rename `openspec/specs/domain-setup-containers` to `domain-setups` (`git mv`). Update `spec.md` content, remove stale MQTT and SupervisorDefaults requirements.
- [x] 3.5 One-word renames in other specs: `architecture-zone-enforcement`, `health-checks`, `options-conventions`, `per-domain-telemetry`, `persistence-provider`, `service-configuration`. Read each file first, change only the identifiers.

### Validation

```bash
cd ..
openspec validate --all --no-interactive
bash scripts/check-openspec-root.sh
grep -rIn "Servus" . --exclude-dir=archive --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude=CHANGELOG.md --exclude=MIGRATION.md
```
