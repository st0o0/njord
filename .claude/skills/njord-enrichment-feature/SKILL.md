---
name: njord-enrichment-feature
description: Use when adding or changing a feature (alert, score, condition, trend, forecast) - its FeatureRule in src/Njord.Features/<OutputKind>/<Feature>/, the FeatureDescriptor, the <Name>Config section (Njord:Features:<Category>:<Name>), registration in AddFeatures, the result contract in Njord.Messages, the gRPC mapping, docs/data/features.json and the Config Builder.
---

# njord feature

Rules (static entity set, `TimeProvider`, toggleable features) live in `AGENTS.md` - follow them. This skill is the checklist.

## Where things live

- **`Njord.Core/Domain`**: `IFeatureRule` (`Descriptor`, `Enabled`, `Execute(ring, now)`), `FeatureRule<TInput>` (base class: `Project(LocationForecast)` builds a typed input, `Evaluate(FeatureContext<TInput>)` returns one `PipelineEvent` or null), `FeatureDescriptor(Output, Category, Name, Requirements, PollDepth)`, `DomainRequirements` (per-domain `XxxParams` flags, `Optional*` flags are requested but never gate), `FeatureInputs`.
- **`Njord.Messages`**: result records per feature in a domain folder (`Alerts/`, ...), `OutputKind`, `FeatureResult` family (`AlertResult`, `ScoreResult`, `ConditionResult`, `TrendResult`, `ForecastResult`). No rule or formula.
- **`Njord.Features/<OutputKind>/<Feature>/`**: the rule, its `<Name>Config` and an optional `<Name>ConfigValidator` side by side (`Alerts/ Scores/ Conditions/ Trends/ Forecasts/`; shared helpers in `<Kind>/Shared/` and top-level `Shared/`).
- **`Njord.Pipeline/Stream/FeatureEngine`** runs the enabled rules whose domain got new data and whose required fields are present.

## Checklist

1. **Config** - `<Name>Config : IConfigSection` with `const string SectionName = "Njord:Features:<Category>:<Name>"`, `bool Enabled` and the options as property initializers (arrays: nullable property plus `Effective*`). Cross-field rules go in `<Name>ConfigValidator : IValidateOptions<<Name>Config>`.
2. **Result type** - record in `src/Njord.Messages/<Feature>/` (alerts) or one of the existing `ScoreResult`, `ConditionResult`, `TrendResult`, `ForecastResult` shapes; add the proto message and the `EnrichmentEvent.feature` oneof case in `protos/njord/v2/weather.proto` (extend-only, use the field range of the output kind).
3. **Rule** - `public sealed class <Name> : FeatureRule<TInput>` taking `IOptions<<Name>Config>`; `Descriptor`, `Enabled => config.Value.Enabled`, `Project`, `Evaluate`. Use `TimeProvider` through `ctx.Now`, never the clock directly.
4. **Register** in `src/Njord.Features/ServiceCollectionExtensions.cs` (`AddFeatures`): `services.AddNjordOptions<XConfig>()` (or `<XConfig, XConfigValidator>`) and `services.AddSingleton<IFeatureRule, X>()` in the block of its output kind.
5. **gRPC** - `ToProto()` mapping in `src/Njord.Grpc/Mapping/` and the event switch (`ToEnrichmentEvent`).
6. **Docs and examples** - hand-maintained entry in `docs/data/features.json` (key `<Category>:<Name>`, default, domains, `options` with names and defaults), the Config Builder budget/serializer if the feature needs special handling (`docs/.vitepress/theme/builder`), `docs/configuration/index.md` feature table, `src/Njord/appsettings.Example.json` and `docker-compose.example.yml` if the options are worth showing.
7. **Tests** - `src/Njord.Features.Tests/<OutputKind>/`: plain `[Fact]`s with a hand-built forecast, one per rule branch (see `Alerts/WeatherAlertsSpec.cs`). Behavioral only, no data-only specs. Enum-to-wire conversions get a spec pinning every member.

## Worked example - an alert (from `FrostAlert`)

```csharp
// src/Njord.Features/Alerts/Frost/FrostAlert.cs
public sealed class FrostAlert(IOptions<FrostConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert, FeatureCategory.Weather,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx) { /* classify, return FrostAlertResult or null */ }
}

// src/Njord.Features/Alerts/Frost/FrostConfig.cs
public sealed class FrostConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Weather:Frost";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [0, -5, -15];
}
```

## Pitfalls

- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- A rule whose required domain data is missing is skipped by the engine, not failed; return null from `Evaluate` when there is nothing to report.
- Feature names are unique within a category; the config key is `Category:Name`.
- Feature libraries never reference each other; share only through Core/Messages types.
- Every new feature changes the polled domains and thus the request budget: check `BudgetGuard` and the Config Builder cost.
