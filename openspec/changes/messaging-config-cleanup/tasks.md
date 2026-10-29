## 1. Akka.Streams dependency and stream message moves

- [x] 1.1 Add `Akka.Streams` package reference to `src/Njord.Messages/Njord.Messages.csproj` via `dotnet add package`
- [ ] 1.2 Create `src/Njord.Messages/Stream/` directory. Move `src/Njord.Core/Actors/ForecastStreamMessages.cs` (RequestForecastSink, RequestForecastSinkResponse, RequestForecastSinkCompleted, RequestForecastSinkFailed) to `src/Njord.Messages/Stream/ForecastStreamMessages.cs`, namespace `Njord.Messages.Stream`
- [ ] 1.3 Move `src/Njord.Core/Actors/PipelineStreamMessages.cs` (RequestPipelineOutput family) to `src/Njord.Messages/Stream/PipelineStreamMessages.cs`, namespace `Njord.Messages.Stream`
- [ ] 1.4 Move `src/Njord.Grpc/GrpcStreamMessages.cs` (RequestGrpcStream family) to `src/Njord.Messages/Stream/GrpcStreamMessages.cs`, namespace `Njord.Messages.Stream`
- [ ] 1.5 Move `src/Njord.Core/Domain/DomainDataReceived.cs` to `src/Njord.Messages/Stream/DomainDataReceived.cs`, namespace `Njord.Messages.Stream`
- [ ] 1.6 Move `src/Njord.Core/Domain/PipelineEvent.cs` to `src/Njord.Messages/Stream/PipelineEvent.cs`, un-nest the 8 subtypes into top-level sealed records, namespace `Njord.Messages.Stream`
- [ ] 1.7 Move `src/Njord.Core/Domain/FeatureResult.cs` to `src/Njord.Messages/Results/FeatureResult.cs`, namespace `Njord.Messages.Results`
- [ ] 1.8 Update all `using` statements across src/ for the moved types. Fix compilation errors in Njord.Core, Njord.Pipeline, Njord.OpenMeteo, Njord.Grpc, Njord.Polling, Njord.Features, Njord/ (Host), and all test projects
- [ ] 1.9 Add ArchUnit rule in `src/Njord.Architecture.Tests/ArchitectureTests.cs`: Njord.Messages may reference Akka.Streams but not Akka.Actor, Akka.Cluster, Akka.Persistence, or Akka.Hosting

### Validation
```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 2. Move FetchResult hierarchy and rename Forecast to Fetch

- [ ] 2.1 Move `src/Njord.Core/Domain/FetchResult.cs`, `FetchSuccess.cs`, `FetchEmpty.cs`, `FetchError.cs` to `src/Njord.Messages/Fetch/`, namespace `Njord.Messages.Fetch`. Also move `PollFailureReason` if it lives in Core
- [ ] 2.2 Rename `src/Njord.Messages/Forecast/` directory to `src/Njord.Messages/Fetch/`. Update namespace from `Njord.Messages.Forecast` to `Njord.Messages.Fetch` in all files: FetchDomain.cs, FetchDomainCompleted.cs, FetchDomainFailed.cs, FetchDomainRejected.cs, FetchRejectionReason.cs, QueryDomainData.cs
- [ ] 2.3 Update all `using Njord.Messages.Forecast` to `using Njord.Messages.Fetch` across src/

### Validation
```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 3. Split Pipeline into Polling and Results

- [ ] 3.1 Create `src/Njord.Messages/Polling/` directory. Move polling messages from `src/Njord.Messages/Pipeline/`: StartPolling.cs, StopPolling.cs, TriggerImmediatePoll.cs, TriggerLocationPoll.cs, PollWorkerReport.cs, QueryPollStates.cs, QueryPollHealth.cs, PollStateEntry.cs, PollPhase.cs, PollTarget.cs, IWithLocationId.cs. Update namespace to `Njord.Messages.Polling`
- [ ] 3.2 Move `src/Njord.Messages/Pipeline/QueryLatestAnalysis.cs` to `src/Njord.Messages/Stream/QueryLatestAnalysis.cs`, namespace `Njord.Messages.Stream`
- [ ] 3.3 Create `src/Njord.Messages/Results/Alerts/AlertResults.cs` -- move alert result records from `src/Njord.Messages/Pipeline/AlertResults.cs`, namespace `Njord.Messages.Results.Alerts`. Move `src/Njord.Messages/Pipeline/TypedFeatureResults.cs` base types to `src/Njord.Messages/Results/TypedFeatureResults.cs`
- [ ] 3.4 Split `src/Njord.Messages/Pipeline/NonAlertResults.cs` into: `src/Njord.Messages/Results/Scores/ScoreResults.cs`, `src/Njord.Messages/Results/Conditions/ConditionResults.cs`, `src/Njord.Messages/Results/Trends/TrendResults.cs`, `src/Njord.Messages/Results/Forecasts/ForecastResults.cs` -- each with its OutputKind namespace
- [ ] 3.5 Split `src/Njord.Messages/Pipeline/FeatureEnums.cs` -- move each enum into the result file that uses it (e.g., UvLevel into AlertResults.cs, CommuteLevel into ConditionResults.cs)
- [ ] 3.6 Move `src/Njord.Messages/Pipeline/ActivityWindow.cs` to `src/Njord.Messages/Results/ActivityWindow.cs`, namespace `Njord.Messages.Results`
- [ ] 3.7 Delete the now-empty `src/Njord.Messages/Pipeline/` directory
- [ ] 3.8 Update all `using Njord.Messages.Pipeline` across src/ to the new namespaces. Bulk find-replace then fix compilation

