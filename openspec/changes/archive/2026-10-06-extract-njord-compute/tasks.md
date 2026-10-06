## 1. Create Njord.Compute Project

- [x] 1.1 Create `src/Njord.Compute/Njord.Compute.csproj` — class library, `ProjectReference` to `Njord.Domain` only, no Akka packages
- [x] 1.2 Move all 46 files from `src/Njord.Core/Analysis/` to `src/Njord.Compute/Analysis/` — keep `namespace Njord.Analysis` unchanged
- [x] 1.3 Move 5 Options POCOs from `src/Njord.Core/Configuration/` to `src/Njord.Compute/Configuration/`: `AlertOptions.cs`, `HistoryOptions.cs`, `IndexOptions.cs`, `IndexPreferences.cs`, `LocationIndexOverride.cs` — keep `namespace Njord.Configuration`
- [x] 1.4 Add `ProjectReference` to `Njord.Compute` in `src/Njord.Core/Njord.Core.csproj`
- [x] 1.5 Verify `dotnet build src/Njord.Core/Njord.Core.csproj` succeeds (validators in Core reference options types in Compute)

## 2. Create Njord.Compute.Tests Project

- [x] 2.1 Create `src/Njord.Compute.Tests/Njord.Compute.Tests.csproj` — `Exe`, `IsTestProject`, references `Njord.Compute` and `xunit.v3.mtp-v2` only (no Akka, no Tests.Shared)
- [x] 2.2 Move all 14 test files from `src/Njord.Core.Tests/Analysis/` to `src/Njord.Compute.Tests/Analysis/` — update namespace from `Njord.Core.Tests.Analysis` to `Njord.Compute.Tests.Analysis`
- [x] 2.3 Delete `src/Njord.Core.Tests/Analysis/` directory (now empty)
- [x] 2.4 Link `xunit.runner.json` in `Njord.Compute.Tests.csproj` (same pattern as other test projects)
- [x] 2.5 Verify `dotnet run --project src/Njord.Compute.Tests/Njord.Compute.Tests.csproj` — all ~174 tests pass

## 3. Update Solution

- [x] 3.1 Add `Njord.Compute/Njord.Compute.csproj` to `/Foundation/` folder in `src/Njord.slnx`
- [x] 3.2 Add `Njord.Compute.Tests/Njord.Compute.Tests.csproj` to `/Tests/` folder in `src/Njord.slnx`
- [x] 3.3 Verify `dotnet build src/Njord.slnx` succeeds

## 4. Update Architecture Tests

- [x] 4.1 Add `"Njord.Compute"` to `BaseLibraryNames` in `src/Njord.Architecture.Tests/NjordArchitecture.cs`
- [x] 4.2 Add `"Njord.Compute"` to `CoreAndBelow` array in `src/Njord.Architecture.Tests/LayerReferenceSpec.cs`
- [x] 4.3 Add new test `Compute_references_only_Domain()` in `LayerReferenceSpec.cs` — assert empty Njord references except `["Njord.Domain"]`
- [x] 4.4 Update `Core_references_only_Domain_Messages_and_Persistence()` to allow `Njord.Compute` — rename method to `Core_references_only_Domain_Messages_Persistence_and_Compute()`
- [x] 4.5 Verify `dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build` passes

## 5. Verify Full Suite

- [x] 5.1 Run `dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj --no-build` — ~167 remaining tests pass
- [x] 5.2 Run `dotnet run --project src/Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build` — ~174 tests pass
- [x] 5.3 Run all test projects sequentially to confirm no regressions

## 6. Documentation Update

- [x] 6.1 Update `AGENTS.md` solution structure — add `Njord.Compute/` and `Njord.Compute.Tests/` with descriptions
- [x] 6.2 Update `AGENTS.md` test counts — split Core count between Core and Compute
- [x] 6.3 Update `AGENTS.md` reference direction documentation to include Compute layer
- [x] 6.4 Update `CLAUDE.md` if it references the layer direction or skill routing for Core analysis

## Validation

```bash
# From src/
dotnet build Njord.slnx
dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build
# Full sequential run
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
# Slopwatch (from repo root)
dotnet slopwatch analyze -d . --fail-on warning
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
