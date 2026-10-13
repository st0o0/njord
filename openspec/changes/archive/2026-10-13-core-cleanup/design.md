# Design: core-cleanup

## D1: Relocate ModelCoverageRegistry → Njord.Weather

Move `ModelCoverageRegistry`, `ModelCoverage`, `BoundingBox`, `CoverageTier`
from `Njord.Core/Configuration/` to `Njord.Weather/`. The types have zero
Njord.Core dependencies — they describe weather-model metadata (geographic
coverage, forecast horizon, display names). Weather sits at the bottom of the
reference chain; every consumer already sees it.

Namespace changes from `Njord.Core.Configuration` to `Njord.Weather`. All
consumers update their `using` — the compiler catches every site.

## D2: Relocate HorizonProjection → Njord.Weather

Move `HorizonProjection` from `Njord.Core/Egress/` to `Njord.Weather/`. It
operates exclusively on Weather types (`Forecast`, `ForecastPoint`,
`ResolvedParameterSet`, `ParameterDef`) and `System.Text.Json`. Both consumers
(Mqtt `StatePayloadBuilder`, Grpc `WeatherGrpcService`) already reference
Weather.

## D3: Relocate TopicSlug → Njord.Mqtt

Move `TopicSlug` from `Njord.Core/Egress/` to `Njord.Mqtt/`. Its only
production consumer is `Mqtt/TopicScheme.cs`. The class has zero Njord
dependencies (only `System.Text`). Test moves from `Njord.Core.Tests` to
`Njord.Mqtt.Tests`.

## D4: EnrichmentKind enum

Add `EnrichmentKind` enum to `Njord.Core/Egress/`:

```csharp
public enum EnrichmentKind
{
    Consensus,
    Alerts,
    Derived,
    Trends,
    Indices,
}
```

Add `abstract EnrichmentKind Kind { get; }` to the `PipelineEvent` base record.
Each analysis variant (`ConsensusComputed`, `AlertComputed`, ...) overrides it.
`ForecastReceived` is not an enrichment — it does not override; `Kind` lives
only on enrichment-specific variants. Alternative: add `Kind` only to the five
enrichment subtypes via a shared interface `IEnrichmentEvent`. Prefer the
interface — `ForecastReceived` should not carry an `EnrichmentKind`.

```csharp
public interface IEnrichmentEvent
{
    EnrichmentKind Kind { get; }
}
```

Replace all bare `"consensus"`, `"alerts"` etc. strings in `Njord.Grpc` and
`Njord.Mqtt` with `EnrichmentKind.Consensus.ToString()` or direct enum
comparisons where possible.

## D5: ConsensusMethod enum

Replace `ConsensusOptions.Method` (string) with a `ConsensusMethod` enum in
`Njord.Compute/Configuration/`:

```csharp
public enum ConsensusMethod
{
    Mean,
    Median,
    TrimmedMean,
}
```

`ConsensusOptions.Method` becomes `ConsensusMethod Method { get; set; } =
ConsensusMethod.Median`. The validator reduces to checking `Enum.IsDefined`.
The `ConsensusComputer` switches on the enum instead of string comparison.
ASP.NET Core binds enums from JSON and environment variables natively.

## D6: Complete ParameterRegistry typed properties

Add typed static properties to `Njord.Weather/ParameterRegistry.cs` for the
14 parameters that currently lack them:

**Hourly** (4): `Rain`, `SoilTemperature0cm`, `Visibility`, `WindDirection10m`

**Daily** (10): `PrecipitationSum`, `UvIndexMax`, `SnowfallSum`,
`Temperature2mMax`, `Temperature2mMin`, `WindSpeed10mMax`, `WindGusts10mMax`,
`Sunrise`, `Sunset`, `WeatherCode`

Then replace every `GetByApiName("...")` call in `Njord.Alerts/AlertEvaluator.cs`
(6 calls) and `Njord.Grpc/WeatherGrpcService.cs` (10+ calls) with the typed
properties.

`GetByApiName` stays as a public method — it serves deserialization paths where
the API name comes from JSON. But no production code should call it with a
string literal.
