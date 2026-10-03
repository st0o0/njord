## 1. Kestrel Dual-Port Binding

- [x] 1.1 Add `GrpcOptions` section to `NjordOptions` in `src/Njord/Configuration/NjordOptions.cs` — `Port` (int, default 8081)
- [x] 1.2 Configure explicit Kestrel dual-port binding in `src/Njord/Program.cs` or a setup container — port 8080 `Http1`, port 8081 `Http2`, both read from config
- [x] 1.3 Update `Dockerfile` to `EXPOSE 8080 8081`
- [x] 1.4 Update `docker-compose.yml` and Aspire AppHost if applicable to map port 8081
- [x] 1.5 Unit test: verify Kestrel config renders both endpoints (integration-level via `WebApplicationFactory` or equivalent)

## 2. Proto Definitions

- [x] 2.1 Create `protos/njord/v1/forecast_service.proto` with `ForecastService` (GetLocations, GetModels, GetForecast), request/response messages, `HourlyForecast`, `DailyForecast` messages
- [x] 2.2 Add `Grpc.AspNetCore` package via `dotnet add package` to `src/Njord/Njord.csproj` and version to `src/Directory.Packages.props`
- [x] 2.3 Add `<Protobuf Include="..\..\protos\njord\v1\*.proto" GrpcServices="Server" ProtoRoot="..\..\protos" />` to `src/Njord/Njord.csproj`
- [x] 2.4 Verify `dotnet build` generates C# stubs without errors

## 3. Weather Condition Mapping

- [x] 3.1 Create `WeatherConditionMapper` static class in `src/Njord/Domain/Weather/` — maps `(int weatherCode, bool isDay)` → `string` HA condition
- [x] 3.2 Unit tests for WMO-to-HA mapping in `src/Njord.Tests/Domain/Weather/` — cover: clear day/night, partly cloudy, overcast, fog, rain light/heavy, snow, mixed, thunderstorm, unknown code

## 4. Forecast Snapshot Store

- [x] 4.1 Create `ForecastSnapshot` record and `HourlySnapshotPoint`/`DailySnapshotPoint` records in `src/Njord/Grpc/`
- [x] 4.2 Create `ForecastSnapshotStore` class in `src/Njord/Grpc/` — `ConcurrentDictionary<(string, string), ForecastSnapshot>`, `Update(location, model, snapshot)`, `TryGet(location, model)` methods
- [x] 4.3 Create `SnapshotConsumerActor` in `src/Njord/Grpc/` — subscribes to EgressActor BroadcastHub, filters `PerModelUpdate`, parses horizon JSON payloads, updates `ForecastSnapshotStore`
- [x] 4.4 Register `ForecastSnapshotStore` as singleton and `SnapshotConsumerActor` in actor system setup (`src/Njord/Configuration/NjordActorSystemSetup.cs`)
- [x] 4.5 Unit tests for `ForecastSnapshotStore` in `src/Njord.Tests/Grpc/` — update, overwrite, TryGet unknown, timestamp tracking
- [x] 4.6 Unit tests for `SnapshotConsumerActor` in `src/Njord.Tests/Grpc/` — requests source on startup, parses horizon JSON into typed snapshot

## 5. gRPC Service Implementation

- [x] 5.1 Create `ForecastGrpcService` in `src/Njord/Grpc/` implementing the generated `ForecastService.ForecastServiceBase` — inject `NjordOptions` for locations/models, `ForecastSnapshotStore` for data
- [x] 5.2 Implement `GetLocations` — return location names from config
- [x] 5.3 Implement `GetModels` — resolve models for location, return NOT_FOUND for unknown location
- [x] 5.4 Implement `GetForecast` — query snapshot store, map to proto response, return NOT_FOUND if no data
- [x] 5.5 Register gRPC service: `AddGrpc()` in DI, `MapGrpcService<ForecastGrpcService>()` in middleware
- [x] 5.6 Unit tests for `ForecastGrpcService` in `src/Njord.Tests/Grpc/` — GetLocations returns config, GetModels for valid/invalid location, GetForecast with/without snapshot

## 6. Validation

- [x] 6.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 6.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 6.3 Run E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` from `src/`
- [x] 6.4 Run slopwatch: `dotnet slopwatch` from repo root
- [x] 6.5 Docker build and manual gRPC smoke test: build image, run container, call GetLocations from a gRPC client
