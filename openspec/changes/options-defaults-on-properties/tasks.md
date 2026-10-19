## 1. Add property initializers

- [ ] 1.1 NjordOptions: `ForecastDays = 4`
- [ ] 1.2 PollingOptions: `PollInterval = TimeSpan.FromHours(1)`, `DiscoveryInterval = TimeSpan.FromMinutes(20)`
- [ ] 1.3 MqttOptions: `Port = 1883`, `DiscoveryPrefix = "homeassistant"`, `BaseTopic = "njord"` (Enabled/Host already have defaults)
- [ ] 1.4 HealthOptions: `PollOverdueFactor = 2.0`, `PollStalledFactor = 3.0`, `MqttUnhealthyAfter = TimeSpan.FromMinutes(2)`, `BudgetDegradedBelowPercent = 5.0`
- [ ] 1.5 SensorOptions: `StalenessSeconds = 7200` (Enabled = true is already C# default for bool? check)
- [ ] 1.6 ConsensusOptions: `Method = ConsensusMethod.Median`, `TrimPercent = 0.1`, `MinQuorum = 2` (Enabled = true)
- [ ] 1.7 AlertOptions: `HeavyRainHourlyThreshold = 10.0`, `HeavyRainDailyThreshold = 25.0`, `PressureDropThreshold = 5.0`, `PressureDropSevereThreshold = 10.0`, `FogPersistentHours = 4`, `CapeThreshold = 1000.0`, `ThunderstormPrecipThreshold = 5.0`, `ThunderstormGustThreshold = 15.0`, `IceThreshold = 2.0` (Enabled = true)
- [ ] 1.8 DerivedOptions: `Enabled = true`
- [ ] 1.9 TrendOptions: (Enabled = false is C# default, check if explicit needed)
- [ ] 1.10 IndexOptions: (Enabled = false is C# default, check if explicit needed)
- [ ] 1.11 PersistenceOptions: `Provider = PersistenceProvider.Sqlite`
- [ ] 1.12 OpenMeteoOptions: `BaseUrl = "https://api.open-meteo.com/"`
- [ ] 1.13 StorageOptions: `PersistencePath = "data/njord.db"`
- [ ] 1.14 Build solution — verify compiles

## 2. Slim down appsettings.json

- [ ] 2.1 Read current `src/Njord/appsettings.json` Njord section
- [ ] 2.2 Remove scalar values that now match property initializers — keep section structure, list values, and any values that appsettings.Development.json overrides
- [ ] 2.3 Verify `appsettings.Development.json` only has genuine dev overrides
- [ ] 2.4 Run all tests — binding still works (section paths exist, code defaults kick in)

## 3. Update conventions and arch specs

- [ ] 3.1 Update AGENTS.md: change "A new scalar option gets its default in this file, not as a property initializer" to the new convention
- [ ] 3.2 Relax the arch spec rule that blocks `new XOptions()` — allow for scalar-only options classes (identify which classes are scalar-only vs have list defaults)
- [ ] 3.3 Update ComposeExampleSpec if affected

## 4. Validation

- [ ] 4.1 `dotnet build Njord.slnx` — clean
- [ ] 4.2 Run ALL test projects (options binding affects everything)
- [ ] 4.3 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — all pass
- [ ] 4.4 `dotnet format Njord.slnx --verify-no-changes` — clean
- [ ] 4.5 Verify `dotnet run --project Njord/Njord.csproj` starts successfully with no appsettings overrides
