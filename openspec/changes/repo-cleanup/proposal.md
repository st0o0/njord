## Why

The codebase has accumulated dead code, unused dependencies, orphaned test helpers, and stale files over several refactoring rounds. Cleaning them out reduces noise, shrinks the dependency footprint, and keeps the repo honest. The TODO.md is fully resolved (all 8 items done/closed) and the model-probes reference data served its one-time purpose. The docs top-nav promotes "Dev" over the Config Builder, which is backwards for end users.

## What Changes

### Remove unused NuGet packages
- `Testcontainers` from `Directory.Packages.props` (zero PackageReference anywhere)
- `Akka.Persistence.TestKit.Xunit` from 4 test csproj files (no class from this package is used; only the non-Xunit `Akka.Persistence.TestKit` is needed)
- `Newtonsoft.Json` from `Njord.Persistence.csproj` (no .cs in Persistence uses it; Akka pulls it transitively)

### Delete dead production code
- `Njord/Configuration/LogLevelMappingExtensions.cs` (never called)
- `Njord.Core/Horizon/Horizon.cs` and `Njord.Core/Horizon/HorizonKind.cs` (no production consumer; only referenced by own tests)

### Delete dead test code
- `Njord.Core.Tests/Horizon/HorizonTests.cs` (tests for removed Horizon types)
- `Njord.Tests.Shared/AlertScenario.cs` (self-referencing cluster, never used by any test)
- `Njord.Tests.Shared/ForecastPointBuilder.cs` (only used by AlertScenario)
- `SilentActor` inner class in `Njord.IntegrationTests/Grpc/GrpcChannelTests.cs` (defined, never referenced)

### Delete stale files
- `TODO.md` (all 8 items confirmed done/closed)
- `tools/model-probes/` directory (1.9 MB one-time reference data, no longer needed)

### Rename mismatched file
- `Njord.Messages/Pipeline/TriggerTargetPoll.cs` → `TriggerLocationPoll.cs` (class inside is `TriggerLocationPoll`)

### Docs navigation
- Replace "Dev" with "Builder" (linking to `/builder`) in the top nav bar
- Dev pages remain accessible via sidebar

## Non-goals

- Refactoring or restructuring any production logic
- Auditing or pruning the OpenSpec archive (CLAUDE.md rule: never delete archive)
- Pruning OpenSpec main specs (separate effort)
- Touching MIGRATION.md (Servus to Terp migration is still pending)

## Capabilities

### New Capabilities

_None. This is a pure cleanup change with no new behavior._

### Modified Capabilities

_None. No spec-level requirements change._

## Impact

- **Dependencies**: 3 fewer NuGet references (Testcontainers, Akka.Persistence.TestKit.Xunit, Newtonsoft.Json in Persistence)
- **Repo size**: ~1.9 MB smaller (model-probes removal)
- **Code**: ~200 LOC removed across production and test code
- **Docs**: Top nav reordered; no content changes
- **No API budget impact** (no polling changes)
