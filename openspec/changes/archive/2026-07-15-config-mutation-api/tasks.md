## 1. Proto Expansion

- [x] 1.1 Replace `EnrichmentConfig` in `config_service.proto` with detailed sub-messages: `ConsensusConfig` (enabled, method, trim_percent), `AlertConfig` (enabled + all thresholds), `DerivedConfig`, `TrendConfig`, `IndexConfig` (enabled + base temps), `EnergyConfig` (enabled + all params), `HistoryConfig` (enabled + retention/sample/snapshot)
- [x] 1.2 Replace the `EnrichmentConfig` field in `NjordConfig` with a new `DetailedEnrichmentConfig` message containing all the sub-messages from 1.1
- [x] 1.3 Add `BudgetProjection` message (projected_monthly_calls, monthly_limit, usage_percent, within_budget) to `NjordConfig`
- [x] 1.4 Add `ConfigResponse` message (config, budget_projection, warnings[], applied, rejection_reason)
- [x] 1.5 Add Location RPCs: `AddLocation(AddLocationRequest)`, `RemoveLocation(RemoveLocationRequest)`, `UpdateLocation(UpdateLocationRequest)` → all return `ConfigResponse`
- [x] 1.6 Add `UpdateForecastSettings(UpdateForecastSettingsRequest)` → `ConfigResponse`
- [x] 1.7 Add `UpdateEnrichmentConfig(UpdateEnrichmentConfigRequest)` → `ConfigResponse`
- [x] 1.8 Add `UpdateBudget(UpdateBudgetRequest)` → `ConfigResponse`
- [x] 1.9 Add `GetStatus(GetStatusRequest)` → `ServerStatus` with `BudgetStatus` and repeated `ModelStatus`
- [x] 1.10 Verify `dotnet build` generates stubs for all new messages and RPCs

## 2. Config Persistence Layer

- [x] 2.1 Create `ConfigPersistence` class in `src/Njord/Configuration/` — atomic write to `data/njord-config.json` (write temp + rename), read on startup
- [x] 2.2 Update `Program.cs` or service setup to add `njord-config.json` to ConfigurationBuilder with `reloadOnChange: true` after `appsettings.json`
- [x] 2.3 Create `BudgetTracker` singleton in `src/Njord/Configuration/` — counts API calls (monthly/daily), exposes `GetUsage()`, resets at day/month boundaries
- [x] 2.4 Create `BudgetValidator` static class in `src/Njord/Configuration/` — projects monthly calls from config (locations × models × api_weight × cycles_per_month), returns `BudgetProjection`
- [x] 2.5 Register `ConfigPersistence` and `BudgetTracker` as singletons in DI
- [x] 2.6 Unit tests for `ConfigPersistence` — write, read, atomic write, corrupt file fallback
- [x] 2.7 Unit tests for `BudgetValidator` — projection calculation, 80% warning, 100% reject

## 3. ConfigGrpcService Mutations

- [x] 3.1 Add mutation serialization lock (`SemaphoreSlim(1)`) to `ConfigGrpcService`
- [x] 3.2 Implement `AddLocation` — validate unique name, plausible coordinates, budget check, persist, return ConfigResponse
- [x] 3.3 Implement `RemoveLocation` — validate exists, persist, return ConfigResponse
- [x] 3.4 Implement `UpdateLocation` — validate exists, apply patch fields, budget check if models changed, persist
- [x] 3.5 Implement `UpdateForecastSettings` — apply patch fields, budget check if poll/models changed, persist, warn on parameter group changes
- [x] 3.6 Implement `UpdateEnrichmentConfig` — apply patch per sub-message, persist
- [x] 3.7 Implement `UpdateBudget` — apply override or clear, persist
- [x] 3.8 Implement `GetStatus` — aggregate version, uptime, budget usage from `BudgetTracker`, model status from `SchedulerActor` via Ask
- [x] 3.9 Update `GetConfig` mapping to include full enrichment details and budget projection
- [x] 3.10 Unit tests for all mutation RPCs — success, validation failure, budget rejection, budget warning

## 4. Config Propagation

- [x] 4.1 Verify `IOptionsMonitor<NjordOptions>.OnChange` fires when `njord-config.json` is written — the existing `StreamConfig` handler should pick it up automatically
- [x] 4.2 Update `SchedulerActor` to use `IOptionsMonitor` for poll interval and location changes (if not already reactive)
- [x] 4.3 Ensure `DiscoveryActor` reacts to horizon/model changes via `IOptionsMonitor`

## 5. Validation

- [x] 5.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 5.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 5.3 Run E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` from `src/`
- [x] 5.4 Run slopwatch: `dotnet slopwatch` from repo root
