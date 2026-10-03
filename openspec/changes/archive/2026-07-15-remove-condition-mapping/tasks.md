## 1. Proto Cleanup

- [x] 1.1 Remove `condition` field from `GetForecastResponse` in `protos/njord/v1/forecast_service.proto`
- [x] 1.2 Remove `condition` field from `HourlyForecast` in `protos/njord/v1/forecast_service.proto`
- [x] 1.3 Remove `condition` field from `DailyForecast` in `protos/njord/v1/forecast_service.proto`

## 2. Remove WeatherConditionMapper

- [x] 2.1 Delete `src/Njord/Domain/Weather/WeatherConditionMapper.cs`
- [x] 2.2 Delete `src/Njord.Tests/Domain/Weather/WeatherConditionMapperSpec.cs`

## 3. Update gRPC Service

- [x] 3.1 Remove condition mapping logic from `ForecastGrpcService.MapResponse()` in `src/Njord/Grpc/ForecastGrpcService.cs` — remove `WeatherConditionMapper` usage, remove `Condition` assignments
- [x] 3.2 Update `ForecastGrpcServiceSpec.cs` in `src/Njord.Tests/Grpc/` — remove condition assertions

## 4. Validation

- [x] 4.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 4.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 4.3 Run slopwatch: `dotnet slopwatch` from repo root
