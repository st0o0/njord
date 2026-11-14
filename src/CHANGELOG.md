# Changelog

## [0.5.0](https://github.com/st0o0/njord/compare/v0.4.1...v0.5.0) (2026-11-14)


### ⚠ BREAKING CHANGES

* replace ManifestBuilder+EntityIdLookup with EntityRegistry, domain-typed consumer hub
* remove v2 gRPC and make v3 the unversioned standard
* replace njord IActorRegistration with Terp.Akka.Hosting
* upgrade Terp 0.1.1 to 0.2.0 (SetupChain API)
* replace Servus with Terp (packages, setups, NjordHost split)
* add WeatherNext as third ensemble model, expand spread variables, add wind_speed_100m
* correct GFS ensemble model ID, add marine wave_peak_period and raise max forecast days to 16
* replace ModelComparison with passive ForecastModelComparison and EnsembleModelComparison
* align Akka bootstrap and telemetry with FunkArr patterns
* remove Satellite domain, replace SatelliteOptions with SolarOptions
* dissolve SolarYieldOptions, merge SolarBatteryStrategy into SolarYield, make alert thresholds configurable, add WindowHours
* split WeatherOptions into ForecastOptions + EnsembleOptions, remove FeatureRequirementsOptions and FeatureResolution
* update config files, docs, e2e, tests for flat feature paths and Njord:Weather
* rewrite AddFeatures with AddFeature helper, implement IHasArrayDefaults, flatten Config Builder keys
* flatten feature config paths, rename ForecastOptions to WeatherOptions, add IHasArrayDefaults
* delete dead messages, replace Effective* with PostConfigure, remove Model shim
* rename Forecast/ to Fetch/, split Pipeline/ into Polling/Stream/Results per OutputKind
* move stream, result and feature-rule types to Njord.Messages, add Akka.Streams dep
* merge Njord.Forecast into Njord.Core, invert Core/Messages dependency
* rename Weather domain to Forecast across code, proto, config, and docs
* split WeatherClient into ForecastClient and EnsembleClient
* rewrite all tests to FunkArr style (Tests suffix, no shared bases, no TestTimeouts)
* remove WeatherResponseConverter and use dictionary-based deserialization
* replace FetchPlan/ServiceInfo with FeatureRequirementsOptions via OptionsBuilder
* rewrite gateway to stream-only with shard-routed replies
* introduce FetchPlan and remove ResolvedRequirements
* multi-model weather comparison
* rewrite gateway with thick clients (FunkArr pattern)
* update docs, config examples and builder for the feature system
* move actor plumbing to owners and MappingExtensions naming
* FunkArr-level options owned by their libraries
* remove legacy domain handler and alert option system
* typed feature results and proto oneof on the wire
* replace domain handlers with FeatureRule and FeatureEngine
* reorganize handler projects by OutputKind pattern
* replace FeatureKind/AlertType/LifestyleKind with OutputKind and feature names
* delete legacy Enrichments migration, breaking change is fine
* replace ConfigKey and EnrichmentOptions with FeatureDescriptor and FeatureOptions
* remove Njord.Mqtt project
* use resolved requirements in BudgetCalculator, remove WeatherVariables
* per-domain options, thin clients, enrichment-driven variables
* replace Triggers with DomainRequirements on IDomainHandler
* make point fields nullable, add HasFields validation
* rename Njord.Model to Njord.Forecast
* remove geocoding and climate change APIs
* PollWorker per-domain independent timers, eliminate round concept
* rewrite PollWorker with typed events, no object, no PersistAll
* rewrite PollWorker with single per-domain state layer
* simplify config - remove Models, Horizons, Parameters, internalize weather variables
* replace EnabledDomains with enrichment-driven config
* rewrite OpenMeteo with typed per-domain clients and response models
* replace enrichment framework with simple IDomainHandler pattern
* remove legacy consensus, derived, trends, indices, and forecast projects
* update gRPC and MQTT consumers for domain-based pipeline events
* redesign pipeline with projection groups and domain-based enrichment
* redesign poll worker from per-model to per-location with ensemble support
* remove sensor hub and indoor sensor input
* remove runtime config management system
* immortal pipeline hubs - eliminate StreamConsumerActor, resume-all supervision, one-shot consumers
* split Njord.Weather into Njord.Model and Njord.Forecast
* split polling, Open-Meteo and storage options into their own sections and register options through one helper
* publish explicit nulls for absent alert timing values and soil_frozen as a boolean in MQTT payloads
* type the gRPC closed sets as enums and make SetEnrichment atomic with structured results
* replace the poll scheduler with a poll manager and persistent per-target poll workers

### Features

