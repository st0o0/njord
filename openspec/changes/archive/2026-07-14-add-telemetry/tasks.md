## 1. Project scaffold and packages

- [x] 1.1 Create `src/Njord.ServiceDefaults/` class library project targeting `net9.0`, add to `src/Njord.slnx`, add project references from `src/Njord/Njord.csproj` and `src/Njord.AppHost/Njord.AppHost.csproj`
- [x] 1.2 Add NuGet packages to `src/Directory.Packages.props` and relevant csproj files: `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console`, `Serilog.Sinks.OpenTelemetry`, `Akka.Logger.Serilog`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`

## 2. NjordTelemetry static class

- [x] 2.1 Create `src/Njord/Telemetry/NjordTelemetry.cs` — static class with `ActivitySource("Njord")`, `Meter("Njord")`, and all instrument definitions: counters (`njord.polls.total`, `njord.fetch.total`, `njord.fetch.failures`, `njord.mqtt.publishes`, `njord.mqtt.discovery`, `njord.data.changes`, `njord.mqtt.reconnects`), histograms (`njord.fetch.duration`, `njord.mqtt.publish.duration`), UpDownCounter (`njord.mqtt.connected`)
- [x] 2.2 Write `src/Njord.Tests/Telemetry/NjordTelemetrySpec.cs` — verify all instrument names are unique, ActivitySource and Meter have name `"Njord"`, instruments are non-null

## 3. ServiceDefaults wiring

- [x] 3.1 Create `src/Njord.ServiceDefaults/Extensions.cs` with `AddNjordTelemetry(this IHostApplicationBuilder)` — configure Serilog (console sink human-readable + OTel sink conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`), enrichers (machine name, thread id, span/trace IDs), OTel tracing (`AddSource("Njord")`, `AddHttpClientInstrumentation()`, `AddAspNetCoreInstrumentation()`), OTel metrics (`AddMeter("Njord")`), OTLP exporter (conditional), resource attributes (`service.name=njord`, `service.version`)
- [x] 3.2 Wire Serilog bootstrap in `src/Njord/Program.cs` — `UseSerilog()` on the host builder, call `AddNjordTelemetry()`
- [x] 3.3 Configure Akka.Logger.Serilog in `src/Njord/Configuration/NjordActorSystemSetup.cs` — `builder.WithLoggers(setup => setup.ClearLoggers().AddLoggerFactory())`
- [x] 3.4 Update `src/Njord.AppHost/Program.cs` to reference ServiceDefaults for Aspire Dashboard integration

## 4. Health state and health checks

- [x] 4.1 Create `src/Njord/Health/NjordHealthState.cs` — singleton with thread-safe fields: `IsMqttConnected`, `MqttConnectedSince`, `MqttDisconnectedSince`, `LastSuccessfulPollUtc`, `ServiceStartedUtc`
- [x] 4.2 Create `src/Njord/Health/MqttConnectionHealthCheck.cs` — `IHealthCheck` that reads `NjordHealthState`, returns Healthy/Degraded/Unhealthy based on disconnect duration (threshold: 2 minutes), uses `TimeProvider`
- [x] 4.3 Create `src/Njord/Health/PipelineHealthCheck.cs` — `IHealthCheck` that reads `NjordHealthState`, returns Healthy/Degraded/Unhealthy based on time since last poll vs configured interval (2× degraded, 3× unhealthy), startup grace period, uses `TimeProvider`
- [x] 4.4 Add `AddNjordHealthChecks(this IHostApplicationBuilder)` to `src/Njord.ServiceDefaults/Extensions.cs` — register `NjordHealthState` singleton, both health checks
- [x] 4.5 Update `src/Njord/Configuration/NjordApplicationSetup.cs` — map `/healthz` (all checks, 503 on unhealthy) and `/alive` (liveness, always 200)
- [x] 4.6 Register `NjordHealthState` in `src/Njord/Configuration/NjordServiceSetup.cs`
- [x] 4.7 Write `src/Njord.Tests/Health/MqttConnectionHealthCheckSpec.cs` — test Healthy (connected), Degraded (disconnected <2 min), Unhealthy (disconnected ≥2 min), using fake `TimeProvider`
- [x] 4.8 Write `src/Njord.Tests/Health/PipelineHealthCheckSpec.cs` — test Healthy (recent poll), Degraded (2–3× interval), Unhealthy (>3× interval), startup grace period, using fake `TimeProvider`

## 5. Pipeline instrumentation

- [x] 5.1 Instrument `src/Njord/Pipeline/PipelineActor.cs` — in the `SelectAsyncUnordered` lambda: start `njord.fetch` Activity with `location` and `model` tags, record `FetchTotal`, `FetchDuration`, set span status on failure
- [x] 5.2 Instrument `src/Njord/Pipeline/SchedulerActor.cs` — increment `PollsTotal` in `OnScheduledPoll()`, `DataChanges` in `OnHashResult()` (on change), `FetchFailures` in `OnFetchFailed()` with `reason` tag
- [x] 5.3 Instrument `src/Njord/Mqtt/MqttConnectionActor.cs` — in the `SelectAsync` lambda: start `njord.mqtt.publish` Activity, record `MqttPublishes` and `MqttPublishDuration`; in `OnConnectedAsync()`: `MqttConnected.Add(1)` + write `NjordHealthState`; on disconnect: `MqttConnected.Add(-1)`, `Reconnects.Add(1)` + write `NjordHealthState`
- [x] 5.4 Instrument `src/Njord/Mqtt/DiscoveryActor.cs` — increment `DiscoveryPublishes` in `PublishDiscovery()` by number of config messages sent
- [x] 5.5 Update `src/Njord/Pipeline/SchedulerActor.cs` — write `NjordHealthState.LastSuccessfulPollUtc` on successful `OnHashResult()`

## 6. Validation

- [x] 6.1 Run all unit tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 6.2 Run integration tests: `dotnet run --project src/Njord.Tests.Integration/Njord.Tests.Integration.csproj`
- [x] 6.3 Build solution: `dotnet build src/Njord.slnx`
- [x] 6.4 Run `dotnet slopwatch` from repo root
