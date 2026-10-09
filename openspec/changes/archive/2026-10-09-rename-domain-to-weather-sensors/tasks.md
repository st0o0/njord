## 1. Rename feature library `Njord.Sensors` -> `Njord.SensorHub`

- [x] 1.1 `git mv src/Njord.Sensors src/Njord.SensorHub`; rename
  `Njord.Sensors.csproj` -> `Njord.SensorHub.csproj`; update its root
  namespace to `Njord.SensorHub` (or leave implicit, matching the
  `<RootNamespace>`/assembly-name convention `Njord.Core`/`Njord.Compute`
  already use).
- [x] 1.2 Update namespaces in `src/Njord.SensorHub/SensorHubActor.cs` and
  `src/Njord.SensorHub/Configuration/SensorSetupContainer.cs` from
  `Njord.Sensors`/`Njord.Sensors.Configuration` to
  `Njord.SensorHub`/`Njord.SensorHub.Configuration`.
- [x] 1.3 `git mv src/Njord.Sensors.Tests src/Njord.SensorHub.Tests`; rename
  `Njord.Sensors.Tests.csproj` -> `Njord.SensorHub.Tests.csproj`; update the
  namespace in `SensorHubActorSpec.cs` and its `using Njord.Sensors` to
  `using Njord.SensorHub`.
- [x] 1.4 Update `ProjectReference`s in `src/Njord/Njord.csproj` from
  `Njord.Sensors` to `Njord.SensorHub`.
