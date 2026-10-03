## 1. Production Code: Configurable OpenMeteoBaseUrl

- [x] 1.1 Add `OpenMeteoBaseUrl` property to `NjordOptions` (`src/Njord/Configuration/NjordOptions.cs`) with default `"https://api.open-meteo.com/"`. Update `IngestServiceCollectionExtensions.AddOpenMeteoIngest` (`src/Njord/Ingest/IngestServiceCollectionExtensions.cs`) to read the base URL from resolved `NjordOptions` via `IServiceProvider` instead of hardcoding it.
- [x] 1.2 Add unit test in `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` verifying that when `OpenMeteoBaseUrl` is configured to a custom value, the `HttpClient`'s base address reflects it.

## 2. Production Code: TriggerPoll gRPC RPC

- [x] 2.1 Add `TriggerPoll` RPC, `TriggerPollRequest`, and `TriggerPollResponse` messages to `protos/njord/v1/config_service.proto`. Fields: `string location`, `string model` on request; `int32 triggered_count`, `repeated string targets` on response.
- [x] 2.2 Add `TriggerImmediatePoll` record message to `SchedulerActor` (`src/Njord/Pipeline/SchedulerActor.cs`). Handle it by resolving matching location/model pairs from config and scheduling immediate `ScheduledPoll` messages. Return a `TriggerPollResult(int Count, List<string> Targets)` response to the sender.
- [x] 2.3 Implement `TriggerPoll` in `ConfigGrpcService` (`src/Njord/Grpc/ConfigGrpcService.cs`): resolve the `SchedulerActor` via `IActorRegistry`, send `TriggerImmediatePoll` via Ask, map the result to `TriggerPollResponse`.
- [x] 2.4 Add unit tests in `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs` for `TriggerImmediatePoll`: all locations/models, specific location, specific location+model, unknown location returns zero.

## 3. AppHost: Repurpose for Tests

- [x] 3.1 Modify `src/Njord.AppHost/Program.cs`: remove MQTT Explorer container, add WireMock container (`wiremock/wiremock:latest`) with HTTP endpoint, inject WireMock endpoint as `Njord__OpenMeteoBaseUrl` into the Njord project, add `.WaitFor(wireMock)`.
- [x] 3.2 Update `src/Njord.AppHost/Njord.AppHost.csproj`: remove PostgreSQL hosting package if no longer needed, or keep behind config flag. Ensure all package versions are in `src/Directory.Packages.props`.

## 4. Test Infrastructure: Aspire Shared Fixture

- [x] 4.1 Add `Aspire.Hosting.Testing` package to `src/Directory.Packages.props`. Add `WireMock.Net` (client-only, not Testcontainers variant) if not already present.
- [x] 4.2 Create shared fixture class in `src/Njord.Tests.Shared/NjordAppHostFixture.cs`: implements `IAsyncLifetime`, creates `DistributedApplication` from `Njord.AppHost` via `DistributedApplicationTestingBuilder.CreateAsync<Projects.Njord_AppHost>()`. Exposes: WireMock admin API URL, MQTT `MqttOptions` (host/port from Mosquitto endpoint), gRPC `GrpcChannel` to Njord.
- [x] 4.3 Add AppHost project reference to `src/Njord.Tests.Shared/Njord.Tests.Shared.csproj` (or to each test project that uses the fixture, depending on where the fixture lives).

## 5. Migrate Integration Tests

- [x] 5.1 Update `src/Njord.Tests.Integration/Njord.Tests.Integration.csproj`: replace `Testcontainers` and `WireMock.Net.Testcontainers` dependencies with `Aspire.Hosting.Testing`. Add project reference to `Njord.AppHost`.
- [x] 5.2 Rewrite `src/Njord.Tests.Integration/Ingest/OpenMeteoClientIntegrationSpec.cs`: use the shared Aspire fixture's WireMock admin API and a manually constructed `OpenMeteoClient` pointing to the fixture's WireMock URL. Keep the same test scenarios (successful fetch, URL construction, 429, 400, icon_d2 null tail).
- [x] 5.3 Rewrite `src/Njord.Tests.Integration/Mqtt/MqttEgressIntegrationSpec.cs`: use the shared Aspire fixture's Mosquitto endpoint. Keep the same test scenarios (full egress round-trip). Remove any `NJORD_DOCKER_TESTS` environment gate.

## 6. Migrate E2E Tests to Black-Box

- [x] 6.1 Update `src/Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj`: replace `Testcontainers` and `WireMock.Net.Testcontainers` with `Aspire.Hosting.Testing`. Add project reference to `Njord.AppHost`.
- [x] 6.2 Rewrite `src/Njord.Tests.Integration.E2E/EndToEndPipelineSpec.cs` as black-box test: use the shared Aspire fixture, load WireMock fixtures via admin API, call `TriggerPoll` via gRPC, subscribe to MQTT and collect retained messages, assert on discovery configs + horizon state payloads + availability topic. No in-process pipeline assembly.

## 7. Cleanup

- [x] 7.1 Remove `Testcontainers` and `WireMock.Net.Testcontainers` package references from `src/Directory.Packages.props` if no project uses them anymore.
- [x] 7.2 Keep `OpenMeteoSmokeSpec` (`src/Njord.Tests.Integration/Ingest/OpenMeteoSmokeSpec.cs`) as-is — it uses a real `HttpClient` against `api.open-meteo.com` and doesn't need containers.

## 8. Validation

- [x] 8.1 Run `dotnet build Njord.slnx` from `src/` and verify all projects compile.
- [x] 8.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` — unit + actor tests pass (no changes expected but verify no regressions).
- [x] 8.3 Run `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` — integration tests pass against Aspire-managed containers.
- [x] 8.4 Run `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` — E2E black-box test passes: poll triggered via gRPC, MQTT retained messages validated.
