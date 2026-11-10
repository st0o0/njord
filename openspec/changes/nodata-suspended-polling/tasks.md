## 1. Enum additions (Messages + Persistence)

- [x] 1.1 Add `NoData` member to `PollFailureReason` in `src/Njord.Messages/Fetch/PollFailureReason.cs`
- [x] 1.2 Add `Suspended` member to `PollPhase` in `src/Njord.Messages/Polling/PollPhase.cs`
- [x] 1.3 Add `Suspended = 2` to `PersistedPollPhase` in `src/Njord.Persistence/Events/Polling/PersistedPollPhase.cs`
- [x] 1.4 Add `NoData` with explicit int value to `PersistedDomainPollOutcome` in `src/Njord.Persistence/Events/Polling/PersistedDomainPollOutcome.cs`
- [x] 1.5 Update wire-format tests in `src/Njord.Persistence.Tests/WireFormatTests.cs`: assert `PersistedPollPhase.Suspended == 2`, member count == 3; assert `PersistedDomainPollOutcome.NoData` value and updated member count

## 2. Gateway FetchEmpty classification

- [x] 2.1 Add `{ Result: FetchEmpty }` case to `ReplySink` in `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs` (before the `_` fallback), mapping to `PollFailureReason.NoData`
- [x] 2.2 Update the existing `A_client_without_data_is_answered_with_malformed_payload_and_nothing_reaches_the_pipeline` test in `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayRoutingTests.cs`: rename to reflect NoData, assert `PollFailureReason.NoData` instead of `MalformedPayload`

## 3. PollWorker NoData handling

- [x] 3.1 Add `static readonly TimeSpan SuspendedRecheckInterval = TimeSpan.FromHours(24)` to `PollWorkerState` in `src/Njord.Polling/PollWorkerState.cs`
- [x] 3.2 Add NoData case to `ToDomainPolled` in `src/Njord.Polling/PollWorker.cs`: `FetchDomainFailed { Reason: PollFailureReason.NoData }` => `PersistedDomainPollOutcome.NoData`, delay = 24h, consecutiveFailures = 0
- [x] 3.3 Add NoData case to `LogDomainOutcome` in `src/Njord.Polling/PollWorker.cs`: log at Info level with location/domain
- [x] 3.4 Add NoData case to `ChooseOutcome` in `src/Njord.Polling/PollWorker.cs`: map `PersistedDomainPollOutcome.NoData` to a suitable `PollWorkerOutcome`

## 4. PollWorkerState NoData/Suspended logic

- [x] 4.1 Add `NoData` case to `Apply(DomainPolled)` in `src/Njord.Polling/PollWorkerStateExtensions.cs`: set `Phase = PollPhase.Suspended`, `NextPollAt = utc + delay`, `ConsecutiveFailures = 0`
- [x] 4.2 Verify `DueDomainsAt` handles Suspended domains correctly (NextPollAt-based, should work without changes)

## 5. Persistence mapping

- [x] 5.1 Update `PollWorkerPersistenceMappingExtensions` in `src/Njord.Polling/PollWorkerPersistenceMappingExtensions.cs`: add `Suspended` to the `PollPhase <-> PersistedPollPhase` mappings (both directions)

## 6. Tests

- [x] 6.1 Add `PollWorkerState.Apply` tests for NoData outcome in `src/Njord.Polling.Tests/PollWorkerStateTests.cs`: assert phase becomes Suspended, NextPollAt = now + 24h, ConsecutiveFailures stays 0
- [x] 6.2 Add `PollWorkerState.Apply` test for recovery from Suspended: a Changed outcome on a Suspended domain resets to Discovery
- [x] 6.3 Add NoData backoff test in `src/Njord.Polling.Tests/PollWorkerFailureTests.cs`: NoData failure results in 24h delay, no failure-count increment
- [x] 6.4 Add persistence roundtrip test for Suspended state in `src/Njord.Persistence.Tests/PollWorkerRoundtripTests.cs`

## 7. Validation

- [x] 7.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 7.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 7.3 Run all test projects sequentially from `src/`:
  - `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`
  - `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`
  - `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`
  - `dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj`
  - `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`
  - `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`
  - `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`
  - `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`
  - `dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj`
  - `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj`
  - `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
