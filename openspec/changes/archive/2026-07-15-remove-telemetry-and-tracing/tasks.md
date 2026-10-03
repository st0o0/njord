## 1. Remove NjordTelemetry call-sites from actors

- [x] 1.1 Remove all `NjordTelemetry.*` calls from `src/Njord/Pipeline/SchedulerActor.cs` (PollsTotal, FetchFailures, DataChanges)
- [x] 1.2 Remove all `NjordTelemetry.*` calls from `src/Njord/Pipeline/PipelineActor.cs` (Source.StartActivity, FetchTotal, FetchDuration)
- [x] 1.3 Remove all `NjordTelemetry.*` calls from `src/Njord/Mqtt/MqttConnectionActor.cs` (Source.StartActivity, MqttPublishes, MqttPublishDuration, MqttConnected, Reconnects)
- [x] 1.4 Remove all `NjordTelemetry.*` calls from `src/Njord/Mqtt/DiscoveryActor.cs` (DiscoveryPublishes)
- [x] 1.5 Remove `using Njord.Telemetry;` and `using System.Diagnostics;` (if no longer needed) from the 4 actor files

## 2. Delete NjordTelemetry class and test

- [x] 2.1 Delete `src/Njord/Telemetry/NjordTelemetry.cs`
- [x] 2.2 Delete `src/Njord.Tests/Telemetry/NjordTelemetrySpec.cs`

## 3. Strip OpenTelemetry from ServiceDefaults

- [x] 3.1 Remove OTel tracing/metrics setup from `src/Njord.ServiceDefaults/Extensions.cs` (`AddOpenTelemetry`, `WithTracing`, `WithMetrics`, `UseOtlpExporter`)
- [x] 3.2 Remove Serilog OTLP sink from `src/Njord.ServiceDefaults/Extensions.cs` (the conditional `WriteTo.OpenTelemetry` block)
- [x] 3.3 Remove OTel-related `using` directives from `Extensions.cs` (`OpenTelemetry`, `OpenTelemetry.Resources`, `OpenTelemetry.Trace`, `Serilog.Sinks.OpenTelemetry`)
- [x] 3.4 Remove the unused `version` variable if it is no longer referenced after OTel removal (it was used for `service.version` resource attribute and Serilog OTLP sink)

## 4. Remove NuGet packages

- [x] 4.1 Remove OpenTelemetry packages from `src/Njord.ServiceDefaults/Njord.ServiceDefaults.csproj`: `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `Serilog.Sinks.OpenTelemetry`
- [x] 4.2 Remove corresponding entries from `src/Directory.Packages.props` (kept `OpenTelemetry.Api` pin for Akka.Hosting transitive NU1902 suppression)

## 5. Validation

- [x] 5.1 Build: `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings
- [x] 5.2 Unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` — 498 passed, 0 failed
- [x] 5.3 Integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` — 7 passed, 0 failed
- [x] 5.4 E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` — 1 passed, 0 failed
