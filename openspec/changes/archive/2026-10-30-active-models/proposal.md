## Why

The Catalog (GetCatalog RPC) currently reads `EnsembleOptions` directly, which forced a
temporary `Njord.Grpc` → `Njord.OpenMeteo` ProjectReference in the BudgetGuard change.
This breaks the "feature libs independent" architecture rule. Additionally, the Catalog
ignores `ForecastOptions.Models` entirely — if a user configures multiple deterministic
forecast models (e.g. `icon_d2`, `icon_eu`), the data flows through the pipeline but
ha-njord never creates Weather entities for them because the Catalog does not list them.

## What Changes

- **Introduce `ActiveModels`** in `Njord.Core` — a read-only record carrying the
  resolved primary forecast model, secondary forecast models, and ensemble models.
  Registered as `IOptions<ActiveModels>` via `PostConfigure` in the Host.
- **Catalog uses `ActiveModels`** instead of `EnsembleOptions` directly. The
  `ToCatalog` method builds `CatalogLocation.Models` from the full model set (forecast
  primary + forecast secondary + ensemble models).
- **Fix the ForecastOptions.Models bug** — secondary forecast models now appear in the
  Catalog and produce Weather entities in ha-njord.
- **Remove the temporary Grpc → OpenMeteo ProjectReference** — `WeatherGrpcService`
  injects `IOptions<ActiveModels>` instead of `IOptions<EnsembleOptions>`.

## Non-goals

- Model metadata (DisplayName, Provider, Resolution) — still a stub, separate concern.
- Merging GetCatalog into GetStatus — the Catalog remains a separate RPC.
- Changes to ha-njord entity creation — it already handles secondary models correctly
  once the Catalog lists them.
- Per-location model lists — models remain global (same list for all locations).

## Capabilities

### New Capabilities

- `active-models`: A read-only `ActiveModels` record in Core carrying the resolved
  forecast and ensemble model identifiers, built by the Host from ForecastOptions and
  EnsembleOptions via PostConfigure.

### Modified Capabilities

- `feature-catalog`: The Catalog includes both forecast and ensemble models in
  `CatalogLocation.Models` and `CatalogModel` entries, sourced from `ActiveModels`
  instead of `EnsembleOptions`.

## Impact

- `Njord.Core`: new `ActiveModels` record (in `Configuration/` or `Domain/`).
- `Njord.Grpc`: `WeatherGrpcService` injects `IOptions<ActiveModels>` instead of
  `IOptions<EnsembleOptions>`. `CatalogMessageMappingExtensions.ToCatalog` takes
  `ActiveModels`. The `using Njord.OpenMeteo.Configuration` import is removed.
- `Njord.Grpc.csproj`: remove `ProjectReference` to `Njord.OpenMeteo`.
- `Njord/` (Host): `PostConfigure<ActiveModels>` in the setup container, built from
  `ForecastOptions` and `EnsembleOptions`.
- `Njord.Architecture.Tests`: remove the temporary `EnsembleOptions` exemption for Grpc.
- Tests: `WeatherGrpcServiceTests` (Catalog tests), `CatalogMessageMappingTests`.
