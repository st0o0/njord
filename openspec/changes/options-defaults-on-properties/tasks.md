## 1. Add property initializers

- [x] 1.1 NjordOptions: `ForecastDays = 4`
- [x] 1.2 PollingOptions: `PollInterval = TimeSpan.FromHours(1)`, `DiscoveryInterval = TimeSpan.FromMinutes(20)`
- [x] 1.3 MqttOptions: `Port = 1883`, `DiscoveryPrefix = "homeassistant"`, `BaseTopic = "njord"`
- [x] 1.4 HealthOptions: `PollOverdueFactor = 2.0`, `PollStalledFactor = 3.0`, `MqttUnhealthyAfter = TimeSpan.FromMinutes(2)`, `BudgetDegradedBelowPercent = 5.0`
- [x] 1.5 SensorOptions: `Enabled = true`, `StalenessSeconds = 7200`
- [x] 1.6 ConsensusOptions: `Enabled = true`, `Method = ConsensusMethod.Median`, `TrimPercent = 0.1`, `MinQuorum = 2`
- [x] 1.7 AlertOptions: all 9 scalar thresholds + `Enabled = true`
- [x] 1.8 DerivedOptions: `Enabled = true`
- [x] 1.9 TrendOptions: `Enabled = false` (explicit for clarity)
- [x] 1.10 IndexOptions: `Enabled = false` (explicit for clarity)
- [x] 1.11 PersistenceOptions: `Provider = PersistenceProvider.Sqlite`
- [x] 1.12 OpenMeteoOptions: `BaseUrl = "https://api.open-meteo.com/"`
- [x] 1.13 StorageOptions: `PersistencePath = "data/njord.db"`
- [x] 1.14 Build solution — compiles clean

## 2. Slim down appsettings.json

- [x] 2.1 Removed entire Njord section from appsettings.json (all defaults now on properties)
- [x] 2.2 Cleaned up appsettings.Development.json: removed values that match new code defaults (PollInterval, Consensus Method/TrimPercent, Persistence Provider, Mqtt Enabled)
- [x] 2.3 Verified appsettings.Development.json only has genuine dev overrides (ForecastDays: 16, extra Horizons, Locations, Models, Trends/Indices enabled)
- [x] 2.4 All tests pass — binding works via property initializers

## 3. Update conventions and arch specs

- [x] 3.1 Updated AGENTS.md: "all option defaults live as property initializers", removed "scalar defaults in appsettings.json" convention, updated all 6 references
- [x] 3.2 Removed "no empty options constructor" arch spec rule entirely (OptionsWithShippedDefaults, EmptyOptions regex, FollowedByInitializer method, self-test, offending/compliant snippets). All options now carry their own defaults, `new XOptions()` is safe everywhere.
- [x] 3.3 Updated ExampleConfigurationSpec: replaced `The_shipped_file_carries_the_scalar_defaults_under_the_Njord_section` with `The_shipped_file_has_no_Njord_section`

## 4. Validation

- [x] 4.1 `dotnet build Njord.slnx` — clean
- [x] 4.2 All 9 key test projects pass (Core 155, Alerts 120, Pipeline 176, Mqtt 171, OpenMeteo 72, Grpc 125, Indices 85, Budget 36, SensorHub 26) + Host 183 + Architecture 128
- [x] 4.3 Architecture tests: 128 pass (rule removed, no stale references)
- [x] 4.4 `dotnet format Njord.slnx --verify-no-changes` — clean
