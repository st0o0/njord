## Context

After the BudgetGuard change, ForecastOptions and EnsembleOptions live in
`Njord.OpenMeteo`. The Catalog in `Njord.Grpc` still needs model identifiers, which
caused a temporary `Grpc → OpenMeteo` ProjectReference. This change removes that
reference by introducing an `ActiveModels` record in Core, following the same
`IOptions<T>` + `PostConfigure` pattern as `DomainCallConfig`.

The Catalog also has a bug: it only lists EnsembleOptions.Models, ignoring
ForecastOptions.Models. Users configuring explicit forecast models (e.g. `icon_d2`)
get the data through the pipeline but ha-njord never creates Weather entities for them.

## Goals / Non-Goals

**Goals:**
- `ActiveModels` record in Core with forecast and ensemble model lists
- Host builds it via `PostConfigure` from `ForecastOptions` + `EnsembleOptions`
- Catalog uses `ActiveModels` — lists all models, forecast primary first
- Remove `Njord.Grpc → Njord.OpenMeteo` ProjectReference
- Remove the architecture test exemption for that reference

**Non-Goals:**
- Model metadata (DisplayName, Provider, Resolution) — remains stub
- Per-location model configuration
- Changes to ha-njord

## Decisions

### D1: ActiveModels record in Njord.Core/Configuration

```csharp
public sealed record ActiveModels
{
    public const string BestMatch = "best_match";

    public string PrimaryForecastModel { get; init; } = BestMatch;
    public IReadOnlyList<string> ForecastModels { get; init; } = [];
    public IReadOnlyList<string> EnsembleModels { get; init; } = [];

    public IReadOnlyList<string> AllModels =>
        [PrimaryForecastModel, .. ForecastModels.Skip(1), .. EnsembleModels];
}
```

`PrimaryForecastModel` is the first forecast model or `"best_match"`.
`ForecastModels` is the full list from `ForecastOptions.Models` (empty when using
best_match). `EnsembleModels` is `EnsembleOptions.Models` after defaults.

`AllModels` returns the combined list in catalog order: primary first, then secondary
forecast, then ensemble. No duplicates expected (different API endpoints).

**Why `Configuration/`?** It sits alongside `NjordOptions` and `DomainCallConfig` — all
Host-built option records that other projects consume.

### D2: PostConfigure in the Host

In the domain setup container (same one that builds `DomainCallConfig`):

```csharp
services.PostConfigure<ActiveModels>((models, forecast, ensemble) =>
{
    models.PrimaryForecastModel = forecast.Models.Length > 0
        ? forecast.Models[0] : ActiveModels.BestMatch;
    models.ForecastModels = [.. forecast.Models];
    models.EnsembleModels = [.. ensemble.Models];
});
```

This runs after `EnsembleOptions.ApplyDefaults()`, so the default model is always
populated.

**Alternative: init-only record built in a factory.** Rejected because `PostConfigure`
is the established pattern (`DomainCallConfig` uses it) and avoids a separate factory
class.

### D3: ToCatalog takes ActiveModels

`CatalogMessageMappingExtensions.ToCatalog(NjordOptions, ActiveModels)` replaces the
`EnsembleOptions` parameter. It builds `CatalogLocation.Models` from `AllModels` and
creates `CatalogModel` entries for each.

`CatalogModel.ForecastDays` becomes model-type-aware: ensemble models use their days
from `DomainCallConfig` if available, forecast models use forecast days. For now, since
`ActiveModels` does not carry days, this field stays as-is (a follow-up could add it via
`DomainCallConfig`).

### D4: Remove Grpc → OpenMeteo reference

`Njord.Grpc.csproj` drops the `ProjectReference` to `Njord.OpenMeteo`. The architecture
test exemption added in the BudgetGuard change is removed. The `using
Njord.OpenMeteo.Configuration` import in `WeatherGrpcService.cs` and
`CatalogMessageMappingExtensions.cs` is replaced by `using Njord.Core.Configuration`.

## Risks / Trade-offs

- **[AllModels ordering]** ha-njord uses `models[0]` as the primary model. The ordering
  must be stable: primary forecast first. → Ensured by the `AllModels` implementation.
- **[No ForecastDays per model type in CatalogModel]** The stub metadata remains
  incomplete. → Acceptable; model metadata is a separate concern.