* add [Flags] param enums per domain ([68849f4](https://github.com/st0o0/njord/commit/68849f42ecc090e7eae8353013261bf369097809))
* add 20 API variables and ShowerAlert, SoilMoisture, Evapotranspiration, SmogRisk, RagweedPollen, GranularAqi, OceanCurrents ([22d4192](https://github.com/st0o0/njord/commit/22d41924375d990b299d15d2270c18d03f808936))
* add 20 feature config validators, move domain options validators to Core ([ad3c62f](https://github.com/st0o0/njord/commit/ad3c62f5d37e74adf57ac38c7e580fcda3daa7e3))
* add 21 bulk observation features across 4 domains ([109e860](https://github.com/st0o0/njord/commit/109e860a2a8dfe42053b7db7c7fdf2e9fda2d7b3))
* add air quality and flood domains with alerts and cross-domain enrichments ([1f09686](https://github.com/st0o0/njord/commit/1f09686f73e7a837ff914c92589693f46d191c5a))
* add climate anomaly enrichment with 30-year baseline comparison ([e5aae36](https://github.com/st0o0/njord/commit/e5aae36b1b8c3e32687caea0d461dd00bd495da7))
* add CompoundHeatStress alert and AllergySeverity score ([feeaf55](https://github.com/st0o0/njord/commit/feeaf55f470127462ae7b15e6bfb333f680d7e07))
* add core domain model for multi-domain proxy architecture ([022b10f](https://github.com/st0o0/njord/commit/022b10fdfb7b2af95c830732b4ededb20e107f5f))
* add cost-based Throttle to gateway stream and fix MaxBurst floor ([da6527b](https://github.com/st0o0/njord/commit/da6527baba3a5296312dc2864ea21510a67c443a))
* add ensemble support to flood API ([420bb0c](https://github.com/st0o0/njord/commit/420bb0c30436835f219258f149c62f5591c88404))
* add EntityMetadata, OutputKind.Weather/Observation, WeatherResult, ObservationResult, fix naming conventions ([2366ac1](https://github.com/st0o0/njord/commit/2366ac1b2f8f916e1ecb9ee030f2c633c85a95f2))
* add FeatureKind, FeatureCategory and FeatureDescriptor ([a21b1ff](https://github.com/st0o0/njord/commit/a21b1fff964d90036dc8eb0e13b1acc9b8df4f44))
* add FireRisk, PollenBlast, CoastalFlood, GrowingDegree and enhance ThunderstormAlert ([b703e45](https://github.com/st0o0/njord/commit/b703e45eb09572075350e67575eb726a3b7c3ca0))
* add geocoding auto-resolve for location coordinates ([e518f02](https://github.com/st0o0/njord/commit/e518f0237680dea5af796e28829b1283be3f6324))
* add GTI, snow depth, wet bulb and spread variables from Ensemble API ([036b6c5](https://github.com/st0o0/njord/commit/036b6c50d965d07862d5e6f9f2e7a6b77b3454dc))
* add HeatPumpCop and SolarBatteryStrategy conditions ([a3a8b3b](https://github.com/st0o0/njord/commit/a3a8b3b2573dee9fc17a27b51f2ef948339626e2))
* add InversionDetection, AtmosphericStability and CloudCeiling conditions ([05d389b](https://github.com/st0o0/njord/commit/05d389b4fefcb4a9f17597c1adbf14da1d133789))
* add IrrigationNeed, FrostDepth and SprayWindow features ([d169640](https://github.com/st0o0/njord/commit/d1696403692ae7380cb584f744b8ebd07981aeb1))
* add IsDay filter to activity scores, support past_hours in ForecastClient, add pipeline architecture doc ([2f86a4d](https://github.com/st0o0/njord/commit/2f86a4d28de5dc4a04c9bf40c4f24fd215274a61))
* add lifestyle enrichments (ventilation, exercise, laundry, commute, pollen trend, night ventilation, confidence) ([09ac7ce](https://github.com/st0o0/njord/commit/09ac7ce8386bd1ca93b6dae2df3c98e762f7500c))
* add marine domain with swimming conditions ([a9070b6](https://github.com/st0o0/njord/commit/a9070b63cc0b7858f57e43fe25c333493d37d87f))
* add MigraineRisk, SleepQuality and AsthmaRisk features ([cac1663](https://github.com/st0o0/njord/commit/cac166347a3dc492763215b3b1cb8e8e26384d72))
* add nitrogen_dioxide and european_aqi proto fields to AirQualitySample ([467b90c](https://github.com/st0o0/njord/commit/467b90c256fdbd4e5fe06cc043219b5c7eea8ef6))
* add optional tier to DomainRequirements ([78a58a9](https://github.com/st0o0/njord/commit/78a58a9d4029500d429228403bcee9bb23cabd6a))
* add ParamFieldNames mapping, CatalogDomainSchema, and catalog-driven domain schema building ([8dbdc3f](https://github.com/st0o0/njord/commit/8dbdc3f51cbcd2207fe39287034353ea1d534e38))
* add RainProbability, WetBulbDanger, ClearSkyUv, SnowDepthWatch features ([731a9dd](https://github.com/st0o0/njord/commit/731a9dd4d04ef0d246dd8be1d2364937d4f5fd43))
* add remaining API params (Weather+7, AQ+8, Marine+7) and ElevatedWind sensors ([1ba341f](https://github.com/st0o0/njord/commit/1ba341ff32227f53a28f7247940c82bb358f22f2))
* add satellite radiation domain with GTI blending ([b63cd9c](https://github.com/st0o0/njord/commit/b63cd9cc535d007b4840405c4f152a5f32f25b63))
* add Seasonal domain and SeasonalOutlook feature ([2ab4f74](https://github.com/st0o0/njord/commit/2ab4f74fca1ba135ad24a0bcbb3344d4fe980395))
* add solar radiation variables to ensemble weather data ([4bffdaf](https://github.com/st0o0/njord/commit/4bffdafa3c37fd325692ea2420e6e3f25bac11d3))
* add the poll manager that orchestrates the poll workers ([4ab1d08](https://github.com/st0o0/njord/commit/4ab1d08a5ee9a99528ebe253f7b3497c5dbb3e20))
* add thunderstorm_probability, precipitation_type, surface_temperature, snowfall_water_equivalent to Forecast ([8561fd1](https://github.com/st0o0/njord/commit/8561fd1197e51eabcadfe4741395800ebb1793b7))
* add tier-2 enrichments (solar yield, gardening, energy, outdoor, bbq, flood timeline, front detection) ([5986aad](https://github.com/st0o0/njord/commit/5986aade33e5f6c20f80adf9fd232085dbcb7df1))
* add TimeWindow scoring with sliding-window algorithm and gRPC mapping ([44d2eaa](https://github.com/st0o0/njord/commit/44d2eaae679f29f40686532245929fc214bf7033))
* add v3 GrpcConsumer entity state, availability tracking, NjordGrpcService, per-OutputKind mapping ([33d258c](https://github.com/st0o0/njord/commit/33d258cbabc7c665dd9f27885d6331179fe69cdd))
* add v3 proto, WeatherRule, Observation features, ManifestBuilder ([a13cd35](https://github.com/st0o0/njord/commit/a13cd35c5b7ca2a19edc6d5fa3f69a33ab528ee4))
* add WeatherNext as third ensemble model, expand spread variables, add wind_speed_100m ([9516a9c](https://github.com/st0o0/njord/commit/9516a9cb6c804a66107ab9cb47024377c6c96bd9))
* add WireTap on gateway reply sink and KeepAlive + IdleTimeout on gRPC client streams ([90e7f6a](https://github.com/st0o0/njord/commit/90e7f6a8342253909f06013904299e832cb5a715))
* classify empty API responses as NoData with 24h suspended recheck ([a274eb1](https://github.com/st0o0/njord/commit/a274eb10b2ce519f0ecb0a974848dd9ed2af14e2))
* Clean up unused imports ([c929900](https://github.com/st0o0/njord/commit/c929900fa418b0f74cdf69dd100a12538d747f0e))
* expand air quality with CO, SO2, dust, ammonia, olive pollen, US AQI, UV ([f520941](https://github.com/st0o0/njord/commit/f5209417adbf7e920f84a6dbbbc80be82542c0c1))
* expand air quality with region filter, dust alert and new variables ([4471778](https://github.com/st0o0/njord/commit/447177815300e8528f373fc8de81879c8d702204))
* expand marine with swell, wind-wave and sea level variables ([db4dcee](https://github.com/st0o0/njord/commit/db4dcee0084a077e0054cd210c832ed5c4081013))
* expand seasonal API with 10 monthly vars, weekly EFI/SOT tier, explicit model, raise max to 7 ([d1f06a1](https://github.com/st0o0/njord/commit/d1f06a190ff4d7a26c37d6c06b76bb83080dc96e))
* expose all new variables over gRPC (16 forecast, 12 ensemble spread, 1 marine proto fields) ([fd391c8](https://github.com/st0o0/njord/commit/fd391c8654ad5b3350ce05e3649ea62c53189821))
* fix observation entity routing in EntityIdLookup via FeatureName ([2532523](https://github.com/st0o0/njord/commit/25325233ada9aca234f03d95be1469add1b4e2b9))
* fractional CallWeight, DomainCallConfig, per-domain BudgetGuard, move Forecast/Ensemble/Flood options to OpenMeteo ([20b29c5](https://github.com/st0o0/njord/commit/20b29c5c6398e319fcc84908217d7b42a04e4d12))
* introduce ActiveModels in Core, fix Catalog to include ForecastOptions.Models, remove Grpc→OpenMeteo reference ([e5552d4](https://github.com/st0o0/njord/commit/e5552d4a3f997d1a66c49b023b49d0c23be30501))
* multi-model weather comparison ([b77f836](https://github.com/st0o0/njord/commit/b77f83682266625727f7af345b98ce0d0d14e270))
* per-domain cycle discovery and independent poll scheduling ([bf9843e](https://github.com/st0o0/njord/commit/bf9843e5e022288971a06ab8bb7262d06bcc0724))
* per-model weather entities and extended weather attributes ([5160ddb](https://github.com/st0o0/njord/commit/5160ddbf580a06f5e3a42c77e5016658c599b689))
* publish explicit nulls for absent alert timing values and soil_frozen as a boolean in MQTT payloads ([68a712e](https://github.com/st0o0/njord/commit/68a712e463f260129f3e8280fa6e024f49a768a7))
* replay cached state to new gRPC stream subscribers ([4143e3f](https://github.com/st0o0/njord/commit/4143e3f364304e3e09e338ad8f6b9fb25971e578))
* stream validation guards handlers against missing data ([37044ba](https://github.com/st0o0/njord/commit/37044ba4f4e1da5b83b6512d22af24cc3f50914e))
* **telemetry:** add HTTP metrics handler, pipeline throughput, sensor and config write counters ([fa072ee](https://github.com/st0o0/njord/commit/fa072ee7618898e8a2c80421e5d813f81693918b))
* type the gRPC closed sets as enums and make SetEnrichment atomic with structured results ([1da4e90](https://github.com/st0o0/njord/commit/1da4e9025ecd43fe1a97a5f622c55b5cf59bcf52))
* WeatherAlertHandler filters rules by individual feature toggles ([4b1b054](https://github.com/st0o0/njord/commit/4b1b0541509e221158aeb3701e175033b2e9e90b))
* wire air quality and flood alerts through gRPC and MQTT consumers ([985ef73](https://github.com/st0o0/njord/commit/985ef736df492a298f9543fa1242634fb062fa93))
* wire air quality and flood alerts to gRPC and MQTT consumers ([0a69f51](https://github.com/st0o0/njord/commit/0a69f51282ec7fbf1fb54f1d470302c954983629))
* wire lifestyle enrichment events to gRPC and MQTT consumers ([403b2d0](https://github.com/st0o0/njord/commit/403b2d04c6d8055d7425fe5c5e9e5e7cbd5c0645))


### Bug Fixes

* add 7 missing config validators and harden ThresholdValidation ([cecaac7](https://github.com/st0o0/njord/commit/cecaac78ff69e1c0f3f490db979676ca225dd315))
* add explicit coordinates to E2E compose, fix enrichment listing in GetStatus ([ecd8f60](https://github.com/st0o0/njord/commit/ecd8f60aae3dfffc2a9fa3a9898bd8906ea1459a))
* bound MQTT broker exchanges to 10 s and republish state after a reconnect ([9073145](https://github.com/st0o0/njord/commit/907314554e39ce35cf6d0ef1085e9e297cd92a36))
* correct AtmosphericStability classification and improve Grpc mutation coverage ([18880f2](https://github.com/st0o0/njord/commit/18880f2947cfe6a31114b670e6461347ef1628d7))
* correct budget projection for 3 domain calls per location ([6719a1d](https://github.com/st0o0/njord/commit/6719a1dad33b602f8d45149a637ac7453aa9fa14))
* correct FrostDepth degree-hours, BestWindow crash, Evapotranspiration window and SolarYield scaling ([e60e87c](https://github.com/st0o0/njord/commit/e60e87cd9a990e6ec642c74be71a5dd56a5fbf38))
* correct GFS ensemble model ID, add marine wave_peak_period and raise max forecast days to 16 ([79fcc52](https://github.com/st0o0/njord/commit/79fcc526ced6b0dfb41f7c421d5e9241134e6648))
* do not restart poll workers that the shard region stops during shutdown ([8bd3f4b](https://github.com/st0o0/njord/commit/8bd3f4bf8b9312ea40b92f97451dac203a4bd2cf))
* e2e findings (base params, failure backoff, seasonal data, resolved locations) ([106f42d](https://github.com/st0o0/njord/commit/106f42d30a6ce85fbd2f0aeed3190fefd376dc68))
* exclude code coverage tracker from architecture tests ([e7c7e3c](https://github.com/st0o0/njord/commit/e7c7e3c7d61fedb3bbd6db00894ee279e9eae8a8))
* filter v3-only result types from v2 enrichment stream ([d9eb7bc](https://github.com/st0o0/njord/commit/d9eb7bc07a2405e7ff56e5b6e6e15cad182d12f0))
* GrpcConsumer reconnect on stream completion, add model comparison tests ([aae1753](https://github.com/st0o0/njord/commit/aae17535adf3479884b2a4ffc1e905a438171310))
* Ice alert uses WeatherCode for freezing rain detection, Snow thresholds configurable ([cffe06b](https://github.com/st0o0/njord/commit/cffe06bcce3c591266583b6333fe5d5c497fa1d6))
* implement forecast/trend value mappings, self-gate model comparison features, initial stable for confidence trend ([eb76b46](https://github.com/st0o0/njord/commit/eb76b461ca4e9c5cd26a0bb081f6983aaf856459))
* include ensemble spread names in SplitByModel to prevent spurious weather entities ([f09a29e](https://github.com/st0o0/njord/commit/f09a29e2bcbbae87a72a63da4ed977e9f89f937a))
* InversionDetection always emits result, add BatteryCapacityKwh to E2E config ([5a466f3](https://github.com/st0o0/njord/commit/5a466f347c7953c98d2e2e9f62aa0afaa2728a7d))
* narrow gitignore results/ pattern that excluded Njord.Messages/Results/ ([beae0aa](https://github.com/st0o0/njord/commit/beae0aadf283a4d129ca00285271cefeaae0da4b))
* passivate PollWorker through its shard ([7c5309f](https://github.com/st0o0/njord/commit/7c5309f392a165a1df64295648d391104d23e3c1))
* **polling:** persist location coordinates in poll worker records ([2acad00](https://github.com/st0o0/njord/commit/2acad005695dbde5ea386ee24fc9da66aa8a3c8f))
* populate condition extra map and update E2E test plan ([c0907a4](https://github.com/st0o0/njord/commit/c0907a4029db9385845e5472a00ab1463f83a11e))
* register the startup AnalysisSettings the forecast pipeline is created with ([fd97c6a](https://github.com/st0o0/njord/commit/fd97c6a491734ab47286164ae44d6bc09441e368))
* remove redundant parameterless constructor from DomainRequirements ([ee84a13](https://github.com/st0o0/njord/commit/ee84a131d72caefd19fe833dcd2eeb418b626b6c))
* remove seasonal monthly absolute vars that return HTTP 400, make model optional ([432b3f6](https://github.com/st0o0/njord/commit/432b3f62928ca8173c77afa74ae5f58784248b1e))
* replace ILogger in stream closures with telemetry counter ([ac750d6](https://github.com/st0o0/njord/commit/ac750d6c6d46b6eb71e9284e22064bb3c96e8fd0))
* report feature config names in active enrichments ([93eb33c](https://github.com/st0o0/njord/commit/93eb33c28dedc860812affd22d27e50c1754fd0f))
* review medium-priority cleanup (telemetry, snapshot msg, validator, materializer, dead config) ([23a68e6](https://github.com/st0o0/njord/commit/23a68e6c00af94ac857b1653b7d25b6ceab9f228))
* **rules:** Simplify boolean expressions ([4b69ad6](https://github.com/st0o0/njord/commit/4b69ad69f17b9256b2ee70af813ddc624d8721f2))
* soil moisture unit to percent, remove UV device class, update E2E condition specs ([6eead2e](https://github.com/st0o0/njord/commit/6eead2e985e82860b7e036993cc3d6a7114ab1d4))
* stabilize StreamConsumerActorSpec flaky test and run dotnet format ([ac65353](https://github.com/st0o0/njord/commit/ac653535c6a9877766dd3378b957ed29642b49c8))
* **test:** move gRPC stream call inside Eventually retry loop ([7ce9dec](https://github.com/st0o0/njord/commit/7ce9dec4d934ef3dcf99f5c2fe79c085e7da7712))
* thread-safe budget gate, gateway telemetry and cancellation ([214109b](https://github.com/st0o0/njord/commit/214109be3a5cb78d2d3a6c391a2c1e0175e0d659))
* Thunderstorm peak-based severity with CIN downgrade, FireRisk daytime-only scoring ([0efc006](https://github.com/st0o0/njord/commit/0efc0063a177ef4da91f879cfc389ac63339858a))
* update architecture test paths and commit pending changes ([b210e4f](https://github.com/st0o0/njord/commit/b210e4f59ee1c67707893789d91f4a081bf6e06d))
* use correct Ensemble API URL, request model and spread variables for StdDev ([2d2eff2](https://github.com/st0o0/njord/commit/2d2eff2fa820519edb88722280ae11473fed10c2))
* use dynamic Akka remoting port in integration tests ([4436c04](https://github.com/st0o0/njord/commit/4436c044fd8cca00b5d3f72d836dd607d9de61a0))
* use PrecipitationSpread and WindSpeedSpread for domain-specific ensemble confidence ([ab567ef](https://github.com/st0o0/njord/commit/ab567ef95c493171a0ebd7c5929413a0d47bab96))
* v2 enrichment mapping skips v3-only result types gracefully ([97a3027](https://github.com/st0o0/njord/commit/97a3027cafb4a6857f651b48f62a96cf98bafdd6))
* wrap Pipeline and GrpcConsumer singletons with BackoffOnFailure ([eff3380](https://github.com/st0o0/njord/commit/eff338083a63725c359585ea5a09143004d97b15))


### Performance

* add collection capacity hints in ConsensusBuilder and ForecastPipeline ([4f74f02](https://github.com/st0o0/njord/commit/4f74f02fa555bcd4f34cdcaa8b7abfbd64db8392))


### Documentation

* archive config-pattern-cleanup, sync specs to main ([50c8a39](https://github.com/st0o0/njord/commit/50c8a39845903dbf09fe274c122ea9b6ff80924c))
* document Njord:Features in appsettings example ([fe004b9](https://github.com/st0o0/njord/commit/fe004b999fa30023d112083dc528e51a73168d3f))
* fix flat feature config paths, correct domain counts, add RainProbability to defaults ([10f26eb](https://github.com/st0o0/njord/commit/10f26ebb3ed17d83289cfde7f67789299c625ea1))
* remove em dashes and simplify wording ([6e84b9e](https://github.com/st0o0/njord/commit/6e84b9eba254e745e5f56f503986f77995007308))
* update config, docs, and AGENTS.md for restructured forecast ([af26f76](https://github.com/st0o0/njord/commit/af26f76fc8ef432bcda9196f0866dd0a1717a476))
* update docs, config examples and builder for the feature system ([08ce6f7](https://github.com/st0o0/njord/commit/08ce6f75012a81184abe72f46576dd03fd647524))
* update docs, configs and E2E for passive model comparison ([1fefec3](https://github.com/st0o0/njord/commit/1fefec3dd3c83825c27724907b8a5b4122f39237))
* update PollWorker logger category in example settings ([7d3e9a0](https://github.com/st0o0/njord/commit/7d3e9a0ca5be5fe01760bc133607c395c849a4dd))
* update seasonal-outlook change for post-restructuring architecture ([0d9ef62](https://github.com/st0o0/njord/commit/0d9ef629fbda0687b9d21211ef270c7192ed9286))


### Refactoring

* add NjordOptions.ResolvedLocations, eliminate GetValueOrDefault conversions ([dfce4ea](https://github.com/st0o0/njord/commit/dfce4ea31537593d5db0623ccd172e0c9c86d602))
* add OpenMeteoRequestBuilder, eliminate URL string interpolation ([b2d6a76](https://github.com/st0o0/njord/commit/b2d6a765e68fdc55b08aaf1021d25bac0ef92404))
* align Akka bootstrap and telemetry with FunkArr patterns ([ff3dce5](https://github.com/st0o0/njord/commit/ff3dce5db831af25ec90c860eb4dfa67978fcd6f))
* align logging with FunkArr conventions ([104860f](https://github.com/st0o0/njord/commit/104860fb650d52e6cf52f1e4d5ba106695404c8f))
* align setup containers with FunkArr pattern ([1939a0a](https://github.com/st0o0/njord/commit/1939a0a9f426edf9752ec273164a4da7ec742d3e))
* build MQTT state payloads from typed records and serializer converters instead of JsonObject ([5f6291a](https://github.com/st0o0/njord/commit/5f6291abf964a1aae249dc98f9ac0eccf1b7850f))
* consolidate alert abstractions in Core, eliminate Settings types, unify DI pattern ([4963109](https://github.com/st0o0/njord/commit/4963109e630c7c7c18bf5af09437a649c4db4de4))
* consolidate AlertWindow into shared ForecastWindow helper ([d3b293c](https://github.com/st0o0/njord/commit/d3b293cc2277a2e129de3cc7142954d064fba5d8))
* consolidate PollWorker tests from 13 files to 6, remove ~570 lines of boilerplate ([1726ca7](https://github.com/st0o0/njord/commit/1726ca7ecaac2cb119c441e511d485605be9c834))
* convert AirQualityAlertHandler to IMultiFeatureHandler pattern ([1107736](https://github.com/st0o0/njord/commit/110773668a537179a5d6459267861220bc183413))
* declare each weather parameter once as a typed field and move parameter selection and MQTT keys to their owners ([ede966d](https://github.com/st0o0/njord/commit/ede966dfa0aa7fb008b461e6c8f8063772c64c4a))
* delete dead messages, replace Effective* with PostConfigure, remove Model shim ([a2940c4](https://github.com/st0o0/njord/commit/a2940c4e294c6fe37f97eaada68820f6bc3079c1))
* delete legacy Enrichments migration, breaking change is fine ([934a5fc](https://github.com/st0o0/njord/commit/934a5fc7ef2abf243db146a7ddd3efed04743542))
* delete Njord.Tests host test project and clean up references ([bb2a59f](https://github.com/st0o0/njord/commit/bb2a59fc8d7393ddfd1740bff52e3ce1db9b0054))
* dissolve SolarYieldOptions, merge SolarBatteryStrategy into SolarYield, make alert thresholds configurable, add WindowHours ([57b8fd4](https://github.com/st0o0/njord/commit/57b8fd4170e70a4c4daee6b56730cc154d0a0127))
* drop unused IConfiguration parameter from AddFeatures ([d420f2f](https://github.com/st0o0/njord/commit/d420f2f9746c5c874b6732fe7a967e0ecad52cca))
* eliminate FetchCoreAsync, add HTTP resilience pipeline ([237d9cb](https://github.com/st0o0/njord/commit/237d9cbae625d41775edd2e03dc5882765055259))
* expose scoring methods as internal static for direct unit testing ([50a3604](https://github.com/st0o0/njord/commit/50a360426813331dcc1051f058db5d3b8c4295b4))
* extract actor state records following FunkArr pattern ([9fc2a10](https://github.com/st0o0/njord/commit/9fc2a106cb4c1279481254173540220ba7e2395a))
* extract AlertThresholdValues and simplify IndexConfigUpdate ([99e646c](https://github.com/st0o0/njord/commit/99e646c0270ee7e794eb838c38f4f875e8b0ae00))
* extract BuildForecast helper, clean up mappers and request builder ([6897e81](https://github.com/st0o0/njord/commit/6897e81527f036b9b9cd02714ee775df8255141a))
* extract ConfigUpdateHandler from ConfigManager (486 → 323 LOC) ([e2a37ec](https://github.com/st0o0/njord/commit/e2a37ecd1a6a534e7695a83d665ee0b5b6464a1e))
* extract Njord.Polling from Njord.Pipeline ([f0d7595](https://github.com/st0o0/njord/commit/f0d75957c639f9e49cf865534cfc210ab59a6fc7))
* extract persistence selection from AkkaSetupContainer and spec it ([8554545](https://github.com/st0o0/njord/commit/8554545bf86b5dd86640abbcebca5dd2cad5d05a))
* extract the host composition into NjordHost ([3017203](https://github.com/st0o0/njord/commit/30172039fcdf578ae8718f31d67cfe1ca07b6876))
* fix namespaces, remove unused usings and archive repo-cleanup change ([57dab22](https://github.com/st0o0/njord/commit/57dab22eec94bbaa9b58ecec98e15f7d71dc4649))
* flatten feature config paths, rename ForecastOptions to WeatherOptions, add IHasArrayDefaults ([3698c66](https://github.com/st0o0/njord/commit/3698c6673cc0b4f68116e1d65ae0941361b75d17))
* flatten Health namespace into Configuration ([dca2eb7](https://github.com/st0o0/njord/commit/dca2eb744a6f24182feaf55e91f19f84533e9305))
* FunkArr-level options owned by their libraries ([6887d6f](https://github.com/st0o0/njord/commit/6887d6f3e3fcc12ca23a32a534cb53feefe25ae3))
* gateway stream-only design with inline lambdas and shard-routed replies ([c952964](https://github.com/st0o0/njord/commit/c9529645d924d26b9261643a551673a3ee581eac))
* grpc-consumer redesign, healthcheck ask-pattern, options defaults on properties ([966a9ab](https://github.com/st0o0/njord/commit/966a9ab1d016d78ce4372d082c9efb1e693176d1))
* immortal pipeline hubs - eliminate StreamConsumerActor, resume-all supervision, one-shot consumers ([e4f3b84](https://github.com/st0o0/njord/commit/e4f3b84289dcafb80033d8c9b2245c4ccee4b593))
* improve test quality and reduce duplication ([94e7b93](https://github.com/st0o0/njord/commit/94e7b93690a101576a3a3951db6443f529f16e18))
* inline DefaultReplyTimeout, clean up PollWorker formatting ([5fbf630](https://github.com/st0o0/njord/commit/5fbf630dbc1ff8753c3abc590f4a533188aa7bfc))
* inline NjordHost setup chain into Program.cs and NjordTestHost ([3ef5a3d](https://github.com/st0o0/njord/commit/3ef5a3d8134bd2d7300c1f73462b0b464b2486e8))
* introduce FetchPlan and remove ResolvedRequirements ([a0d08d1](https://github.com/st0o0/njord/commit/a0d08d197176f6df6db6debca5dfe091c07cf38f))
* let analysis features own their options and move configuration orchestration to the host ([fa30b18](https://github.com/st0o0/njord/commit/fa30b18c913b7b9fdbdf1806e305597bc6969ce9))
* low-priority review cleanup (snapshot logic, unused refs, allocation reduction) ([0296928](https://github.com/st0o0/njord/commit/0296928e0c232aaee5b81ec1181e9c553826ffa7))
* make feature config section names private ([64080f3](https://github.com/st0o0/njord/commit/64080f37748604d6e2f3232b47958318d4c8dc2b))
* make Models and Horizons optional in post-consensus architecture ([a63ad4d](https://github.com/st0o0/njord/commit/a63ad4da9821ac30a7d08958c6bc0e959f443183))
* make point fields nullable, add HasFields validation ([560d040](https://github.com/st0o0/njord/commit/560d04051f78a416dd51cc50babbac0c4f4aab05))
* merge Njord.Forecast into Njord.Core, invert Core/Messages dependency ([330a2f9](https://github.com/st0o0/njord/commit/330a2f963f66c276ab5b4d06b1613b008bcbda38))
* move actor plumbing to owners and MappingExtensions naming ([f0e10cb](https://github.com/st0o0/njord/commit/f0e10cb5e633639d364f32af48ae184a84bd2e8c))
* move all MQTT-specific code into Njord.Mqtt and replace name-based enum conversions with explicit switches ([caafdfa](https://github.com/st0o0/njord/commit/caafdfab9c630ced6a7ffbe4d83255db20457a8e))
* move analysis contracts into Njord.Messages folders and rename Njord.Compute to Njord.Consensus ([0774501](https://github.com/st0o0/njord/commit/07745012c2a3efd0c79bc500aa759c096653c929))
* move event, Open-Meteo series and log level mapping out of actors and the client into mapping classes ([d00391f](https://github.com/st0o0/njord/commit/d00391fcf753502101fa0ac389662aa3f7078160))
* move PersistenceOptions and PersistenceProvider to Core ([53d58f5](https://github.com/st0o0/njord/commit/53d58f5be328fca6d972644b8386519cd1f3c533))
* move stream, result and feature-rule types to Njord.Messages, add Akka.Streams dep ([27879e7](https://github.com/st0o0/njord/commit/27879e7e2d81cff445cfa0ec96f2d61b70aad2ac))
* overhaul mutation testing with auto-discovered targets and break threshold ([cfefb8c](https://github.com/st0o0/njord/commit/cfefb8c39ea76f733e4c5c9e08b61016d122e545))
* per-domain options, thin clients, enrichment-driven variables ([3375618](https://github.com/st0o0/njord/commit/337561809b1d155389ee9ccb7d6ce6fe97620786))
* persist the poll phase as a persisted enum and move persistence mappers out of the state types ([cb6b2a8](https://github.com/st0o0/njord/commit/cb6b2a8686efffff4ea5f351eb09f8487c5b1f9d))
* PollManager immutable state pattern with Apply extensions and immutable PollCycleTracker ([f633109](https://github.com/st0o0/njord/commit/f633109b046ac93a9a0a7184d806ca6750b1725f))
* PollWorker per-domain independent timers, eliminate round concept ([6a2ce3a](https://github.com/st0o0/njord/commit/6a2ce3afe8189ace443ebcd5fd3203f55814ae1d))
* PollWorker Tell + reply timer, IWithLocationId on responses, simplify FetchTimeouts ([6043478](https://github.com/st0o0/njord/commit/60434789e667b430db4d6d17b06265d10d9ea854))
* read scalar option defaults from the shipped appsettings.json instead of code initializers ([e16ef51](https://github.com/st0o0/njord/commit/e16ef517e752ff2aa52996077d65aee01f43832e))
* redesign health checks and telemetry with push pattern ([02d9ac4](https://github.com/st0o0/njord/commit/02d9ac424e12bb9a0f34979ef8dec00f1f048c36))
* redesign pipeline with projection groups and domain-based enrichment ([45fef03](https://github.com/st0o0/njord/commit/45fef0393941609a6ea73e84d70f718f9a17ca4d))
* redesign poll worker from per-model to per-location with ensemble support ([c00aacb](https://github.com/st0o0/njord/commit/c00aacbac5ce90ee02de576feb5e35e3a18b6890))
* register derived options via Options pipeline instead of raw IOptions singletons ([d09bb47](https://github.com/st0o0/njord/commit/d09bb479960b338cb2fde5497c127e480bb693bc))
* remove 44 low-value tests (compiler-guaranteed records, redundant enum mappings, library tests) ([b549c43](https://github.com/st0o0/njord/commit/b549c43dda86f0d2507296042c6033d4ba1f1568))
* remove converter reflection and extract query constants ([ba425d2](https://github.com/st0o0/njord/commit/ba425d24f6fb6c546f1b6f6f718b61916e740975))
* remove dead code and fix type locations ([c39d784](https://github.com/st0o0/njord/commit/c39d784dd757e40dcb01dad8abe068845140732f))
* remove dead code, unused deps, stale files and fix docs nav ([cc16f15](https://github.com/st0o0/njord/commit/cc16f15786bc36dfc0f576264887c4b088ad4c48))
* remove EnergyDemand, clean PollWorker, hardcode client URLs ([3455af0](https://github.com/st0o0/njord/commit/3455af031386e351f938b2dba4f87c1e9d291d35))
* remove geocoding and climate change APIs ([65faf8a](https://github.com/st0o0/njord/commit/65faf8a926f12b187541bcf5b15447e4abca1e5a))
* remove legacy consensus, derived, trends, indices, and forecast projects ([040e677](https://github.com/st0o0/njord/commit/040e6770133dadeb70e647edbdd096d0bbedd852))
* remove legacy domain handler and alert option system ([76a1632](https://github.com/st0o0/njord/commit/76a1632a03ca32860ed71cd66d1bb2bff9bca9b9))
* remove Njord.Mqtt project ([8248c58](https://github.com/st0o0/njord/commit/8248c5831bf6c3fb550c9f0b869ec8e8bd86384f))
* remove redundant comments from message records, expand PollPhase enum ([ab50dd2](https://github.com/st0o0/njord/commit/ab50dd2c4b002a194ef1c5d2aa91a7cecc105632))
* remove runtime config management system ([5d8b9ae](https://github.com/st0o0/njord/commit/5d8b9aed4910ebb8ef53dd828cd5ff7df5e3612a))
* remove Satellite domain, replace SatelliteOptions with SolarOptions ([f1f9614](https://github.com/st0o0/njord/commit/f1f96142f75067b5b59398460575d08c2856d19d))
* remove sensor hub and indoor sensor input ([a45be5e](https://github.com/st0o0/njord/commit/a45be5ea1362fb0730512ee10094024e3bf41d36))
* remove unused FloodMarineForecastInput ([c2a61ca](https://github.com/st0o0/njord/commit/c2a61ca201a4ee10ec280ed1bf6b38c30ddd85b7))
* remove unused required params from rules and fix BudgetGuard ensemble variable count ([b960cad](https://github.com/st0o0/njord/commit/b960cada0fec3c13c3832b8ed521c926fae01fbc))
* remove unused usings and tidy style ([dadaed7](https://github.com/st0o0/njord/commit/dadaed7a3ac4b3c22701d39cf322e0b9a9832fea))
* remove v2 gRPC and make v3 the unversioned standard ([3a0b9e0](https://github.com/st0o0/njord/commit/3a0b9e0e0693ec54c0a36ca68dd77444d26ee0b7))
* remove WeatherResponseConverter and use dictionary-based deserialization ([2e70ae6](https://github.com/st0o0/njord/commit/2e70ae65c6d83bc8d8912343d4393e2a7c46fba1))
* rename Forecast/ to Fetch/, split Pipeline/ into Polling/Stream/Results per OutputKind ([cae0fb8](https://github.com/st0o0/njord/commit/cae0fb87b45e215037f55b2323e505962b2150ab))
* rename Njord.Model to Njord.Forecast ([245dcc7](https://github.com/st0o0/njord/commit/245dcc781951fd039f67061b082c37e317fe757f))
* rename Param enums to plural (Sonar S2344) ([ce34c59](https://github.com/st0o0/njord/commit/ce34c59f7b9d037dcfaf8673fc859dda3e737270))
* rename Weather domain to Forecast across code, proto, config, and docs ([89eaf0c](https://github.com/st0o0/njord/commit/89eaf0c29fa6929654f7e138269719e231cf1765))
* reorder DomainRequirements params by domain, use named args ([c2b5419](https://github.com/st0o0/njord/commit/c2b5419f5a6697c2247457cad730e22ce7bcfe76))
* reorganize handler projects by OutputKind pattern ([f048e8e](https://github.com/st0o0/njord/commit/f048e8e3d6aa57369361dfe2cdf9953270e98cab))
* replace AnalysisFixture with DomainForecast builder ([43c9b3e](https://github.com/st0o0/njord/commit/43c9b3e012e59b8c8fc84f1e26f082ca150ceb82))
* replace ConfigKey and EnrichmentOptions with FeatureDescriptor and FeatureOptions ([1628bba](https://github.com/st0o0/njord/commit/1628bbadb4a5224c1b35019df9134bd5eeed2fd0))
* replace domain handlers with FeatureRule and FeatureEngine ([d473ff2](https://github.com/st0o0/njord/commit/d473ff2d335186b4d128ebb136765bf743828945))
* replace EnabledDomains with enrichment-driven config ([d3e131e](https://github.com/st0o0/njord/commit/d3e131e9535e8312f27ad1a85476f1aeb818def2))
* replace enrichment framework with simple IDomainHandler pattern ([b5c033c](https://github.com/st0o0/njord/commit/b5c033c2b14424f24e7d34492030e40ffe1c177e))
* replace FeatureKind/AlertType/LifestyleKind with OutputKind and feature names ([ac7c1ec](https://github.com/st0o0/njord/commit/ac7c1ec837d8f7164f7f32bf01b4136d73347229))
* replace FetchPlan/ServiceInfo with FeatureRequirementsOptions via OptionsBuilder ([bb373bd](https://github.com/st0o0/njord/commit/bb373bd2a2813a9c771ba0cbf2c76876e558cb92))
* replace flat DayScoreSet with dictionary keyed by IndexKind ([8ecac91](https://github.com/st0o0/njord/commit/8ecac91c606344602be39d107925fc3d7e73f9a5))
* replace JournalHealthCheck with Akka.Hosting checks, add DataDirectoryHealthCheck ([10c46c3](https://github.com/st0o0/njord/commit/10c46c369e317cac1d68e6eb497921957e324fd3))
* replace LocationId with Location throughout codebase ([daac52e](https://github.com/st0o0/njord/commit/daac52e5ae14ba9bea2cfcb7af6b0af3cbc55ba1))
* replace ManifestBuilder+EntityIdLookup with EntityRegistry, domain-typed consumer hub ([ce05866](https://github.com/st0o0/njord/commit/ce05866e563316df0d62f33965614474711524c7))
* replace ModelComparison with passive ForecastModelComparison and EnsembleModelComparison ([c1f2aa9](https://github.com/st0o0/njord/commit/c1f2aa95183f6d8d26e2935564e3b284bd739954))
* replace njord IActorRegistration with Terp.Akka.Hosting ([482fab0](https://github.com/st0o0/njord/commit/482fab0f6085918833789a68af40abc0da5cf79c))
* replace param mapping switches with API variable enums ([e453b9a](https://github.com/st0o0/njord/commit/e453b9af86804489d48d619455646f2b05a5cb0f))
* replace Servus with Terp (packages, setups, NjordHost split) ([5728588](https://github.com/st0o0/njord/commit/57285882f0b7aa466f6ab9aba250850a06df075e))
* replace StreamSupervision with Terp.Akka ResumeOnError ([d474728](https://github.com/st0o0/njord/commit/d4747283f73ffb332bcc836c6b643240fd575a29))
* replace string fields with proto enums in all new conditions ([2bc2737](https://github.com/st0o0/njord/commit/2bc273779730102593f043763d984345a661e58c))
* replace the poll scheduler with a poll manager and persistent per-target poll workers ([a4ae165](https://github.com/st0o0/njord/commit/a4ae1657d06d5b708c91093d57f934c7d1e6296c))
* replace the shared health monitor by feature-owned health state and checks ([0c5ce27](https://github.com/st0o0/njord/commit/0c5ce27ff2c9ffb7c2af128c8f85ae0672b5fd2a))
* replace Throttle + GatewayBudgetGate with custom BudgetGateStage GraphStage ([0459151](https://github.com/st0o0/njord/commit/0459151011bfd0e90dddc66f23a3d65e1be7d9e3))
* replace Triggers with DomainRequirements on IDomainHandler ([050cdd6](https://github.com/st0o0/njord/commit/050cdd6a440db3d41786b2ce4362c13b6b049105))
* reserve Snapshot for persisted actor state and name contract types after what they hold ([b275e1f](https://github.com/st0o0/njord/commit/b275e1f357962257e8f62ddd6043361849a98e8b))
* restructure Njord.OpenMeteo into domain folders ([c29e3c7](https://github.com/st0o0/njord/commit/c29e3c7eecaa29157a8426bd3d5183142951bf3d))
* return a single result from IFeatureRule.Execute ([e715949](https://github.com/st0o0/njord/commit/e71594999f8e132642e812ecb627542bee82ef51))
* rewrite AddFeatures with AddFeature helper, implement IHasArrayDefaults, flatten Config Builder keys ([1a1e1d3](https://github.com/st0o0/njord/commit/1a1e1d35e7403c76eb012c2011bff4c3cb4ee284))
* rewrite all tests to FunkArr style (Tests suffix, no shared bases, no TestTimeouts) ([c34d41e](https://github.com/st0o0/njord/commit/c34d41e54601fa42c9b45bce4ce97a8b78e1029c))
* rewrite gateway to stream-based dispatch ([cb4c908](https://github.com/st0o0/njord/commit/cb4c9081fd3a1bf972d89876bbd0f217b3f48c02))
* rewrite gateway to stream-only with shard-routed replies ([d44c10f](https://github.com/st0o0/njord/commit/d44c10fc6a2935ea2d9df42260576477ffb82108))
* rewrite gateway with thick clients (FunkArr pattern) ([c4ecee4](https://github.com/st0o0/njord/commit/c4ecee497060c75fa94e13aba7fe7916e8887f7c))
* rewrite OpenMeteo with clean API models and generic fetch ([2116925](https://github.com/st0o0/njord/commit/21169259bbbbf924530b9bb22c8eabe025dc2015))
* rewrite OpenMeteo with typed per-domain clients and response models ([45b0e51](https://github.com/st0o0/njord/commit/45b0e51299b19d4505b01c4817b621566409d4e0))
* rewrite PollWorker with single per-domain state layer ([97b0617](https://github.com/st0o0/njord/commit/97b0617b0a48b682aa67e51f934e8be5b49f778f))
* rewrite PollWorker with typed events, no object, no PersistAll ([d6ad6c8](https://github.com/st0o0/njord/commit/d6ad6c8c0b04d1383f9cef64df1f830c1c1a65e1))
* scope Verify to Persistence + Grpc only, remove from 5 test projects ([850090c](https://github.com/st0o0/njord/commit/850090c2c38bbb0ad501f0eab52146c610e179ba))
* simplify config - remove Models, Horizons, Parameters, internalize weather variables ([846552d](https://github.com/st0o0/njord/commit/846552d2da005e9b64fa10763f1fa5b97a59fcb4))
* simplify gateway with generic domain routing and one-shot attach ([2707b32](https://github.com/st0o0/njord/commit/2707b32fbf736a0fac58565da4619cb164bcc96c))
* simplify health checks with direct static state ([67e4b72](https://github.com/st0o0/njord/commit/67e4b72005bcedb567f073ba37aafc26b9031fa8))
* simplify mappers to pure field assignment, extract trimming ([c11ff7b](https://github.com/st0o0/njord/commit/c11ff7b7081ea18dfa6255243c3dd24dd0259839))
* simplify Model project to pure vocabulary types ([9acc81a](https://github.com/st0o0/njord/commit/9acc81aff49258ad8488f6ac46eaccff5cc61cdb))
* simplify PollManager by removing region resolution retry loop ([2feccc0](https://github.com/st0o0/njord/commit/2feccc0436e232391c047fea840d6678d70e12ff))
* Simplify state updates and remove unused imports ([9aec82f](https://github.com/st0o0/njord/commit/9aec82fbede430860b6452f652872d3be86c972e))
* split alert domains into per-domain projects ([74b3e17](https://github.com/st0o0/njord/commit/74b3e17ae40055652d5e7c82409ef340f30de1b1))
* split god validator, resolve SolarYield bridge, remove DefaultEnabled ([ce80d94](https://github.com/st0o0/njord/commit/ce80d94e73f07daf4fd937b2fec28a28c65b11df))
* split god-specs into focused test files following FunkArr pattern ([d3063f4](https://github.com/st0o0/njord/commit/d3063f4d457979157871560417716f3dad6710b8))
* split Njord.Weather into Njord.Model and Njord.Forecast ([a60864f](https://github.com/st0o0/njord/commit/a60864f5d83a4ffac57cda1c8e13cee8c37f33d7))
* split polling, Open-Meteo and storage options into their own sections and register options through one helper ([59421ea](https://github.com/st0o0/njord/commit/59421ea9b5dee957afc9551e3c6c89522ecb8244))
* split WeatherClient into ForecastClient and EnsembleClient ([9f515a6](https://github.com/st0o0/njord/commit/9f515a6d9f91122bfe884820bdb233a94776a739))
* split WeatherOptions into ForecastOptions + EnsembleOptions, remove FeatureRequirementsOptions and FeatureResolution ([0d69d1a](https://github.com/st0o0/njord/commit/0d69d1a64435d8502079f6ad9b2fd44292d40021))
* sync actor resolution and split gate from fetch stages ([36a9be1](https://github.com/st0o0/njord/commit/36a9be1da78ad2b3aa6aea3bb1e1da233b6805dd))
* Terp cleanup and stream recovery improvements ([787505e](https://github.com/st0o0/njord/commit/787505e17f7ab9b6ae8b03f5853b508fd0e886ff))
* typed feature results and proto oneof on the wire ([a911210](https://github.com/st0o0/njord/commit/a91121020c2a6296d765c04ee2d1672c5aaa3b73))
* unify alert type hierarchies into single AlertType enum ([b5ac9c1](https://github.com/st0o0/njord/commit/b5ac9c18584f01e93738159cf28e7f6ae6b6be4b))
* unify mappings into per-domain static extension classes and split the core setup container ([2977df5](https://github.com/st0o0/njord/commit/2977df5442ae1ac1cc291013809977adf2f0b8a2))
* update config files, docs, e2e, tests for flat feature paths and Njord:Weather ([63208b9](https://github.com/st0o0/njord/commit/63208b907dfafbac183964516a55fa62b3bb8a88))
* update gRPC and MQTT consumers for domain-based pipeline events ([0ae9d80](https://github.com/st0o0/njord/commit/0ae9d8026ef72e15aa75f87e724a29036842e5a3))
* upgrade Terp 0.1.1 to 0.2.0 (SetupChain API) ([db05d6f](https://github.com/st0o0/njord/commit/db05d6fd1ab075f77a6e6f4372df9cad24c0cbf0))
* use instance request ids in OpenMeteoGateway ([0da4e5f](https://github.com/st0o0/njord/commit/0da4e5fa4622e92afe0347eb28511a807889b42f))
* use resolved requirements in BudgetCalculator, remove WeatherVariables ([18a0781](https://github.com/st0o0/njord/commit/18a0781a8f34f32dbd0e617d69718501ddd95635))
* use Sink.ActorRef for gateway reply branch ([00c4d33](https://github.com/st0o0/njord/commit/00c4d33be12dac6c10e0d89407db41d8b88ea824))
* use Terp ValidationFailures in 38 options validators ([179dbba](https://github.com/st0o0/njord/commit/179dbba5678fa6b7ddf04110064c590dd942814b))
