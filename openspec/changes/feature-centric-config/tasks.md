## 1. FeatureKind, FeatureDescriptor, FeatureCategory
- [x] 1.1 Tests first (`src/Njord.Core.Tests`), then add `src/Njord.Core/Domain/FeatureKind.cs`, `FeatureCategory.cs`, `FeatureDescriptor.cs`
- [x] 1.2 `FeatureKind` -> config key explicit-switch mapping (`FeatureKindConfigMappings`) with a spec pinning every member
- [x] 1.3 Add `Feature` descriptor to `IAlertRule<T>` (`src/Njord.Core/Domain/IAlertRule.cs`)

## 2. IDomainHandler change
- [x] 2.1 Replace `ConfigKey` and `Requirements` with `Feature` in `src/Njord.Core/Domain/IDomainHandler.cs`; fix fakes in `src/Njord.Tests.Shared`

## 3. FeatureOptions with migration
- [x] 3.1 Add `src/Njord.Core/Configuration/FeatureOptions.cs` (enum-keyed, descriptor defaults) and `FeatureOptionsValidator`; remove `EnrichmentOptions.cs`
- [x] 3.2 Legacy `Njord:Enrichments` mapper (WeatherAlerts expansion, precedence, warning) in `src/Njord` host configuration
- [x] 3.3 Register via `AddNjordOptions<FeatureOptions>`

## 4. FeatureResolution
- [x] 4.1 Add `src/Njord.Core/Domain/FeatureResolution.cs` with `AllFeatures`, `EnabledFeatures`, `RequiredParams`, `RequiredDomains`, `CostOf`; reuse `BudgetCalculator` (`src/Njord.Core/Configuration/Polling`)
- [x] 4.2 Remove `EnrichmentResolution.cs`; update callers (host, setup containers, `PollManager`)

## 5. Handlers declare Feature
- [x] 5.1 `src/Njord.Weather` lifestyle handlers
- [x] 5.2 `src/Njord.AirQuality`, `src/Njord.Flood`, `src/Njord.Marine` handlers
- [x] 5.3 Update `src/Njord.Architecture.Tests` `EnrichmentConfigKeySpec` to feature-kind uniqueness/coverage

## 6. WeatherAlertHandler rule-awareness
- [x] 6.1 Each `src/Njord.Weather/Rules/*Rule.cs` declares its feature (14)
- [x] 6.2 `WeatherAlertHandler` runs only enabled rules; composite feature/requirements; specs for single-rule disable

## 7. Config Builder redesign
- [x] 7.1 Export `docs/data/features.json` from descriptors; update `docs/.vitepress/theme/builder/types.ts`, `budget.ts` (marginal cost), `serializer.ts` (Features export, legacy import)
- [x] 7.2 Redesign `ConfigBuilder.vue`: category groups, per-feature cost, budget meter with colors

## 8. Docs and validation
- [x] 8.1 Update docs pages, `docker-compose.example.yml`, `src/Njord/appsettings.Example.json`, AGENTS.md, skill `njord-enrichment-feature`
- [x] 8.2 From `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, `dotnet run --project <Project>/<Project>.csproj` for each touched test project; builder tests; `openspec validate --all --no-interactive`
