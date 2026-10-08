## Why

AdminService runtime mutations (`SetEnrichment`, `SetSettings`, etc.) return `applied=true` but have no effect when configuration is set via Docker environment variables. The root cause is twofold: `ConfigPersistence` writes `NjordOptions` without the `"Njord"` section wrapper, so the persisted keys never match the bound configuration section; and even if they did, the separate `njord-config.json` concept is non-standard — .NET's configuration system already supports layered JSON overrides that solve this correctly.

Additionally, the Kestrel dual-port setup in `Program.cs` uses a verbose `ConfigureKestrel` block with an `ASPNETCORE_URLS` branch and magic port numbers, when Kestrel endpoint configuration via `appsettings.json` handles this declaratively.

## What Changes

- **Replace `ConfigPersistence` with `WritableNjordOptions`** — an `IWritableOptions<NjordOptions>` implementation that reads/modifies/writes a section-wrapped JSON override file (`data/appsettings.Override.json`) and calls `IConfigurationRoot.Reload()`, following the community-standard writable options pattern.
- **Refactor `AdminGrpcService`** to use `IWritableOptions<NjordOptions>.Update(Action<NjordOptions>)` instead of manual `CloneOptions` + field-by-field mutation + `SaveAsync`.
- **Move Kestrel endpoint config to `appsettings.json`** — declarative `Kestrel:Endpoints` section with `Http` (port 8080, Http1) and `Grpc` (port 8081, Http2). Reduce `ConfigureKestrel` to a single line setting `MinResponseDataRate = null` globally.
- **Remove `data/njord-config.json`** concept — replaced by `data/appsettings.Override.json` which follows .NET convention and is loaded as the last configuration source (highest priority, overrides env vars).
- **Remove `GrpcOptions`** — dead code, port now lives in Kestrel endpoint config.
- **Remove `ConfigPersistence`** — replaced by `WritableNjordOptions`.

## Non-goals

- Adopting an external writable-options NuGet package (the implementation is small enough to own).
- Changing the AdminService gRPC API contract (proto files unchanged).
- Altering polling behavior (no API-budget impact).

## Capabilities

### New Capabilities

- `writable-options`: The `IWritableOptions<NjordOptions>` implementation — read-modify-write pattern on `data/appsettings.Override.json` with `IConfigurationRoot.Reload()`, section-wrapped keys, thread-safe file access.

### Modified Capabilities

- `config-persistence`: Runtime config mutations now persist to `data/appsettings.Override.json` (was `data/njord-config.json`), with correct section prefix so values override env vars.
- `grpc-v2-admin-service`: `AdminGrpcService` uses `IWritableOptions<NjordOptions>.Update()` instead of `CloneOptions` + `ConfigPersistence.SaveAsync()`. Same gRPC behavior, simpler implementation.
- `kestrel-dual-port`: Kestrel endpoints move from code to `appsettings.json` declarative config. `MinResponseDataRate = null` set globally. Port override via `Kestrel__Endpoints__Grpc__Url` env var instead of custom `Njord:Grpc:Port`.

## Impact

- **Files changed:** `src/Njord/Program.cs`, `src/Njord/appsettings.json`, `src/Njord/Configuration/CoreSetupContainer.cs`, `src/Njord.Grpc/AdminGrpcService.cs`
- **Files added:** `src/Njord.Core/Configuration/WritableNjordOptions.cs` (or similar)
- **Files removed:** `src/Njord.Core/Configuration/ConfigPersistence.cs`, `src/Njord.Core/Configuration/GrpcOptions.cs`
- **Tests:** `AdminGrpcService` specs need update (new dependency), `ConfigPersistence` specs replaced by `WritableNjordOptions` specs
- **Docker:** `Dockerfile` unchanged (`EXPOSE 8080 8081` stays). `docker-compose.e2e.yml` unchanged (env vars still work as baseline, mutations now correctly override them).
- **Breaking for existing `data/njord-config.json`:** If a user has a pre-existing override file, it will be ignored. Migration: rename to `data/appsettings.Override.json` and wrap content in `{"Njord": {...}}`.
