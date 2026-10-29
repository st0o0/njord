## Why

Njord.Messages and Njord.Core grew organically over 0.x development. Before the 0.5.0 release we need a clean, navigable contract surface: 18 inter-actor messages live outside Njord.Messages, the Pipeline/ directory is a 115-type dumping ground mixing polling commands with feature results and unrelated enums, 10 types are dead code, and configuration options carry workarounds that should be resolved while breaking changes are still free.

## What Changes

- **BREAKING** Add Akka.Streams NuGet reference to Njord.Messages (for `ISinkRef<T>` / `ISourceRef<T>` in stream handshake messages)
- **BREAKING** Move 18 public inter-actor and stream-payload types from Njord.Core and Njord.Grpc into Njord.Messages (namespace changes for all consumers)
- **BREAKING** Rename `Njord.Messages.Forecast` namespace to `Njord.Messages.Fetch`
- **BREAKING** Split `Njord.Messages.Pipeline` into `Njord.Messages.Polling`, `Njord.Messages.Stream`, and `Njord.Messages.Results` (with sub-namespaces per OutputKind)
- **BREAKING** Delete `QueryBudgetUsage` and its response hierarchy (`QueryBudgetStatus` is a superset)
- **BREAKING** Delete `FetchRejectionReason.Budget` and `.Expired` enum members (never produced)
- **BREAKING** Delete `ForecastOptions.Model` migration shim
- **BREAKING** Consolidate `SolarYieldOptions.PeakKw` (SolarYieldConfig reads from SolarYieldOptions)
- **BREAKING** Replace `Effective*` pattern with `PostConfigure<T>` for nullable-with-default options (ForecastOptions.Models, PollingOptions.BudgetOverride, 15 feature Thresholds)
- Delete dead types: `BudgetStatusFailed`, `BudgetUsageFailed`, `PollStatesFailed`
- Delete unused `ToWireName` extension methods (prod uses `ToProto`)
- Un-nest PipelineEvent subtypes (8 sealed records become standalone)
- Split NonAlertResults.cs (44 types), AlertResults.cs (30 types), FeatureEnums.cs (20 enums) into per-OutputKind files with co-located enums

## Capabilities

### New Capabilities

- `message-namespace-structure`: The new Njord.Messages directory and namespace layout (Alerts, Budget, Config, Fetch, Polling, Stream, Results/{Alerts,Scores,Conditions,Trends,Forecasts})
- `post-configure-defaults`: Replace the nullable+Effective* options pattern with PostConfigure<T> that fills null arrays with defaults at startup

### Modified Capabilities

- `message-conventions`: Breaking namespace changes for all inter-actor messages; stream handshakes move into Messages
- `message-domain-separation`: Njord.Messages gains an Akka.Streams dependency for ISinkRef/ISourceRef
- `options-conventions`: ForecastOptions.Model removed, Effective* pattern replaced with PostConfigure
- `options-validation-hygiene`: Validator branch for dead Model property removed

## Impact

- **Njord.Messages**: new Akka.Streams package reference; all files reorganized into new namespace tree
- **Njord.Core**: loses 18 types from Domain/ and Actors/; Configuration/ simplified
- **Njord.Grpc**: loses GrpcStreamMessages.cs (moved to Messages)
- **All consumer projects**: updated `using` statements (Polling, Pipeline, Features, Grpc, Host, all test projects)
- **No API-budget impact**: this is a code-organization change with no polling or API call changes
- **No wire-format changes**: proto files and gRPC contracts are untouched

## Non-goals

- Redesigning the actor topology or stream architecture
- Moving persistence records (Njord.Persistence stays as-is)
- Changing FeatureInputs structure (cohesive, stays in Core)
- Adding new features or messages
- Changing wire formats (proto stays the same)
- Renaming actor classes or their Props
