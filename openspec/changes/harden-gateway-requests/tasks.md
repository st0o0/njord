## 1. Failing specs first

- [ ] 1.1 `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs`: add `Hold()`/`Release()` so specs can keep fetches in flight
- [ ] 1.2 `src/Njord.OpenMeteo.Tests/OpenMeteoGatewaySpec.cs`: specs (sealed, `[Fact(Timeout = TestTimeouts.Hosted)]`, BDD names) for 48 requests at once each answered exactly once, requests around a stream stop each answered once, a differently cased request answered in the requested spelling, a differently spelled request reaching the hub under the configured names, two concurrent requests for one target both answered, and a full waiting list answered immediately with `Transport`; record the failure messages
- [ ] 1.3 `src/Njord.Pipeline.Tests/PollSchedulerSpec.cs`: a differently spelled reply updates the configured target without adding one; a reply for an unknown location is ignored
- [ ] 1.4 `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`: forecast and analysis queries match the location regardless of case
- [ ] 1.5 `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs`: `GetForecast` with `LUCERNE`/`ICON_D2` queries the configured spelling and returns the data

## 2. Gateway: one reply per request

- [ ] 2.1 `src/Njord.OpenMeteo/OpenMeteoGateway.cs`: per-request id carried by `FetchTarget`/`FetchResult`, `Dictionary<long, PendingRequest>`, single idempotent reply helper; replies echo the requested location/model
- [ ] 2.2 Resolve location and model to the configured spelling for the `FetchTarget`
- [ ] 2.3 Waiting list (max 1024) with one outstanding offer, offer results via `PipeTo` (`OfferCompleted`/`OfferFailed`, generation-tagged); non-`Enqueued` or faulted offers answered with `Transport`; overflow answered immediately
- [ ] 2.4 Stream recovery fails only offered requests and re-offers the waiting list into the new stream
- [ ] 2.5 Gateway specs from 1.2 pass

## 3. Case-insensitive boundaries

- [ ] 3.1 `src/Njord.Pipeline/PollScheduler.cs`: resolve every reply to the configured target, warn and ignore unknown targets
- [ ] 3.2 `src/Njord.Pipeline/ForecastReadModel.cs`: key state by `LocationId`
- [ ] 3.3 `src/Njord.Grpc/WeatherGrpcService.cs`: `GetForecast` queries with the catalog's location id and model spelling
- [ ] 3.4 Specs from 1.3–1.5 pass

## 4. Validation

- [ ] 4.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings and 0 errors
- [ ] 4.2 `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj --no-build`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` and `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build`, three times each, all green
- [ ] 4.3 Full sequential loop over every `Njord.*Tests` project (AGENTS.md) green; update "Current total" in `AGENTS.md`; no `*.verified.*` changes
- [ ] 4.4 `openspec validate harden-gateway-requests --no-interactive`
