# Changelog

## [0.5.0](https://github.com/st0o0/njord/compare/v0.4.1...v0.5.0) (2026-10-30)


### ⚠ BREAKING CHANGES

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
* remove the unused Ack message and describe the final consumer shape
* make Njord.Messages the Akka-free contract project
* bind one options type per section
* drop dead JSON annotations from analysis records
* simplify persistence records - positional style, DateTimeOffset, drop JsonProperty
* align codebase with FunkArr conventions - messages, persistence, logging, naming
* implement gateway-pipeline-redesign - extract OpenMeteoGateway, Budget project, rename actors to FunkArr-style

### Features

* add [Flags] param enums per domain ([68849f4](https://github.com/st0o0/njord/commit/68849f42ecc090e7eae8353013261bf369097809))
* add 20 API variables and ShowerAlert, SoilMoisture, Evapotranspiration, SmogRisk, RagweedPollen, GranularAqi, OceanCurrents ([22d4192](https://github.com/st0o0/njord/commit/22d41924375d990b299d15d2270c18d03f808936))
* add 20 feature config validators, move domain options validators to Core ([ad3c62f](https://github.com/st0o0/njord/commit/ad3c62f5d37e74adf57ac38c7e580fcda3daa7e3))
* add air quality and flood domains with alerts and cross-domain enrichments ([1f09686](https://github.com/st0o0/njord/commit/1f09686f73e7a837ff914c92589693f46d191c5a))
* add climate anomaly enrichment with 30-year baseline comparison ([e5aae36](https://github.com/st0o0/njord/commit/e5aae36b1b8c3e32687caea0d461dd00bd495da7))
* add CompoundHeatStress alert and AllergySeverity score ([feeaf55](https://github.com/st0o0/njord/commit/feeaf55f470127462ae7b15e6bfb333f680d7e07))
* add ConfigManager for hot config reload ([fbec8bd](https://github.com/st0o0/njord/commit/fbec8bd3b84684449df0edfc5770e1332e339881))
* add core domain model for multi-domain proxy architecture ([022b10f](https://github.com/st0o0/njord/commit/022b10fdfb7b2af95c830732b4ededb20e107f5f))
* add cost-based Throttle to gateway stream and fix MaxBurst floor ([da6527b](https://github.com/st0o0/njord/commit/da6527baba3a5296312dc2864ea21510a67c443a))
* add ensemble support to flood API ([420bb0c](https://github.com/st0o0/njord/commit/420bb0c30436835f219258f149c62f5591c88404))
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
* add optional tier to DomainRequirements ([78a58a9](https://github.com/st0o0/njord/commit/78a58a9d4029500d429228403bcee9bb23cabd6a))
* add poll target contracts and the shard message extractor ([862afb5](https://github.com/st0o0/njord/commit/862afb5f2bdac9bb2e20b0e9d16f1cb4efc19dbf))
* add RainProbability, WetBulbDanger, ClearSkyUv, SnowDepthWatch features ([731a9dd](https://github.com/st0o0/njord/commit/731a9dd4d04ef0d246dd8be1d2364937d4f5fd43))
* add remaining API params (Weather+7, AQ+8, Marine+7) and ElevatedWind sensors ([1ba341f](https://github.com/st0o0/njord/commit/1ba341ff32227f53a28f7247940c82bb358f22f2))
* add satellite radiation domain with GTI blending ([b63cd9c](https://github.com/st0o0/njord/commit/b63cd9cc535d007b4840405c4f152a5f32f25b63))
* add Seasonal domain and SeasonalOutlook feature ([2ab4f74](https://github.com/st0o0/njord/commit/2ab4f74fca1ba135ad24a0bcbb3344d4fe980395))
* add solar radiation variables to ensemble weather data ([4bffdaf](https://github.com/st0o0/njord/commit/4bffdafa3c37fd325692ea2420e6e3f25bac11d3))
* add the persistent per-target poll worker ([cea4a6b](https://github.com/st0o0/njord/commit/cea4a6beefc0d2ec56bd2d45e223822290254ffd))
* add the poll manager that orchestrates the poll workers ([4ab1d08](https://github.com/st0o0/njord/commit/4ab1d08a5ee9a99528ebe253f7b3497c5dbb3e20))
* add the poll worker state and its persistence records ([820d09d](https://github.com/st0o0/njord/commit/820d09d30955a768a1f0f20b2251a1233fcf169f))
* add tier-2 enrichments (solar yield, gardening, energy, outdoor, bbq, flood timeline, front detection) ([5986aad](https://github.com/st0o0/njord/commit/5986aade33e5f6c20f80adf9fd232085dbcb7df1))
* add TimeWindow scoring with sliding-window algorithm and gRPC mapping ([44d2eaa](https://github.com/st0o0/njord/commit/44d2eaae679f29f40686532245929fc214bf7033))
* add WireTap on gateway reply sink and KeepAlive + IdleTimeout on gRPC client streams ([90e7f6a](https://github.com/st0o0/njord/commit/90e7f6a8342253909f06013904299e832cb5a715))
* answer config and budget queries and commands as messages ([447b24b](https://github.com/st0o0/njord/commit/447b24b06a496eb5222c2d5f331fdc0c7beff6c3))
* Clean up unused imports ([c929900](https://github.com/st0o0/njord/commit/c929900fa418b0f74cdf69dd100a12538d747f0e))
* expand air quality with CO, SO2, dust, ammonia, olive pollen, US AQI, UV ([f520941](https://github.com/st0o0/njord/commit/f5209417adbf7e920f84a6dbbbc80be82542c0c1))
* expand air quality with region filter, dust alert and new variables ([4471778](https://github.com/st0o0/njord/commit/447177815300e8528f373fc8de81879c8d702204))
* expand marine with swell, wind-wave and sea level variables ([db4dcee](https://github.com/st0o0/njord/commit/db4dcee0084a077e0054cd210c832ed5c4081013))
* fractional CallWeight, DomainCallConfig, per-domain BudgetGuard, move Forecast/Ensemble/Flood options to OpenMeteo ([20b29c5](https://github.com/st0o0/njord/commit/20b29c5c6398e319fcc84908217d7b42a04e4d12))
* gateway answers fetches request/response and feeds the pipeline's stable input hub via SinkRef ([710398d](https://github.com/st0o0/njord/commit/710398d1d4d3b1d7348dd9edcbba239bf1d28513))
* introduce ActiveModels in Core, fix Catalog to include ForecastOptions.Models, remove Grpc→OpenMeteo reference ([e5552d4](https://github.com/st0o0/njord/commit/e5552d4a3f997d1a66c49b023b49d0c23be30501))
* let a started pipeline fetch the current analysis settings and sensor map ([c03e54c](https://github.com/st0o0/njord/commit/c03e54c88b3a4912475d27cc9cc1d9c67ecf84c8))
* multi-model weather comparison ([b77f836](https://github.com/st0o0/njord/commit/b77f83682266625727f7af345b98ce0d0d14e270))
* pass analysis settings per computation through a pipeline side channel ([8a931e4](https://github.com/st0o0/njord/commit/8a931e4ac315f16e0ffac79185ae077210163f3a))
* per-domain cycle discovery and independent poll scheduling ([bf9843e](https://github.com/st0o0/njord/commit/bf9843e5e022288971a06ab8bb7262d06bcc0724))
* publish explicit nulls for absent alert timing values and soil_frozen as a boolean in MQTT payloads ([68a712e](https://github.com/st0o0/njord/commit/68a712e463f260129f3e8280fa6e024f49a768a7))
* replay cached state to new gRPC stream subscribers ([4143e3f](https://github.com/st0o0/njord/commit/4143e3f364304e3e09e338ad8f6b9fb25971e578))
* sensor hub pushes snapshot maps of all fresh locations to the pipeline ([2b92477](https://github.com/st0o0/njord/commit/2b92477d0844a6d7759727725c517c235763671e))
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
* answer every gateway fetch request once and match locations case-insensitively ([4bec681](https://github.com/st0o0/njord/commit/4bec6815b4b5813c7fe694a9e99bc585ba37e18d))
* bound MQTT broker exchanges to 10 s and republish state after a reconnect ([9073145](https://github.com/st0o0/njord/commit/907314554e39ce35cf6d0ef1085e9e297cd92a36))
* complete poll cycles with failed fetches and keep the gateway fetch stream alive ([281f180](https://github.com/st0o0/njord/commit/281f180496db8f4125df6269577d68454162b6e4))
* correct budget projection for 3 domain calls per location ([6719a1d](https://github.com/st0o0/njord/commit/6719a1dad33b602f8d45149a637ac7453aa9fa14))
* correct FrostDepth degree-hours, BestWindow crash, Evapotranspiration window and SolarYield scaling ([e60e87c](https://github.com/st0o0/njord/commit/e60e87cd9a990e6ec642c74be71a5dd56a5fbf38))
* do not restart poll workers that the shard region stops during shutdown ([8bd3f4b](https://github.com/st0o0/njord/commit/8bd3f4bf8b9312ea40b92f97451dac203a4bd2cf))
* drain the read model stream hub and buffer each streaming client ([80b299e](https://github.com/st0o0/njord/commit/80b299e2cc81b76089a4fa57a83de61a8aa018ce))
* e2e findings (base params, failure backoff, seasonal data, resolved locations) ([106f42d](https://github.com/st0o0/njord/commit/106f42d30a6ce85fbd2f0aeed3190fefd376dc68))
* exclude code coverage tracker from architecture tests ([e7c7e3c](https://github.com/st0o0/njord/commit/e7c7e3c7d61fedb3bbd6db00894ee279e9eae8a8))
* feed live sensor readings of each forecast's location into the indices ([ff306b1](https://github.com/st0o0/njord/commit/ff306b10182a337f22f34970a2f9493cfff74dd6))
* give fetch requests a 60 s admission window and tell queue expiry apart from budget rejections ([b34d222](https://github.com/st0o0/njord/commit/b34d222b7fd1e95bf07f8c298cbc305994624a43))
* handle every MQTT connection lifecycle failure as a message instead of restarting the connection actor ([144cd6a](https://github.com/st0o0/njord/commit/144cd6a8e3554b462f536121a0ab13766e084b6b))
* honor the consensus toggle by suppressing consensus output when disabled ([fcaa1af](https://github.com/st0o0/njord/commit/fcaa1af1d4dea1618d5c96740899e54ab2c8f230))
* Ice alert uses WeatherCode for freezing rain detection, Snow thresholds configurable ([cffe06b](https://github.com/st0o0/njord/commit/cffe06bcce3c591266583b6333fe5d5c497fa1d6))
* include ensemble spread names in SplitByModel to prevent spurious weather entities ([f09a29e](https://github.com/st0o0/njord/commit/f09a29e2bcbbae87a72a63da4ed977e9f89f937a))
* InversionDetection always emits result, add BatteryCapacityKwh to E2E config ([5a466f3](https://github.com/st0o0/njord/commit/5a466f347c7953c98d2e2e9f62aa0afaa2728a7d))
* keep MQTT state publishing alive after a failed publish ([037dec7](https://github.com/st0o0/njord/commit/037dec70ab9945c0cd747c26ec5ca985ce7c7a79))
* log actor events under their actor class and derive the Akka log level from the Serilog minimum levels ([8644015](https://github.com/st0o0/njord/commit/8644015af0ea6361289e313227025161222aa50b))
* narrow gitignore results/ pattern that excluded Njord.Messages/Results/ ([beae0aa](https://github.com/st0o0/njord/commit/beae0aadf283a4d129ca00285271cefeaae0da4b))
* passivate PollWorker through its shard ([7c5309f](https://github.com/st0o0/njord/commit/7c5309f392a165a1df64295648d391104d23e3c1))
* **polling:** persist location coordinates in poll worker records ([2acad00](https://github.com/st0o0/njord/commit/2acad005695dbde5ea386ee24fc9da66aa8a3c8f))
* register the startup AnalysisSettings the forecast pipeline is created with ([fd97c6a](https://github.com/st0o0/njord/commit/fd97c6a491734ab47286164ae44d6bc09441e368))
* remove redundant parameterless constructor from DomainRequirements ([ee84a13](https://github.com/st0o0/njord/commit/ee84a131d72caefd19fe833dcd2eeb418b626b6c))
* replace ILogger in stream closures with telemetry counter ([ac750d6](https://github.com/st0o0/njord/commit/ac750d6c6d46b6eb71e9284e22064bb3c96e8fd0))
* report feature config names in active enrichments ([93eb33c](https://github.com/st0o0/njord/commit/93eb33c28dedc860812affd22d27e50c1754fd0f))
* retry polls rejected while the gateway attaches to the pipeline after five seconds ([e451df4](https://github.com/st0o0/njord/commit/e451df4ff99d5be2b62cc3b06019880e36cf9aab))
* return every enrichment kind from GetEnrichments and filter enrichment streams by location ([0aea6b2](https://github.com/st0o0/njord/commit/0aea6b20f065d0057ddbad59840777f99f18afc0))
* rewire stream consumers and the analysis pipeline when their graph completes or fails ([31913b6](https://github.com/st0o0/njord/commit/31913b6a677a12c454658e0d727b7f7697613663))
* **rules:** Simplify boolean expressions ([4b69ad6](https://github.com/st0o0/njord/commit/4b69ad69f17b9256b2ee70af813ddc624d8721f2))
* size the gateway fetch queue for a full startup round and log each analyzed forecast ([68b5bb1](https://github.com/st0o0/njord/commit/68b5bb19b680cb7d9ef426b6eeb56e1349e5df38))
* stabilize StreamConsumerActorSpec flaky test and run dotnet format ([ac65353](https://github.com/st0o0/njord/commit/ac653535c6a9877766dd3378b957ed29642b49c8))
* start with a corrupt config override file and keep the last good values on reload ([1fbcfed](https://github.com/st0o0/njord/commit/1fbcfed007c48267c6610b247ecaaf10ae430d70))
* thread-safe budget gate, gateway telemetry and cancellation ([214109b](https://github.com/st0o0/njord/commit/214109be3a5cb78d2d3a6c391a2c1e0175e0d659))
* Thunderstorm peak-based severity with CIN downgrade, FireRisk daytime-only scoring ([0efc006](https://github.com/st0o0/njord/commit/0efc0063a177ef4da91f879cfc389ac63339858a))
* update architecture test paths and commit pending changes ([b210e4f](https://github.com/st0o0/njord/commit/b210e4f59ee1c67707893789d91f4a081bf6e06d))
* use correct Ensemble API URL, request model and spread variables for StdDev ([2d2eff2](https://github.com/st0o0/njord/commit/2d2eff2fa820519edb88722280ae11473fed10c2))
* use dynamic Akka remoting port in integration tests ([4436c04](https://github.com/st0o0/njord/commit/4436c044fd8cca00b5d3f72d836dd607d9de61a0))
* use PrecipitationSpread and WindSpeedSpread for domain-specific ensemble confidence ([ab567ef](https://github.com/st0o0/njord/commit/ab567ef95c493171a0ebd7c5929413a0d47bab96))
* wrap Pipeline and GrpcConsumer singletons with BackoffOnFailure ([eff3380](https://github.com/st0o0/njord/commit/eff338083a63725c359585ea5a09143004d97b15))


### Performance

* add collection capacity hints in ConsensusBuilder and ForecastPipeline ([4f74f02](https://github.com/st0o0/njord/commit/4f74f02fa555bcd4f34cdcaa8b7abfbd64db8392))


### Documentation

* archive config-pattern-cleanup, sync specs to main ([50c8a39](https://github.com/st0o0/njord/commit/50c8a39845903dbf09fe274c122ea9b6ff80924c))
* document Njord:Features in appsettings example ([fe004b9](https://github.com/st0o0/njord/commit/fe004b999fa30023d112083dc528e51a73168d3f))
* fix flat feature config paths, correct domain counts, add RainProbability to defaults ([10f26eb](https://github.com/st0o0/njord/commit/10f26ebb3ed17d83289cfde7f67789299c625ea1))
* remove em dashes and simplify wording ([6e84b9e](https://github.com/st0o0/njord/commit/6e84b9eba254e745e5f56f503986f77995007308))
* remove MQTT discovery and history from user docs, examples and diagrams ([0499617](https://github.com/st0o0/njord/commit/04996171470e39be25c2b5d0d42e508a85785dd6))
* update config, docs, and AGENTS.md for restructured forecast ([af26f76](https://github.com/st0o0/njord/commit/af26f76fc8ef432bcda9196f0866dd0a1717a476))
* update docs, config examples and builder for the feature system ([08ce6f7](https://github.com/st0o0/njord/commit/08ce6f75012a81184abe72f46576dd03fd647524))
* update PollWorker logger category in example settings ([7d3e9a0](https://github.com/st0o0/njord/commit/7d3e9a0ca5be5fe01760bc133607c395c849a4dd))
* update seasonal-outlook change for post-restructuring architecture ([0d9ef62](https://github.com/st0o0/njord/commit/0d9ef629fbda0687b9d21211ef270c7192ed9286))


### Refactoring

* add consensus query vocabulary and split Compute into Consensus and Contracts ([98b829c](https://github.com/st0o0/njord/commit/98b829cc84e9ffc24b6f2425f506f5dbf976a483))
* add NjordOptions.ResolvedLocations, eliminate GetValueOrDefault conversions ([dfce4ea](https://github.com/st0o0/njord/commit/dfce4ea31537593d5db0623ccd172e0c9c86d602))
* add OpenMeteoRequestBuilder, eliminate URL string interpolation ([b2d6a76](https://github.com/st0o0/njord/commit/b2d6a765e68fdc55b08aaf1021d25bac0ef92404))
* align actor state pattern with FunkArr conventions ([7e6d02b](https://github.com/st0o0/njord/commit/7e6d02b8a180f5fe541789c1ad5ee8f593ed6914))
* align Akka bootstrap and telemetry with FunkArr patterns ([ff3dce5](https://github.com/st0o0/njord/commit/ff3dce5db831af25ec90c860eb4dfa67978fcd6f))
* align codebase with FunkArr conventions - messages, persistence, logging, naming ([9784f95](https://github.com/st0o0/njord/commit/9784f95eb06c0bf6b6d851eaa3bf756e3da5ab5f))
* align logging with FunkArr conventions ([104860f](https://github.com/st0o0/njord/commit/104860fb650d52e6cf52f1e4d5ba106695404c8f))
* align setup containers with FunkArr pattern ([1939a0a](https://github.com/st0o0/njord/commit/1939a0a9f426edf9752ec273164a4da7ec742d3e))
* attach pipeline consumers only on start, pipeline termination or own graph loss ([8ae6509](https://github.com/st0o0/njord/commit/8ae65091db65110945859a73204ab3073ae03d3e))
* bind one options type per section ([f58481b](https://github.com/st0o0/njord/commit/f58481b312a664df8141e223f263aa43c5f768c9))
* build MQTT state payloads from typed records and serializer converters instead of JsonObject ([5f6291a](https://github.com/st0o0/njord/commit/5f6291abf964a1aae249dc98f9ac0eccf1b7850f))
* clean options DI - bind enrichment sub-options, use IOptions&lt;T&gt; in analyzers and MqttNetPublisher ([42c27aa](https://github.com/st0o0/njord/commit/42c27aabf41f0445cdbc482ce660ac43204f8edc))
* consolidate alert abstractions in Core, eliminate Settings types, unify DI pattern ([4963109](https://github.com/st0o0/njord/commit/4963109e630c7c7c18bf5af09437a649c4db4de4))
* consolidate AlertWindow into shared ForecastWindow helper ([d3b293c](https://github.com/st0o0/njord/commit/d3b293cc2277a2e129de3cc7142954d064fba5d8))
* consolidate PollWorker tests from 13 files to 6, remove ~570 lines of boilerplate ([1726ca7](https://github.com/st0o0/njord/commit/1726ca7ecaac2cb119c441e511d485605be9c834))
* convert AirQualityAlertHandler to IMultiFeatureHandler pattern ([1107736](https://github.com/st0o0/njord/commit/110773668a537179a5d6459267861220bc183413))
* core cleanup and convention hygiene - relocate helpers, typed constants, FunkArr persistence alignment ([6b9382d](https://github.com/st0o0/njord/commit/6b9382d5d2878b962cc86c18d7067bfdd72b6c6e))
* declare each weather parameter once as a typed field and move parameter selection and MQTT keys to their owners ([ede966d](https://github.com/st0o0/njord/commit/ede966dfa0aa7fb008b461e6c8f8063772c64c4a))
* delete dead messages, replace Effective* with PostConfigure, remove Model shim ([a2940c4](https://github.com/st0o0/njord/commit/a2940c4e294c6fe37f97eaada68820f6bc3079c1))
* delete legacy Enrichments migration, breaking change is fine ([934a5fc](https://github.com/st0o0/njord/commit/934a5fc7ef2abf243db146a7ddd3efed04743542))
* delete Njord.Tests host test project and clean up references ([bb2a59f](https://github.com/st0o0/njord/commit/bb2a59fc8d7393ddfd1740bff52e3ce1db9b0054))
* dissolve SolarYieldOptions, merge SolarBatteryStrategy into SolarYield, make alert thresholds configurable, add WindowHours ([57b8fd4](https://github.com/st0o0/njord/commit/57b8fd4170e70a4c4daee6b56730cc154d0a0127))
* drop dead JSON annotations from analysis records ([2e6e2b6](https://github.com/st0o0/njord/commit/2e6e2b647dd16602373ad96a35dbb0324fef6103))
* drop unused IConfiguration parameter from AddFeatures ([d420f2f](https://github.com/st0o0/njord/commit/d420f2f9746c5c874b6732fe7a967e0ecad52cca))
* eliminate FetchCoreAsync, add HTTP resilience pipeline ([237d9cb](https://github.com/st0o0/njord/commit/237d9cbae625d41775edd2e03dc5882765055259))
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
* give compute immutable settings records instead of options ([def081d](https://github.com/st0o0/njord/commit/def081dff1344152cb4cbb52dedb891ed4567f7b))
* grpc-consumer redesign, healthcheck ask-pattern, options defaults on properties ([966a9ab](https://github.com/st0o0/njord/commit/966a9ab1d016d78ce4372d082c9efb1e693176d1))
* immortal pipeline hubs - eliminate StreamConsumerActor, resume-all supervision, one-shot consumers ([e4f3b84](https://github.com/st0o0/njord/commit/e4f3b84289dcafb80033d8c9b2245c4ccee4b593))
* implement gateway-pipeline-redesign - extract OpenMeteoGateway, Budget project, rename actors to FunkArr-style ([be2299b](https://github.com/st0o0/njord/commit/be2299b7e637dc6379bff77df3d7e4ca9409d72d))
* improve test quality and reduce duplication ([94e7b93](https://github.com/st0o0/njord/commit/94e7b93690a101576a3a3951db6443f529f16e18))
* inline DefaultReplyTimeout, clean up PollWorker formatting ([5fbf630](https://github.com/st0o0/njord/commit/5fbf630dbc1ff8753c3abc590f4a533188aa7bfc))
* introduce FetchPlan and remove ResolvedRequirements ([a0d08d1](https://github.com/st0o0/njord/commit/a0d08d197176f6df6db6debca5dfe091c07cf38f))
* let analysis features own their options and move configuration orchestration to the host ([fa30b18](https://github.com/st0o0/njord/commit/fa30b18c913b7b9fdbdf1806e305597bc6969ce9))
* make feature config section names private ([64080f3](https://github.com/st0o0/njord/commit/64080f37748604d6e2f3232b47958318d4c8dc2b))
* make Models and Horizons optional in post-consensus architecture ([a63ad4d](https://github.com/st0o0/njord/commit/a63ad4da9821ac30a7d08958c6bc0e959f443183))
* make Njord.Messages the Akka-free contract project ([764072b](https://github.com/st0o0/njord/commit/764072b5286a99f66bd2aa741ff9876c186b8382))
* make point fields nullable, add HasFields validation ([560d040](https://github.com/st0o0/njord/commit/560d04051f78a416dd51cc50babbac0c4f4aab05))
* make the gRPC API a pure ask-and-map adapter ([0759022](https://github.com/st0o0/njord/commit/0759022749a634d9ff64188235dde855307c2497))
* merge Njord.Forecast into Njord.Core, invert Core/Messages dependency ([330a2f9](https://github.com/st0o0/njord/commit/330a2f963f66c276ab5b4d06b1613b008bcbda38))
* move actor plumbing to owners and MappingExtensions naming ([f0e10cb](https://github.com/st0o0/njord/commit/f0e10cb5e633639d364f32af48ae184a84bd2e8c))
* move all MQTT-specific code into Njord.Mqtt and replace name-based enum conversions with explicit switches ([caafdfa](https://github.com/st0o0/njord/commit/caafdfab9c630ced6a7ffbe4d83255db20457a8e))
* move analysis contracts into Njord.Messages folders and rename Njord.Compute to Njord.Consensus ([0774501](https://github.com/st0o0/njord/commit/07745012c2a3efd0c79bc500aa759c096653c929))
* move derived and trend computation into their feature libraries ([96acf63](https://github.com/st0o0/njord/commit/96acf638ffe88208d03f3568a64db6593244328e))
* move event, Open-Meteo series and log level mapping out of actors and the client into mapping classes ([d00391f](https://github.com/st0o0/njord/commit/d00391fcf753502101fa0ac389662aa3f7078160))
* move gRPC read model into the pipeline ([5413b4a](https://github.com/st0o0/njord/commit/5413b4a8d8ce472a0f92eee42c8139597f75b1ec))
* move index scoring into Njord.Indices ([26c0adc](https://github.com/st0o0/njord/commit/26c0adc8107aa3b283d084a4430abfd3b9c8672b))
* move PersistenceOptions and PersistenceProvider to Core ([53d58f5](https://github.com/st0o0/njord/commit/53d58f5be328fca6d972644b8386519cd1f3c533))
* move stream, result and feature-rule types to Njord.Messages, add Akka.Streams dep ([27879e7](https://github.com/st0o0/njord/commit/27879e7e2d81cff445cfa0ec96f2d61b70aad2ac))
* narrow options injection and guard options monitor use ([1750209](https://github.com/st0o0/njord/commit/175020934cc2b2e62aa6c481894effb0b66d6f75))
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
* remove the unused Ack message and describe the final consumer shape ([0e31a16](https://github.com/st0o0/njord/commit/0e31a16ea91723fff59587367120063b3ad7c082))
* remove unused FloodMarineForecastInput ([c2a61ca](https://github.com/st0o0/njord/commit/c2a61ca201a4ee10ec280ed1bf6b38c30ddd85b7))
* remove unused required params from rules and fix BudgetGuard ensemble variable count ([b960cad](https://github.com/st0o0/njord/commit/b960cada0fec3c13c3832b8ed521c926fae01fbc))
* remove unused usings and tidy style ([dadaed7](https://github.com/st0o0/njord/commit/dadaed7a3ac4b3c22701d39cf322e0b9a9832fea))
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
* replace param mapping switches with API variable enums ([e453b9a](https://github.com/st0o0/njord/commit/e453b9af86804489d48d619455646f2b05a5cb0f))
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
* Simplify imports and modernize List creation ([d2da668](https://github.com/st0o0/njord/commit/d2da6681762f0b8bdbccb8791eae2a442ded25e1))
* simplify mappers to pure field assignment, extract trimming ([c11ff7b](https://github.com/st0o0/njord/commit/c11ff7b7081ea18dfa6255243c3dd24dd0259839))
* simplify Model project to pure vocabulary types ([9acc81a](https://github.com/st0o0/njord/commit/9acc81aff49258ad8488f6ac46eaccff5cc61cdb))
* simplify persistence records - positional style, DateTimeOffset, drop JsonProperty ([d655aea](https://github.com/st0o0/njord/commit/d655aeaf07176f6719b1093395fc8016bc9718de))
* Simplify state updates and remove unused imports ([9aec82f](https://github.com/st0o0/njord/commit/9aec82fbede430860b6452f652872d3be86c972e))
* split alert domains into per-domain projects ([74b3e17](https://github.com/st0o0/njord/commit/74b3e17ae40055652d5e7c82409ef340f30de1b1))
* split alert evaluation into one rule per alert type ([9b60b9b](https://github.com/st0o0/njord/commit/9b60b9bc2f75e8983ba5f741c94a9f70e55637be))
* split god validator, resolve SolarYield bridge, remove DefaultEnabled ([ce80d94](https://github.com/st0o0/njord/commit/ce80d94e73f07daf4fd937b2fec28a28c65b11df))
* split god-specs into focused test files following FunkArr pattern ([d3063f4](https://github.com/st0o0/njord/commit/d3063f4d457979157871560417716f3dad6710b8))
* split Njord.Weather into Njord.Model and Njord.Forecast ([a60864f](https://github.com/st0o0/njord/commit/a60864f5d83a4ffac57cda1c8e13cee8c37f33d7))
* split polling, Open-Meteo and storage options into their own sections and register options through one helper ([59421ea](https://github.com/st0o0/njord/commit/59421ea9b5dee957afc9551e3c6c89522ecb8244))
* split WeatherClient into ForecastClient and EnsembleClient ([9f515a6](https://github.com/st0o0/njord/commit/9f515a6d9f91122bfe884820bdb233a94776a739))
* split WeatherOptions into ForecastOptions + EnsembleOptions, remove FeatureRequirementsOptions and FeatureResolution ([0d69d1a](https://github.com/st0o0/njord/commit/0d69d1a64435d8502079f6ad9b2fd44292d40021))
* sync actor resolution and split gate from fetch stages ([36a9be1](https://github.com/st0o0/njord/commit/36a9be1da78ad2b3aa6aea3bb1e1da233b6805dd))
* type alert details and severity thresholds ([239a55a](https://github.com/st0o0/njord/commit/239a55a93d461ca12339945a2e5eaa3a1edcebe4))
* type analysis horizons ([aba6560](https://github.com/st0o0/njord/commit/aba6560355cd69cd2582c1984140847effeeae34))
* type categorical analysis results ([92f8318](https://github.com/st0o0/njord/commit/92f83189f4b6d64e25829c388409197ce6ebf59c))
* type index kinds and locations in analysis results ([5220194](https://github.com/st0o0/njord/commit/522019498abcf98baaa5625405be83823b904120))
* typed feature results and proto oneof on the wire ([a911210](https://github.com/st0o0/njord/commit/a91121020c2a6296d765c04ee2d1672c5aaa3b73))
* unify alert type hierarchies into single AlertType enum ([b5ac9c1](https://github.com/st0o0/njord/commit/b5ac9c18584f01e93738159cf28e7f6ae6b6be4b))
* unify mappings into per-domain static extension classes and split the core setup container ([2977df5](https://github.com/st0o0/njord/commit/2977df5442ae1ac1cc291013809977adf2f0b8a2))
* update config files, docs, e2e, tests for flat feature paths and Njord:Weather ([63208b9](https://github.com/st0o0/njord/commit/63208b907dfafbac183964516a55fa62b3bb8a88))
* update gRPC and MQTT consumers for domain-based pipeline events ([0ae9d80](https://github.com/st0o0/njord/commit/0ae9d8026ef72e15aa75f87e724a29036842e5a3))
* use instance request ids in OpenMeteoGateway ([0da4e5f](https://github.com/st0o0/njord/commit/0da4e5fa4622e92afe0347eb28511a807889b42f))
* use resolved requirements in BudgetCalculator, remove WeatherVariables ([18a0781](https://github.com/st0o0/njord/commit/18a0781a8f34f32dbd0e617d69718501ddd95635))
* use Sink.ActorRef for gateway reply branch ([00c4d33](https://github.com/st0o0/njord/commit/00c4d33be12dac6c10e0d89407db41d8b88ea824))