- [x] 1.5 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` and
  `ZoneArchitectureSpec.cs` wherever they name the `Njord.Sensors` feature
  library (actor-lib list) to `Njord.SensorHub`.
- [x] 1.6 Build + run: `dotnet build Njord.slnx`,
  `dotnet run --project Njord.SensorHub.Tests/Njord.SensorHub.Tests.csproj --no-build`,
  `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`.

## 2. Split `Njord.Domain` -> `Njord.Weather` + `Njord.Sensors` (foundation)

- [x] 2.1 Create `src/Njord.Weather/Njord.Weather.csproj` (copy
  `Njord.Domain.csproj`'s settings, zero Njord `ProjectReference`s);
  `git mv src/Njord.Domain/Weather/* src/Njord.Weather/`.
- [x] 2.2 Create `src/Njord.Sensors/Njord.Sensors.csproj` (same template,
  zero Njord `ProjectReference`s — this is now the foundation library, not
  the actor lib from section 1); `git mv src/Njord.Domain/Sensors/* src/Njord.Sensors/`.
- [x] 2.3 Move `src/Njord.Domain/Options/LocationOptions.cs` to
  `src/Njord.Core/Configuration/LocationOptions.cs`, changing its namespace
  from `Njord.Domain.Options` to `Njord.Configuration` (matching
  `NjordOptions`, `MqttOptions`, etc. already in that folder). This finally
  satisfies the pre-existing `domain-core-merge` requirement that
  `Domain/Options/*` live in `Njord.Core/Configuration`.
- [x] 2.4 Delete the now-empty `src/Njord.Domain/` and
  `Njord.Domain.csproj`.
- [x] 2.5 Update every `using Njord.Domain.Weather;` -> `using Njord.Weather;`
  and `using Njord.Domain.Sensors;` -> `using Njord.Sensors;` across all
  production and test files currently referencing them (see the file list
  gathered during exploration — `Njord.Compute`, `Njord.Core`,
  `Njord.Enrichment`, `Njord.Mqtt`, `Njord.Pipeline`, `Njord.Egress`,
  `Njord.Grpc`, `Njord.Ingest`, `Njord.Messages`, `Njord.SensorHub`, and
  their `*.Tests` counterparts, plus `Njord.Tests.Shared`). Also fix the one
  remaining `using Njord.Domain.Options;` reference left by task 2.3.
- [x] 2.6 Update `ProjectReference`s from `Njord.Domain` to `Njord.Weather`
  and/or `Njord.Sensors` (as needed per project) in: `Njord.csproj`,
  `Njord.Compute.csproj`, `Njord.Core.csproj`, `Njord.Messages.csproj`, and
  any other `.csproj` found referencing `Njord.Domain`.
- [x] 2.7 Split `src/Njord.Domain.Tests/` into two new test projects:
  - [x] 2.7.1 Create `src/Njord.Weather.Tests/Njord.Weather.Tests.csproj`;
    `git mv src/Njord.Domain.Tests/Weather/* src/Njord.Weather.Tests/`
    (`CycleIdSpec.cs`, `DailyForecastPointSpec.cs`, `ForecastDataHashSpec.cs`,
    `ForecastSeriesQuerySpec.cs`, `ForecastSeriesSpec.cs`,
    `ModelForecastSpec.cs`, `ModelSnapshotSpec.cs`, `ParameterRegistrySpec.cs`,
    `ParameterRegistryTypedAccessSpec.cs`,
    `ResolvedParameterSetLookupSpec.cs`, `TimeAnchorSpec.cs`,
    `WeatherModelSpec.cs`); update namespaces/`using`s.
  - [x] 2.7.2 Create `src/Njord.Sensors.Tests/Njord.Sensors.Tests.csproj`
    (the foundation-library test project — distinct from the
    `Njord.SensorHub.Tests` created in section 1); `git mv
    src/Njord.Domain.Tests/Sensors/* src/Njord.Sensors.Tests/`
    (`SensorSnapshotSpec.cs`); update namespace/`using`s.
  - [x] 2.7.3 Delete the now-empty `src/Njord.Domain.Tests/` and
    `Njord.Domain.Tests.csproj`.
- [x] 2.8 Build + run:
  `dotnet build Njord.slnx`,
  `dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj --no-build`,
  `dotnet run --project Njord.Sensors.Tests/Njord.Sensors.Tests.csproj --no-build`,
  `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build`,
  `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build`.

## 3. Update the solution file and architecture tests

- [x] 3.1 Update `src/Njord.slnx`: remove `Njord.Domain` /
  `Njord.Domain.Tests`; add `Njord.Weather`, `Njord.Weather.Tests`,
  `Njord.Sensors`, `Njord.Sensors.Tests`, `Njord.SensorHub`,
  `Njord.SensorHub.Tests` (the last two already handled structurally in
  section 1, but confirm the `.slnx` entries match the renamed paths).
- [x] 3.2 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs`:
  `NjordArchitecture.BaseLibraryNames` drops `"Njord.Domain"`, adds
  `"Njord.Weather"` and `"Njord.Sensors"`; the reference-direction
  assertions for `Njord.Compute` and `Njord.Core` check for
  `Njord.Weather`/`Njord.Sensors` instead of `Njord.Domain`.
- [x] 3.3 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`:
  the "Domain is independent of the zones" check targets
  `Njord.Weather.*`/`Njord.Sensors.*` instead of `Njord.Domain.*`; the
  feature-library lateral-reference list uses `Njord.SensorHub` (from
  section 1).
- [x] 3.4 Build + run:
  `dotnet build Njord.slnx`,
  `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`.

## 4. Update documentation

- [x] 4.1 Update `AGENTS.md`'s solution-structure code block: replace
  `Njord.Domain/` with `Njord.Weather/` and `Njord.Sensors/` under
  Foundation; replace the feature-lib `Njord.Sensors/` entry with
  `Njord.SensorHub/`; update the matching `*.Tests/` list; update the
  "Reference direction" prose line and the `LayerReferenceSpec`/
  `ZoneArchitectureSpec` description sentence.
- [x] 4.2 Update `AGENTS.md`'s "Current total: 817 tests (...)" breakdown
  line if the per-project test counts shift due to the split (Domain's
  count splits across Weather + Sensors; Sensors renames to SensorHub).

## 5. Full verification

- [x] 5.1 `dotnet build Njord.slnx` from `src/` — zero errors, zero new
  warnings.
- [x] 5.2 Run every `Njord.*Tests` project via the sequential loop in
  AGENTS.md (skip `Njord.Tests.Shared`) — all pass, same total test count
  as before (817), just redistributed across the renamed/split projects.
- [x] 5.3 From the repo root: `dotnet tool restore` then
  `dotnet slopwatch analyze -d . --fail-on warning` — no new findings.
- [x] 5.4 From the repo root: `openspec validate --all --no-interactive`
  and `bash scripts/check-openspec-root.sh`.
- [x] 5.5 `dotnet format` whitespace check (per repo convention, run after
  slopwatch).
