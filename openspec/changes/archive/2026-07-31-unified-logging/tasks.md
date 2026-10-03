## 1. Output template and configuration

- [x] 1.1 Change Serilog console output template in `src/Njord/Program.cs` from `[{Subsystem,-8}]` to `[{SourceContext}]`
- [x] 1.2 Update `src/Njord/appsettings.json`, `appsettings.Development.json`, and `appsettings.Example.json` — remove any `Subsystem`-related config, add namespace-based `MinimumLevel.Override` examples for `Njord.Pipeline`, `Njord.Mqtt`, etc.

## 2. Migrate Pipeline actors to Akka logger

- [x] 2.1 `src/Njord/Pipeline/SchedulerActor.cs` — replace `ILogger<SchedulerActor>` with `Context.GetLogger()`, remove `_logScope`/`PushProperty`/`Dispose`, remove `using Serilog.Context`, remove DI logger parameter
- [x] 2.2 `src/Njord/Pipeline/PipelineActor.cs` — same migration as 2.1
- [x] 2.3 `src/Njord/Pipeline/BudgetTrackerActor.cs` — already uses `Context.GetLogger()`, remove `.WithContext("Subsystem", "pipeline")` call

## 3. Migrate MQTT actors to Akka logger

- [x] 3.1 `src/Njord/Mqtt/MqttConnectionActor.cs` — replace `ILogger<T>` with `Context.GetLogger()`, remove `_logScope`/`PushProperty`/`Dispose`, remove `using Serilog.Context`
- [x] 3.2 `src/Njord/Mqtt/MqttEgressActor.cs` — same migration
- [x] 3.3 `src/Njord/Mqtt/DiscoveryActor.cs` — same migration

## 4. Migrate Egress and Enrichment actors to Akka logger

- [x] 4.1 `src/Njord/Egress/ModelStateActor.cs` — replace `ILogger<T>` with `Context.GetLogger()`, remove `_logScope`/`PushProperty`/`Dispose`; move `"Capability learned"` log out of static `BuildCapabilityLearned` into the actor, remove `ILogger` parameter from static method
- [x] 4.2 `src/Njord/Enrichment/EnrichmentActor.cs` — replace `ILogger<T>` with `Context.GetLogger()`, remove `_logScope`/`PushProperty`/`Dispose`; move `"Enrichment computed"` log out of static `BuildConsensusInlineFlow` into the actor, remove `ILogger` parameter from static method
- [x] 4.3 `src/Njord/Enrichment/ForecastHistoryActor.cs` — already uses `Context.GetLogger()`, remove `.WithContext("Subsystem", "enrich")` call

## 5. Migrate gRPC actors to Akka logger

- [x] 5.1 `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` — replace `ILogger<T>` with `Context.GetLogger()`, remove `_logScope`/`PushProperty`/`Dispose`
- [x] 5.2 `src/Njord/Grpc/EnrichmentSnapshotActor.cs` — already uses `Context.GetLogger()`, remove `.WithContext("Subsystem", "grpc")` call
- [x] 5.3 `src/Njord/Grpc/ForecastSnapshotActor.cs` — already uses `Context.GetLogger()`, remove `.WithContext("Subsystem", "grpc")` call

## 6. Clean up non-actor classes

- [x] 6.1 `src/Njord/Grpc/OpsGrpcService.cs` — keep `ILogger<T>`, remove `LogContext.PushProperty`/`_logScope` (fixes scope leak), remove `using Serilog.Context`
- [x] 6.2 `src/Njord/Grpc/AdminGrpcService.cs` — keep `ILogger<T>`, remove `LogContext.PushProperty`/`_logScope` (fixes scope leak), remove `using Serilog.Context`
- [x] 6.3 `src/Njord/Mqtt/Transport/MqttNetPublisher.cs` — keep `ILogger<T>`, remove `LogContext.PushProperty`/scope disposal, remove `using Serilog.Context`
- [x] 6.4 `src/Njord/Enrichment/Features/HistoryEnrichment.cs` — keep `ILogger<T>`, remove `LogContext.PushProperty`/scope disposal, remove `using Serilog.Context`

## 7. Update DI registrations

- [x] 7.1 `src/Njord/Configuration/NjordServiceSetup.cs` — remove any `ILogger<T>` registrations that were only needed for actors (actors now self-source their logger)

## 8. Update tests

- [x] 8.1 Update tests under `src/Njord.Tests/` that inject `ILogger<T>` into actors — remove the logger parameter from actor construction, adjust assertions that reference `Subsystem` property
- [x] 8.2 Update tests that verify log output format — adjust expected output from `[pipeline]` to `[Njord.Pipeline.SchedulerActor]` etc.

## 9. Validation

- [x] 9.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 9.2 Tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 9.3 Verify no `using Serilog.Context` in application code: grep `src/Njord/` excluding `Program.cs`
- [x] 9.4 Run `dotnet slopwatch` from repo root, then `dotnet format` whitespace check
