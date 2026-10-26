## A. Plain xUnit renames (mechanical, ~80 files)

Rename `*Spec.cs` → `*Tests.cs` (file and class) for all test files that use plain xUnit (no TestKit
base class). No logic changes.

- [x] A.1 Rename plain xUnit files in `Njord.Core.Tests` (14 files)
- [x] A.2 Rename plain xUnit files in `Njord.Features.Tests` (24 files)
- [x] A.3 Rename plain xUnit files in `Njord.Forecast.Tests` (12 files)
- [x] A.4 Rename plain xUnit files in `Njord.Messages.Tests` (3 files)
- [x] A.5 Rename plain xUnit files in `Njord.Persistence.Tests` (3 files)
- [x] A.6 Rename `ArchitectureSpec` → `ArchitectureTests` in `Njord.Architecture.Tests`
- [x] A.7 Rename plain xUnit files in `Njord.Budget.Tests` (3 non-actor files)
- [x] A.8 Rename plain xUnit files in `Njord.OpenMeteo.Tests` (12 non-actor files)
- [x] A.9 Rename plain xUnit files in `Njord.Grpc.Tests` (8 non-actor files)
- [x] A.10 Rename plain xUnit files in `Njord.Pipeline.Tests` (4 non-actor files)
- [x] A.11 Rename plain xUnit files in `Njord.Polling.Tests` (10 non-actor files)
- [x] A.12 Build and run all tests to verify renames

## B. Polling actor tests (~25 files)

Dissolve `PollManagerSpecBase`, `PollWorkerSpecBase`, `PollingConnectionTestKit`. Each test class
inherits `TestKit` directly. Remove TestTimeouts references. Rename Spec→Tests.

- [x] B.1 Read PollManagerSpecBase and PollWorkerSpecBase to understand shared setup (probes, actor creation, helpers)
- [x] B.2 Rewrite `PollManagerStartupSpec`, `PollManagerQuerySpec`, `PollManagerTriggerSpec`: inline base setup, `TestKit` direct, rename to Tests
- [x] B.3 Rewrite `PollManagerCycleSpec`, `PollManagerLateRegionSpec`: NjordActorSpec→TestKit, rename
- [x] B.4 Rewrite `PollWorkerDomainScheduleSpec`, `PollWorkerCoordinatesSpec`, `PollWorkerIntervalSpec`, `PollWorkerOutcomeSpec`, `PollWorkerFailureSpec`, `PollWorkerRecoverySpec`, `PollWorkerTriggerSpec`: inline base setup, TestKit direct, rename
- [x] B.5 Rewrite `PollWorkerFetchReplySpec`: NjordActorSpec→TestKit, rename
- [x] B.6 Rewrite `PollWorkerGatewayConnectionSpec`: PollingConnectionTestKit→TestKit, rename
- [x] B.7 Rewrite `PollingClusterSpec`: NjordActorSpec→TestKit, rename
- [x] B.8 Delete `PollManagerSpecBase.cs`, `PollWorkerSpecBase.cs`, `PollingConnectionTestKit.cs`
- [x] B.9 Remove TestTimeouts references from all Polling test files
- [x] B.10 Build and run Polling tests

## C. Pipeline TestKits (~7 files)

Dissolve `SinkRefTestKit`, `PipelineConnectionTestKit`. Inline setup into each test class.

- [x] C.1 Read SinkRefTestKit and PipelineConnectionTestKit to understand shared setup
- [x] C.2 Rewrite SinkRef tests (4 files: ActorBoundary, HubChain, HubConsumer, PersistentConsumer): inline setup, TestKit direct, rename
- [x] C.3 Rewrite `PipelineSchedulerConnectionSpec`: PipelineConnectionTestKit→TestKit, rename
- [x] C.4 Rewrite `GatewayRestartSpec`, `ForecastPipelineSettingsSpec`, `ForecastPipelineInputSpec`: NjordActorSpec→TestKit, rename
- [x] C.5 Delete `SinkRefTestKit.cs`, `PipelineConnectionTestKit.cs`
- [x] C.6 Remove TestTimeouts references from Pipeline test helpers (PipelineProducer etc.)
- [x] C.7 Build and run Pipeline tests

## D. Gateway, Budget, Grpc actor tests (~8 files)

NjordActorSpec→TestKit for remaining actor tests.

- [x] D.1 Rewrite `GatewayBudgetGateSpec`, `OpenMeteoGatewayAttachSpec`, `OpenMeteoGatewayRoutingSpec`: NjordActorSpec→TestKit, rename
- [x] D.2 Rewrite `BudgetTrackerSpec`: NjordActorSpec→TestKit, rename
- [x] D.3 Rewrite `GrpcConsumerSpec`: NjordActorSpec→TestKit, rename
- [x] D.4 Build and run OpenMeteo, Budget, Grpc tests

## E. Grpc Hosting TestKit tests (~3 files)

Already on TestKit directly, just remove TestTimeouts and rename.

- [x] E.1 Update `WeatherGrpcServiceSpec`, `OpsGrpcServiceSpec`, `ActorAskSpec`: remove AddTestTimefactor, remove TestTimeouts references, rename to Tests
- [x] E.2 Build and run Grpc tests

## F. Integration tests + Shared cleanup (~10 files)

- [x] F.1 Rename IntegrationTests files: HostSmokeSpec, GrpcChannelSpec, MultiModelSpec, PersistenceRestartSpec, HandlerRequirementsSpec, PersistenceOptionsValidatorSpec → Tests
- [x] F.2 Remove TestTimeouts references from IntegrationTests (Eventually.cs helper, NjordTestHost)
- [x] F.3 Delete from `Njord.Tests.Shared`: `NjordActorSpec.cs`, `TestTimeouts.cs`, `TestAkkaConfigExtensions.cs` (TestTimeConstants kept — still used)
- [x] F.4 Build and run all tests

## G. Final validation

- [x] G.1 `dotnet build Njord.slnx` from `src/`
- [x] G.2 `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] G.3 Run all test projects from `src/`
- [x] G.4 Verify no `*Spec.cs` test files remain (only actor tests that were already renamed)
- [x] G.5 Verify no references to NjordActorSpec, TestTimeouts, AddTestTimefactor remain
