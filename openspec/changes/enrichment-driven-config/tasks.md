## 1. EnrichmentOptions

- [x] 1.1 Create `EnrichmentOptions` in `src/Njord.Core/Configuration/` with bool properties for all current handlers, defaults as specified
- [x] 1.2 Create `EnrichmentOptionsValidator` (at least one enrichment must be enabled)
- [x] 1.3 Register with `AddNjordOptions<EnrichmentOptions>(configuration)` in a setup container
- [x] 1.4 Add `IsEnabled(string key)` method with cached reflection dictionary
- [x] 1.5 Tests for defaults, IsEnabled, validator

## 2. IDomainHandler ConfigKey

- [x] 2.1 Add `string ConfigKey` property to `IDomainHandler` interface in `src/Njord.Core/Domain/`
- [x] 2.2 Add ConfigKey to all existing handlers: WeatherAlertHandler, AirQualityAlertHandler, FloodRiskHandler, FloodRainHandler, SwimmingConditionsHandler, VentilationWindowHandler, ExerciseAdvisoryHandler, LaundryScoreHandler, CommuteSafetyHandler, PollenTrendHandler, NightVentilationHandler, ConfidenceTrendHandler
- [x] 2.3 Architecture test: every registered IDomainHandler has a valid ConfigKey matching an EnrichmentOptions property

## 3. Domain resolution

- [x] 3.1 Create domain resolution logic: union of enabled handlers' Triggers → `IReadOnlySet<DomainKind>`
- [x] 3.2 Register as singleton in DI
- [x] 3.3 Tests for resolution (minimal defaults → 2 domains, flood added → 3, all AQ disabled → no AQ)

## 4. PollWorker + BudgetCalculator

- [x] 4.1 PollWorker takes `IReadOnlySet<DomainKind>` instead of reading EnabledDomains from options
- [x] 4.2 BudgetCalculator uses resolved domain count
- [x] 4.3 Remove `EnabledDomains` from `NjordOptions` and its validator
- [x] 4.4 Update PollWorker and Budget tests

## 5. Pipeline filtering

- [x] 5.1 Pipeline/ProjectionEngine filters handlers by `EnrichmentOptions.IsEnabled(handler.ConfigKey)`
- [x] 5.2 Test: disabled handler not called even with domain data present

## 6. Config + Docs

- [x] 6.1 Update docker-compose.example.yml: `Njord__Enrichments__*` env vars
- [x] 6.2 Update docs/configuration/ for enrichment-driven config
- [x] 6.3 Update Config Builder: enrichment toggles replace domain checkboxes
- [x] 6.4 Update AGENTS.md

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx`
- [x] 7.2 Full test suite passes
