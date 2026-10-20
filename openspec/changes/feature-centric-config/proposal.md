## Why

Config is technology-centric: operators enable "enrichments" (`WeatherAlerts`, `PollenAlert`, `FloodRisk`) without knowing which API calls each one costs, and `WeatherAlerts` bundles 14 alert rules into one switch. Users want "Frost alerts", not "the WeatherAlerts enrichment", and budget transparency prevents accidental free-tier exhaustion. The Config Builder should be the primary configuration tool and show the price of every feature.

## What Changes

- Add `FeatureKind` (enum, one member per feature, no magic strings), `FeatureCategory` (WeatherAlerts, AirQuality, Lifestyle, Marine, Flood, Solar, Seasonal) and `FeatureDescriptor` (kind, category, description, default, required and optional `DomainRequirements`) to `Njord.Core`.
- **BREAKING**: `IDomainHandler.ConfigKey` (string) and `IDomainHandler.Requirements` are replaced by `IDomainHandler.Feature`.
- **BREAKING**: `EnrichmentOptions` (`Njord:Enrichments`) is replaced by `FeatureOptions` (`Njord:Features`), one toggle per `FeatureKind`, defaults taken from the descriptors. In code the enum is always used; only the configuration layer maps a `FeatureKind` to its key (`Njord:Features:FrostAlert=true`) through an explicit mapping with a spec pinning every member.
- **BREAKING**: the `WeatherAlerts` toggle is split into one toggle per alert rule (14 features, e.g. `FrostAlert`, `StormAlert`). Each `IAlertRule` carries its own `FeatureDescriptor`; `WeatherAlertHandler` stays one handler and runs only the rules whose feature is enabled.
- `FeatureResolution` replaces `EnrichmentResolution` and additionally exposes all registered features, enabled features, resolved required params/domains and a per-feature `FeatureCost`, backed by `BudgetCalculator`.
- Migration: legacy `Njord:Enrichments:*` keys are still read. `WeatherAlerts` expands to all 14 alert features; every other legacy key maps 1:1 to the feature of the same name. An explicit `Njord:Features` value wins over a legacy value. A startup warning names every legacy key found.
- Config Builder redesign: features grouped by category, each row shows description, required domains and marginal cost ("+N calls/month", "+0" when the domain is already active), plus a live budget meter (green below 50 %, yellow 50-80 %, red above 80 % of the 300k/month tier).
- Docs, `docker-compose.example.yml`, `appsettings.Example.json` and AGENTS.md move to `Njord:Features`.

## Capabilities

### New Capabilities
- `feature-catalog`: `FeatureKind`/descriptors and categories, handler-declared features, `FeatureOptions` with legacy migration, `FeatureResolution` with per-feature cost, rule-aware weather alerts.

### Modified Capabilities
- `config-builder`: feature-grouped toggles with per-feature cost and budget meter instead of enrichment toggles.
- `domain-requirements`: requirements are declared through a handler's `FeatureDescriptor` (required and optional) rather than a bare property.

## Vision (future, enabled by this design, not part of this change)

- **Multi-model weather entities.** The ensemble API accepts a `models` parameter with 20+ models (ECMWF IFS, DWD ICON, GFS, ...). njord could fetch several models in parallel and expose each as its own data stream; ha-njord would map each to a Home Assistant weather entity (e.g. `weather.njord_ecmwf_berlin`, `weather.njord_icon_berlin`). A `ModelComparison` feature (a new `FeatureKind`) could request the extra models, and its cost would show up in the Config Builder like any other feature. Out of scope here.
- **Budget sensor.** ha-njord should expose `sensor.njord_api_budget` with used and remaining API calls, making budget a first-class, user-visible value. Out of scope here; the per-feature cost model from `FeatureResolution` is the foundation.

## Non-goals

- Changing the polling architecture, discovery or per-domain timers.
- Changing the gRPC or MQTT wire format; alert types and events are unchanged.
- Per-rule threshold customization in the builder (`Njord:Enrichment:Alerts:*` stays as is, separate feature).
- Changing which parameters Open-Meteo is asked for (weather variables stay internal, `WeatherVariables`).
- Multi-model fetching, ha-njord model weather entities and the `sensor.njord_api_budget` sensor (see Vision).

## API-budget impact

No change to actual requests. With the same set of enabled features the fetched domains and call counts are identical (domains = union of required domains of enabled features; weather weight `ceil(variables / 10)` is unchanged). Splitting `WeatherAlerts` can only lower usage: disabling every Weather-only feature no longer forces Weather on if nothing else needs it, and disabling individual alerts changes no request. Startup validation against 80 % of 300k/month stays. Per-feature cost is reported from the same `BudgetCalculator` formula (locations x domains x polls/month x call weight), so the Config Builder number matches the startup check. Example at 1 location, 60 min polling (~720 polls/month): Weather = 4 x 720 = 2,880 calls, each additional single-weight domain = 720 calls.

## Impact

- `src/Njord.Core`: new `Domain/FeatureKind.cs`, `Domain/FeatureDescriptor.cs`, `Domain/FeatureCategory.cs`, `Domain/FeatureResolution.cs`, `Configuration/FeatureOptions.cs`; removed `EnrichmentOptions.cs`, `EnrichmentResolution.cs`; changed `Domain/IDomainHandler.cs`, `Domain/IAlertRule.cs`.
- Handler projects `Njord.Weather`, `Njord.AirQuality`, `Njord.Flood`, `Njord.Marine`: `Feature` property on every handler; all `Rules/*Rule.cs` declare features.
- `src/Njord` (host): options registration, legacy migration, startup warning.
- `src/Njord.Architecture.Tests`: `EnrichmentConfigKeySpec` becomes a feature-kind spec.
- `docs/.vitepress/theme/builder/*`, `docs/data/enrichment.json`, `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, AGENTS.md, skill `njord-enrichment-feature`.
- ha-njord is unaffected (wire format unchanged).
