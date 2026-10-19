## 1. Remove unused Verify dependencies

- [x] 1.1 Removed Verify.XunitV3 PackageReference from Core.Tests csproj and deleted ModuleInitializer.cs
- [x] 1.2 Removed Verify.XunitV3 PackageReference from Pipeline.Tests csproj and deleted ModuleInitializer.cs
- [x] 1.3 Removed Verify.XunitV3 PackageReference from Budget.Tests csproj and deleted ModuleInitializer.cs
- [x] 1.4 Solution builds cleanly

## 2. Remove DefaultOptionsSpec Verify golden master

- [x] 2.1 Removed Verify golden-master test and .verified.json. Kept validator-passes and override-wins tests.
- [x] 2.2 Removed Verify.XunitV3 PackageReference from Njord.Tests csproj (no remaining Verify calls; no ModuleInitializer existed)
- [x] 2.3 Njord.Tests: 183 pass (was 184, -1 Verify test)

## 3. Replace Mqtt Verify snapshots with explicit assertions

- [x] 3.1 Read StatePayloadGoldenMasterSpec.cs and all 6 .verified.txt files
- [x] 3.2 Replaced 6 Verify snapshot tests with 6 explicit JsonNode/JsonObject assertion tests verifying structure (topic format, retain flag, key presence, JSON validity). Kept 3 existing behavior tests unchanged.
- [x] 3.3 Deleted all 6 .verified.txt files
- [x] 3.4 Removed Verify.XunitV3 PackageReference from Mqtt.Tests csproj and deleted ModuleInitializer.cs
- [x] 3.5 Mqtt.Tests: 171 pass (count unchanged — 6 Verify → 6 assertion tests)

## 4. Cross-cutting verification

- [x] 4.1 `dotnet build Njord.slnx` — 0 errors, 0 warnings
- [x] 4.2 Architecture tests: 128 pass
- [x] 4.3 Verified: only Persistence.Tests and Grpc.Tests reference Verify.XunitV3
- [x] 4.4 `dotnet format Njord.slnx --verify-no-changes` — clean
- [x] 4.5 Updated AGENTS.md: 1709→1708 (Njord.Tests 184→183)
