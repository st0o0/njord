## 1. Messages: FetchDomainResponse implements IWithLocationId

- [x] 1.1 Add `IWithLocationId` to the abstract `FetchDomainResponse` in `src/Njord.Messages/Forecast/FetchDomain.cs`. The subtypes (`FetchDomainCompleted`, `FetchDomainFailed`, `FetchDomainRejected`) already carry `Location`; the base class gets `abstract Location Location` and `string LocationName => Location.Name`. Add a test in `src/Njord.Polling.Tests/` verifying the `ShardMessageExtractor` extracts the correct entity id from each subtype (Theory over three message types).

## 2. FetchTimeouts simplification

- [x] 2.1 Remove `Http` and `Reply` from `src/Njord.Core/Polling/FetchTimeouts.cs`, keeping only `Admission`. Update the `Default` constant. Update every consumer (13 files): `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`, `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs`, `src/Njord.Polling/PollWorker.cs`, `src/Njord.Polling/Configuration/PollingSetupContainer.cs`, `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayRoutingSpec.cs`, `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachSpec.cs`, `src/Njord.Pipeline.Tests/Stream/GatewayRestartSpec.cs`, `src/Njord.Polling.Tests/PollWorkerSpecBase.cs`, `src/Njord.Polling.Tests/PollWorkerFetchReplySpec.cs`, `src/Njord.Polling.Tests/PollWorkerTriggerSpec.cs`, `src/Njord.Polling.Tests/PollWorkerGatewayConnectionSpec.cs`, `src/Njord.Polling.Tests/PollingClusterSpec.cs`. Build to verify no remaining references.

## 3. PollWorker: Tell instead of Ask

- [x] 3.1 In `src/Njord.Polling/PollWorker.cs`, replace `gateway.Ask<FetchDomainResponse>(FetchDomain, timeout).PipeTo(Self, ...)` with `gateway.Tell(FetchDomain)`. Start a per-domain reply timer (`StartSingleTimer($"fetch-reply-{domain}", new FetchReplyTimeout(domain), timeout)`) when sending. The timeout is 90 s (admission 60 s + HTTP resilience 30 s). Add `Receive<FetchDomainResponse>` that cancels the timer and feeds the result into the existing `OnDomainFetchResult` handler. Add `Receive<FetchReplyTimeout>` that calls `OnDomainFetchResult` with a `FetchDomainFailed(Transport)`. Remove the `DomainFetchReplied` wrapper and the `AskFailure` helper if they become unused.

- [x] 3.2 Update PollWorker tests. In `src/Njord.Polling.Tests/PollWorkerFetchReplySpec.cs`: the gateway probe receives `FetchDomain` via `ExpectMsg`, then replies directly to the shard region probe (or the worker under test). Add a timeout test: send `FetchDomain`, do not reply, assert the worker processes a transport failure after the timer fires. Update `src/Njord.Polling.Tests/PollWorkerSpecBase.cs`, `src/Njord.Polling.Tests/PollWorkerTriggerSpec.cs`, `src/Njord.Polling.Tests/PollWorkerGatewayConnectionSpec.cs` and `src/Njord.Polling.Tests/PollingClusterSpec.cs` for the Tell-based contract (no more Ask assertions).

- [x] 3.3 Build and run `src/Njord.Polling.Tests/Njord.Polling.Tests.csproj` to verify all PollWorker tests pass. Commit.

## 4. Gateway rewrite

- [x] 4.1 Rewrite `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`. Constructor resolves `IPollRegion` and `IBudgetTracker` synchronously via `Context.GetActor<T>()`. PreStart resolves `IForecastPipeline` synchronously, watches it, asks for SinkRef. `OnSinkCompleted` materializes the stream as one inline expression: `Source.Queue<FetchDomain>(64, DropNew)` → `.Select(gate check lambda)` → `.SelectAsyncUnordered(3, fetch lambda with CTS token)` → `.AlsoTo(Flow.Select(toReply).To(Sink.ActorRef<FetchDomainResponse>(region, streamEndMarker)))` → `.Where(success)` → `.SelectMany(toDomainDataReceived)` → `.Buffer(16, DropHead)` → `.WithAttributes(StreamSupervision.LoggingDecider)` → `.To(sinkRef.Sink)` → `.Run(materializer)`. Remove: `_pending`, `PendingDomainRequest`, `DomainFetchDone`, `DomainFetchFailed`, `DomainBranchOffered`, `PipelineResolved`, `AttachFailed`, `ResolveAsync`, `_forecastQueue`, `BranchEnded`, `NjordOptions` constructor param. Keep: `Offered` handler for queue-full rejection (rejects to `_pollRegion`), `RequestForecastSinkFailed`/`Terminated` handlers that throw. PostStop cancels `_stopped` CTS only, no `_requestQueue.Complete()`. Remove `IOptions<NjordOptions>` from `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` gateway registration.

- [x] 4.2 Remove the `lock` from `src/Njord.OpenMeteo/Budget/GatewayBudgetGate.cs` (the synchronous `Select` stage serializes access; no concurrent callers).

## 5. Gateway tests

- [x] 5.1 Rewrite `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayRoutingSpec.cs`: register `IPollRegion` probe and `IForecastPipeline` (via `src/Njord.OpenMeteo.Tests/GatewayTestActors.cs`). Send `FetchDomain` to gateway via Tell. Expect reply on the `IPollRegion` probe (not on `Self`). Test: success reply with hash, failure reply, budget rejection, unknown domain rejection, queue-full rejection before SinkRef handshake. Update `src/Njord.OpenMeteo.Tests/GatewayTestActors.cs` as needed.

- [x] 5.2 Rewrite `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachSpec.cs`: test that when the pipeline actor terminates, the gateway crashes (supervision restarts), re-resolves the pipeline, re-attaches, and the next fetch succeeds. Uses Tell-based contract.

- [x] 5.3 Update `src/Njord.Pipeline.Tests/Stream/GatewayRestartSpec.cs`: replace Ask-based `FetchUntilCompletedAsync` with Tell-based approach. Register `IPollRegion` probe, expect reply there instead of via Ask return.

- [x] 5.4 Verify `src/Njord.OpenMeteo.Tests/GatewayBudgetGateSpec.cs` still passes unchanged (gate API is unchanged, only the lock is removed).

## 6. Verify and document

- [x] 6.1 Build, format and run all test suites (see Validation). Commit.

- [x] 6.2 Update `AGENTS.md` gateway contract description: replies route via the poll-worker shard region (not to the original sender), PollWorker uses Tell with a reply-timeout timer, pipeline SinkRef is the stream's main sink.

## Validation

All commands from `src/`:

```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```
