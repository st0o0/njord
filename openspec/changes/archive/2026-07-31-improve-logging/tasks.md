## 1. Configuration & Template

- [x] 1.1 Update `src/Njord/Program.cs`: change Serilog console `outputTemplate` to `[{Timestamp:HH:mm:ss} {Level:u3}] [{Subsystem,-8}] {Message:lj}{NewLine}{Exception}`
- [x] 1.2 Replace `Logging` section in `src/Njord/appsettings.json` with `Serilog` section: `MinimumLevel.Default = Information`, overrides for `Akka: Warning`, `Microsoft.AspNetCore: Warning`, `Microsoft.AspNetCore.Hosting.Diagnostics: Information`, `System.Net.Http.HttpClient: Warning`, `Grpc.AspNetCore.Server: Warning`, `Microsoft.AspNetCore.Routing: Warning`
- [x] 1.3 Update `src/Njord/appsettings.Development.json`: set `Serilog.MinimumLevel.Default = Debug`
- [x] 1.4 Update `src/Njord/appsettings.Example.json` to match the new config structure

## 2. Subsystem Property — Pipeline Actors

- [x] 2.1 `src/Njord/Pipeline/PipelineActor.cs`: add `LogContext.PushProperty("Subsystem", "pipeline")` in constructor, dispose in `PostStop`. Demote `"Pipeline graph materialized"` message — keep at INF. No level changes here.
- [x] 2.2 `src/Njord/Pipeline/SchedulerActor.cs`: add subsystem `"pipeline"`. Demote ref-received logs (`"Pipeline SinkRef received"`, `"Pipeline SourceRef received"`, `"Pipeline refs received - connecting"`) to Debug with enriched content (include sender path). Add new Debug logs: `"Scheduling next poll for {Location}/{Model} at {NextPoll}"`, `"Hash unchanged for {Location}/{Model}"`. Add new Info log: `"Poll complete for {Location}: {Changed}/{Total} models changed in {Duration}ms"` — requires tracking cycle start time and changed-count per location.
- [x] 2.3 `src/Njord/Pipeline/StreamSupervision.cs`: no subsystem change needed (static helper, inherits caller's LogContext). Verify log level stays Warning.

## 3. Subsystem Property — Egress Actors

- [x] 3.1 `src/Njord/Egress/ModelStateActor.cs`: add subsystem `"egress"`. Demote `"Pipeline SourceRef received"` and `"Egress SinkRef received"` to Debug with source actor path. Keep `"Capability learned"` at INF.

## 4. Subsystem Property — MQTT Actors

- [x] 4.1 `src/Njord/Mqtt/MqttConnectionActor.cs`: add subsystem `"mqtt"`. Add new Info log `"MQTT connected to {Host}:{Port}"` in `OnConnectedAsync`. Keep all Warning logs as-is.
- [x] 4.2 `src/Njord/Mqtt/MqttEgressActor.cs`: add subsystem `"mqtt"`. Demote `"Egress SourceRef received"` and `"MQTT SinkRef received"` to Debug with source path. Add new Debug log `"Published {Count} state messages for {Location}"` in `MapToMqttMessages` (count yielded messages per invocation).
- [x] 4.3 `src/Njord/Mqtt/DiscoveryActor.cs`: add subsystem `"mqtt"`. Demote `"MQTT SinkRef received"` and `"Egress SourceRef received"` to Debug with source path. Keep all other logs at their current levels.
- [x] 4.4 `src/Njord/Mqtt/Transport/MqttNetPublisher.cs`: add subsystem `"mqtt"` (non-actor, set in constructor). Keep Debug log as-is.

## 5. Subsystem Property — Enrichment

- [x] 5.1 `src/Njord/Enrichment/EnrichmentActor.cs`: add subsystem `"enrich"`. Demote `"Pipeline SourceRef received"` and `"Egress SinkRef received"` to Debug with source path. Add new Info log `"Enrichment computed for {Location}: {Features}"` in `ComputeAll` or as a stream stage after it, listing enabled feature type names.
- [x] 5.2 `src/Njord/Enrichment/Features/HistoryEnrichment.cs`: add subsystem `"enrich"` in constructor.
- [x] 5.3 `src/Njord/Enrichment/ForecastHistoryActor.cs`: add subsystem `"enrich"` (uses `Context.GetLogger()` — use `WithContext("Subsystem", "enrich")` instead of `LogContext`).

## 6. Subsystem Property — gRPC

- [x] 6.1 `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs`: add subsystem `"grpc"`. Demote `"gRPC snapshot consumer materialized"` to Debug. Keep `"Watched actor terminated"` at Warning.
- [x] 6.2 `src/Njord/Grpc/OpsGrpcService.cs`: add subsystem `"grpc"` (scoped gRPC service — push in constructor, no explicit dispose needed since scoped lifetime handles it). Keep all Warning logs.
- [x] 6.3 `src/Njord/Grpc/AdminGrpcService.cs`: add subsystem `"grpc"`.
- [x] 6.4 `src/Njord/Grpc/ForecastSnapshotActor.cs`: add subsystem `"grpc"` (uses `Context.GetLogger()` — use `.WithContext("Subsystem", "grpc")`).
- [x] 6.5 `src/Njord/Grpc/EnrichmentSnapshotActor.cs`: add subsystem `"grpc"` (uses `Context.GetLogger()` — use `.WithContext("Subsystem", "grpc")`).

## 7. Pipeline BudgetTracker

- [x] 7.1 `src/Njord/Pipeline/BudgetTrackerActor.cs`: add subsystem `"pipeline"` (uses `Context.GetLogger()` — use `.WithContext("Subsystem", "pipeline")`).

## 8. Tests

- [x] 8.1 Update `src/Njord.Tests/Pipeline/StreamSupervisionSpec.cs` if any log level assertions need adjustment (currently asserts `LogLevel.Warning` — should still pass since StreamSupervision stays at Warning).
- [x] 8.2 Verify all existing tests pass: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.

## 9. Validation

- [x] 9.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj`
- [x] 9.2 Run `dotnet build Njord.slnx` to confirm no compilation errors
- [x] 9.3 Run `dotnet slopwatch` from repo root
- [x] 9.4 Run `dotnet format` whitespace check