### Validation
```
dotnet build src/Njord.slnx
dotnet format src/Njord.slnx --verify-no-changes
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 4. Delete dead code

- [ ] 4.1 Delete `src/Njord.Messages/Budget/QueryBudgetUsage.cs` (entire file: QueryBudgetUsage, BudgetUsageResponse, BudgetUsageResult, BudgetUsageFailed)
- [ ] 4.2 Remove `BudgetStatusFailed` from `src/Njord.Messages/Budget/QueryBudgetStatus.cs` and `BudgetStatusResponse` base type if no longer needed
- [ ] 4.3 Remove `PollStatesFailed` from `src/Njord.Messages/Polling/QueryPollStates.cs` (moved in step 3) and `PollStatesResponse` base if unneeded
- [ ] 4.4 Remove `ToWireName` method from `src/Njord.Messages/Alerts/AlertSeverityExtensions.cs` and `src/Njord.Messages/Alerts/ConfidenceLevelExtensions.cs`. Delete `src/Njord.Messages.Tests/Alerts/AlertWireNamesTests.cs` test file
- [ ] 4.5 Remove `FetchRejectionReason.Budget` and `FetchRejectionReason.Expired` enum members. Update match arms in `src/Njord.Polling/PollWorker.cs` (remove the two dead cases)
- [ ] 4.6 Delete `src/Njord.Core/Domain/FloodMarineForecastInput` if still present (was deleted earlier, verify)

### Validation
```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project src/Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj
```

## 5. Configuration cleanup

- [ ] 5.1 Delete `ForecastOptions.Model` property and its validator branch in `src/Njord.OpenMeteo/Configuration/OpenMeteoOptionsValidator.cs`
- [ ] 5.2 Implement PostConfigure for `ForecastOptions`: register `PostConfigure<ForecastOptions>` that sets `Models` to `[DefaultModel]` when null or empty. Make `Models` non-nullable `string[]` with default `[DefaultModel]`. Remove `EffectiveModels` and `PrimaryModel` properties. Update all callers (grep for `EffectiveModels` and `PrimaryModel`)
- [ ] 5.3 Implement PostConfigure for `PollingOptions`: register `PostConfigure<PollingOptions>` for `BudgetOverride`. Remove `EffectiveBudget`. Update callers
- [ ] 5.4 Implement PostConfigure for feature Thresholds: create a generic helper or loop that registers PostConfigure for each feature config type with nullable Thresholds. Remove `EffectiveThresholds` from all 15 feature configs. Update callers in each alert rule
- [ ] 5.5 Consolidate SolarYield config: remove `PeakKw` and `SystemEfficiency` from `src/Njord.Features/Conditions/SolarYield/SolarYieldConfig.cs`. Add `IOptions<SolarYieldOptions>` constructor parameter to the SolarYield rule. Update `src/Njord.Features.Tests/Conditions/SolarYield/SolarYieldTests.cs`
- [ ] 5.6 Update `src/Njord.OpenMeteo/Configuration/OpenMeteoOptionsValidator.cs` to validate `SolarYieldOptions.SystemEfficiency` is between 0 and 1

### Validation
```
dotnet build src/Njord.slnx
dotnet format src/Njord.slnx --verify-no-changes
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj
```

## 6. Final verification

- [ ] 6.1 Run all test projects sequentially, verify 0 failures
- [ ] 6.2 Run `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] 6.3 Verify no remaining `using Njord.Messages.Pipeline` or `using Njord.Messages.Forecast` anywhere in src/
- [ ] 6.4 Verify no remaining `Njord.Core.Domain.DomainDataReceived`, `Njord.Core.Domain.PipelineEvent`, or `Njord.Core.Actors.Request*` references
- [ ] 6.5 Update `AGENTS.md` to reflect the new namespace structure and the Akka.Streams dependency in Messages

### Validation
```
dotnet build src/Njord.slnx
for p in src/Njord.*Tests; do [ "$p" = src/Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```
