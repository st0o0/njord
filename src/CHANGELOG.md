# Changelog

## [0.5.0](https://github.com/st0o0/njord/compare/v0.4.1...v0.5.0) (2026-10-22)


### ⚠ BREAKING CHANGES

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
* remove dead code, unused packages, and fix stale references after pipeline refactors
* replace untyped analysis pipeline with typed PipelineEvent subtypes and remove History feature
* rename EgressEvent to PipelineEvent with typed subtypes
* remove Njord.Enrichment, Njord.Egress, presenters, and MQTT discovery
* replace ConfigPersistence with WritableNjordOptions, move Kestrel to appsettings
* replace monolithic setup with domain-specific Servus containers and shard snapshot actors
* dissolve Njord.Tests into IntegrationTests and domain projects
* remove EgressActor hub and replace with producer-owned BroadcastHubs
* merge Analysis and Options from Domain into Core, add solution folders
* align message response naming to QueryNounResponse convention
* persistence DTO types moved to Njord.Persistence assembly; existing persisted scheduler/budget/snapshot data may need to be reset.

### Features

* add [Flags] param enums per domain ([68849f4](https://github.com/st0o0/njord/commit/68849f42ecc090e7eae8353013261bf369097809))
* add air quality and flood domains with alerts and cross-domain enrichments ([1f09686](https://github.com/st0o0/njord/commit/1f09686f73e7a837ff914c92589693f46d191c5a))
* add climate anomaly enrichment with 30-year baseline comparison ([e5aae36](https://github.com/st0o0/njord/commit/e5aae36b1b8c3e32687caea0d461dd00bd495da7))
* add ConfigManager for hot config reload ([fbec8bd](https://github.com/st0o0/njord/commit/fbec8bd3b84684449df0edfc5770e1332e339881))
* add core domain model for multi-domain proxy architecture ([022b10f](https://github.com/st0o0/njord/commit/022b10fdfb7b2af95c830732b4ededb20e107f5f))
* add ensemble support to flood API ([420bb0c](https://github.com/st0o0/njord/commit/420bb0c30436835f219258f149c62f5591c88404))
* add FeatureKind, FeatureCategory and FeatureDescriptor ([a21b1ff](https://github.com/st0o0/njord/commit/a21b1fff964d90036dc8eb0e13b1acc9b8df4f44))
* add geocoding auto-resolve for location coordinates ([e518f02](https://github.com/st0o0/njord/commit/e518f0237680dea5af796e28829b1283be3f6324))
* add GTI, snow depth, wet bulb and spread variables from Ensemble API ([036b6c5](https://github.com/st0o0/njord/commit/036b6c50d965d07862d5e6f9f2e7a6b77b3454dc))
* add IAnalysisStage and wire full analysis pipeline into PipelineActor ([580cea6](https://github.com/st0o0/njord/commit/580cea6090bd177c3d0a2addd367f01c4cb350c5))
* add lifestyle enrichments (ventilation, exercise, laundry, commute, pollen trend, night ventilation, confidence) ([09ac7ce](https://github.com/st0o0/njord/commit/09ac7ce8386bd1ca93b6dae2df3c98e762f7500c))
* add marine domain with swimming conditions ([a9070b6](https://github.com/st0o0/njord/commit/a9070b63cc0b7858f57e43fe25c333493d37d87f))
* add optional tier to DomainRequirements ([78a58a9](https://github.com/st0o0/njord/commit/78a58a9d4029500d429228403bcee9bb23cabd6a))
* add poll target contracts and the shard message extractor ([862afb5](https://github.com/st0o0/njord/commit/862afb5f2bdac9bb2e20b0e9d16f1cb4efc19dbf))
* add satellite radiation domain with GTI blending ([b63cd9c](https://github.com/st0o0/njord/commit/b63cd9cc535d007b4840405c4f152a5f32f25b63))
* add Seasonal domain and SeasonalOutlook feature ([2ab4f74](https://github.com/st0o0/njord/commit/2ab4f74fca1ba135ad24a0bcbb3344d4fe980395))
* add solar radiation variables to ensemble weather data ([4bffdaf](https://github.com/st0o0/njord/commit/4bffdafa3c37fd325692ea2420e6e3f25bac11d3))
* add SourceRef consumer architecture for MQTT, gRPC, and pipeline output ([e8fd06e](https://github.com/st0o0/njord/commit/e8fd06ec01b9fa11e27bb4787166d9c50ae04243))
* add the persistent per-target poll worker ([cea4a6b](https://github.com/st0o0/njord/commit/cea4a6beefc0d2ec56bd2d45e223822290254ffd))
* add the poll manager that orchestrates the poll workers ([4ab1d08](https://github.com/st0o0/njord/commit/4ab1d08a5ee9a99528ebe253f7b3497c5dbb3e20))
* add the poll worker state and its persistence records ([820d09d](https://github.com/st0o0/njord/commit/820d09d30955a768a1f0f20b2251a1233fcf169f))
* add tier-2 enrichments (solar yield, gardening, energy, outdoor, bbq, flood timeline, front detection) ([5986aad](https://github.com/st0o0/njord/commit/5986aade33e5f6c20f80adf9fd232085dbcb7df1))
* add vertical feature libraries with typed input interfaces ([acaa2b7](https://github.com/st0o0/njord/commit/acaa2b7e056424e7b3b647eccae28bb22088a50e))
* answer config and budget queries and commands as messages ([447b24b](https://github.com/st0o0/njord/commit/447b24b06a496eb5222c2d5f331fdc0c7beff6c3))
* Clean up unused imports ([c929900](https://github.com/st0o0/njord/commit/c929900fa418b0f74cdf69dd100a12538d747f0e))
* expand air quality with CO, SO2, dust, ammonia, olive pollen, US AQI, UV ([f520941](https://github.com/st0o0/njord/commit/f5209417adbf7e920f84a6dbbbc80be82542c0c1))
* expand air quality with region filter, dust alert and new variables ([4471778](https://github.com/st0o0/njord/commit/447177815300e8528f373fc8de81879c8d702204))
* expand marine with swell, wind-wave and sea level variables ([db4dcee](https://github.com/st0o0/njord/commit/db4dcee0084a077e0054cd210c832ed5c4081013))
* gateway answers fetches request/response and feeds the pipeline's stable input hub via SinkRef ([710398d](https://github.com/st0o0/njord/commit/710398d1d4d3b1d7348dd9edcbba239bf1d28513))
* let a started pipeline fetch the current analysis settings and sensor map ([c03e54c](https://github.com/st0o0/njord/commit/c03e54c88b3a4912475d27cc9cc1d9c67ecf84c8))
* multi-model weather comparison ([b77f836](https://github.com/st0o0/njord/commit/b77f83682266625727f7af345b98ce0d0d14e270))
* pass analysis settings per computation through a pipeline side channel ([8a931e4](https://github.com/st0o0/njord/commit/8a931e4ac315f16e0ffac79185ae077210163f3a))
* per-domain cycle discovery and independent poll scheduling ([bf9843e](https://github.com/st0o0/njord/commit/bf9843e5e022288971a06ab8bb7262d06bcc0724))
* publish AnalysisResult events as MQTT state in MqttStateActor ([923e58b](https://github.com/st0o0/njord/commit/923e58be0b4ec29b2eaf755aecdd97e9bd72a358))
* publish explicit nulls for absent alert timing values and soil_frozen as a boolean in MQTT payloads ([68a712e](https://github.com/st0o0/njord/commit/68a712e463f260129f3e8280fa6e024f49a768a7))
* sensor hub pushes snapshot maps of all fresh locations to the pipeline ([2b92477](https://github.com/st0o0/njord/commit/2b92477d0844a6d7759727725c517c235763671e))
* stream validation guards handlers against missing data ([37044ba](https://github.com/st0o0/njord/commit/37044ba4f4e1da5b83b6512d22af24cc3f50914e))
* **telemetry:** add HTTP metrics handler, pipeline throughput, sensor and config write counters ([fa072ee](https://github.com/st0o0/njord/commit/fa072ee7618898e8a2c80421e5d813f81693918b))
* type the gRPC closed sets as enums and make SetEnrichment atomic with structured results ([1da4e90](https://github.com/st0o0/njord/commit/1da4e9025ecd43fe1a97a5f622c55b5cf59bcf52))
* WeatherAlertHandler filters rules by individual feature toggles ([4b1b054](https://github.com/st0o0/njord/commit/4b1b0541509e221158aeb3701e175033b2e9e90b))
* wire air quality and flood alerts through gRPC and MQTT consumers ([985ef73](https://github.com/st0o0/njord/commit/985ef736df492a298f9543fa1242634fb062fa93))
* wire air quality and flood alerts to gRPC and MQTT consumers ([0a69f51](https://github.com/st0o0/njord/commit/0a69f51282ec7fbf1fb54f1d470302c954983629))
* wire lifestyle enrichment events to gRPC and MQTT consumers ([403b2d0](https://github.com/st0o0/njord/commit/403b2d04c6d8055d7425fe5c5e9e5e7cbd5c0645))


### Bug Fixes

* adapt to Testcontainers 4.15.0 API and fix gRPC proto codegen warnings ([33cb478](https://github.com/st0o0/njord/commit/33cb4782eebdc75501772fa4ab7a65a16cd9baa9))
* add explicit coordinates to E2E compose, fix enrichment listing in GetStatus ([ecd8f60](https://github.com/st0o0/njord/commit/ecd8f60aae3dfffc2a9fa3a9898bd8906ea1459a))
* add failure handlers to all PipeTo dependency-resolution calls ([80b0be8](https://github.com/st0o0/njord/commit/80b0be83531ee669661ee57464059b35eee4da18))
* answer every gateway fetch request once and match locations case-insensitively ([4bec681](https://github.com/st0o0/njord/commit/4bec6815b4b5813c7fe694a9e99bc585ba37e18d))
* bound MQTT broker exchanges to 10 s and republish state after a reconnect ([9073145](https://github.com/st0o0/njord/commit/907314554e39ce35cf6d0ef1085e9e297cd92a36))
* complete poll cycles with failed fetches and keep the gateway fetch stream alive ([281f180](https://github.com/st0o0/njord/commit/281f180496db8f4125df6269577d68454162b6e4))
* correct budget projection for 3 domain calls per location ([6719a1d](https://github.com/st0o0/njord/commit/6719a1dad33b602f8d45149a637ac7453aa9fa14))
* do not restart poll workers that the shard region stops during shutdown ([8bd3f4b](https://github.com/st0o0/njord/commit/8bd3f4bf8b9312ea40b92f97451dac203a4bd2cf))
* drain the read model stream hub and buffer each streaming client ([80b299e](https://github.com/st0o0/njord/commit/80b299e2cc81b76089a4fa57a83de61a8aa018ce))
* e2e findings (base params, failure backoff, seasonal data, resolved locations) ([106f42d](https://github.com/st0o0/njord/commit/106f42d30a6ce85fbd2f0aeed3190fefd376dc68))
* enforce test conventions and remove XML doc comments ([70aad79](https://github.com/st0o0/njord/commit/70aad7952ec9c6cc58648ccd2396cad2f2f5c36e))
* feed live sensor readings of each forecast's location into the indices ([ff306b1](https://github.com/st0o0/njord/commit/ff306b10182a337f22f34970a2f9493cfff74dd6))
* form cluster via SeedNodes to fix singleton proxy startup race ([c34f960](https://github.com/st0o0/njord/commit/c34f96019a3d286a41a62a4aba8c18c76555fbc4))
* give fetch requests a 60 s admission window and tell queue expiry apart from budget rejections ([b34d222](https://github.com/st0o0/njord/commit/b34d222b7fd1e95bf07f8c298cbc305994624a43))
* handle every MQTT connection lifecycle failure as a message instead of restarting the connection actor ([144cd6a](https://github.com/st0o0/njord/commit/144cd6a8e3554b462f536121a0ab13766e084b6b))
* honor the consensus toggle by suppressing consensus output when disabled ([fcaa1af](https://github.com/st0o0/njord/commit/fcaa1af1d4dea1618d5c96740899e54ab2c8f230))
* keep MQTT state publishing alive after a failed publish ([037dec7](https://github.com/st0o0/njord/commit/037dec70ab9945c0cd747c26ec5ca985ce7c7a79))
* log actor events under their actor class and derive the Akka log level from the Serilog minimum levels ([8644015](https://github.com/st0o0/njord/commit/8644015af0ea6361289e313227025161222aa50b))
* passivate PollWorker through its shard ([7c5309f](https://github.com/st0o0/njord/commit/7c5309f392a165a1df64295648d391104d23e3c1))
* **polling:** persist location coordinates in poll worker records ([2acad00](https://github.com/st0o0/njord/commit/2acad005695dbde5ea386ee24fc9da66aa8a3c8f))
* register the startup AnalysisSettings the forecast pipeline is created with ([fd97c6a](https://github.com/st0o0/njord/commit/fd97c6a491734ab47286164ae44d6bc09441e368))
* reject zero budget, delta-serialize writable options, correct E2E test plan ([85dd311](https://github.com/st0o0/njord/commit/85dd3116cea60238016763275bc6506889bcb342))
* replace ConfigPersistence with WritableNjordOptions, move Kestrel to appsettings ([2cc5531](https://github.com/st0o0/njord/commit/2cc55314da2d4ec2453062e6e1dbf5c1a86c5fa7))
* replace ILogger in stream closures with telemetry counter ([ac750d6](https://github.com/st0o0/njord/commit/ac750d6c6d46b6eb71e9284e22064bb3c96e8fd0))
* report feature config names in active enrichments ([93eb33c](https://github.com/st0o0/njord/commit/93eb33c28dedc860812affd22d27e50c1754fd0f))
* retry polls rejected while the gateway attaches to the pipeline after five seconds ([e451df4](https://github.com/st0o0/njord/commit/e451df4ff99d5be2b62cc3b06019880e36cf9aab))
* return every enrichment kind from GetEnrichments and filter enrichment streams by location ([0aea6b2](https://github.com/st0o0/njord/commit/0aea6b20f065d0057ddbad59840777f99f18afc0))
* rewire stream consumers and the analysis pipeline when their graph completes or fails ([31913b6](https://github.com/st0o0/njord/commit/31913b6a677a12c454658e0d727b7f7697613663))
* **rules:** Simplify boolean expressions ([4b69ad6](https://github.com/st0o0/njord/commit/4b69ad69f17b9256b2ee70af813ddc624d8721f2))
* size the gateway fetch queue for a full startup round and log each analyzed forecast ([68b5bb1](https://github.com/st0o0/njord/commit/68b5bb19b680cb7d9ef426b6eeb56e1349e5df38))
* stabilize flaky egress and architecture tests ([6d47876](https://github.com/st0o0/njord/commit/6d47876014a1fd47ebb2f6e1c1fb9381620a82bc))
* stabilize specs, complete streams on shutdown, restore slopwatch ([9d40a9d](https://github.com/st0o0/njord/commit/9d40a9d2ef82dfab7f40cf6f8143f1460c0e90ee))
* stabilize StreamConsumerActorSpec flaky test and run dotnet format ([ac65353](https://github.com/st0o0/njord/commit/ac653535c6a9877766dd3378b957ed29642b49c8))
* start with a corrupt config override file and keep the last good values on reload ([1fbcfed](https://github.com/st0o0/njord/commit/1fbcfed007c48267c6610b247ecaaf10ae430d70))
* **tests:** apply whitespace formatting and remove stale pipeline init race test ([d6a84dc](https://github.com/st0o0/njord/commit/d6a84dc0bba56d9c513c4b9daa46e3cb93f8af77))
* update architecture test paths and commit pending changes ([b210e4f](https://github.com/st0o0/njord/commit/b210e4f59ee1c67707893789d91f4a081bf6e06d))
* use correct Ensemble API URL, request model and spread variables for StdDev ([2d2eff2](https://github.com/st0o0/njord/commit/2d2eff2fa820519edb88722280ae11473fed10c2))
* use dynamic Akka remoting port in integration tests ([4436c04](https://github.com/st0o0/njord/commit/4436c044fd8cca00b5d3f72d836dd607d9de61a0))


### Performance

* add collection capacity hints in ConsensusBuilder and ForecastPipeline ([4f74f02](https://github.com/st0o0/njord/commit/4f74f02fa555bcd4f34cdcaa8b7abfbd64db8392))


### Documentation

* document Njord:Features in appsettings example ([fe004b9](https://github.com/st0o0/njord/commit/fe004b999fa30023d112083dc528e51a73168d3f))
* remove em dashes and simplify wording ([6e84b9e](https://github.com/st0o0/njord/commit/6e84b9eba254e745e5f56f503986f77995007308))
* remove MQTT discovery and history from user docs, examples and diagrams ([0499617](https://github.com/st0o0/njord/commit/04996171470e39be25c2b5d0d42e508a85785dd6))
* update config, docs, and AGENTS.md for restructured forecast ([af26f76](https://github.com/st0o0/njord/commit/af26f76fc8ef432bcda9196f0866dd0a1717a476))
* update docs, config examples and builder for the feature system ([08ce6f7](https://github.com/st0o0/njord/commit/08ce6f75012a81184abe72f46576dd03fd647524))
* update PollWorker logger category in example settings ([7d3e9a0](https://github.com/st0o0/njord/commit/7d3e9a0ca5be5fe01760bc133607c395c849a4dd))
* update seasonal-outlook change for post-restructuring architecture ([0d9ef62](https://github.com/st0o0/njord/commit/0d9ef629fbda0687b9d21211ef270c7192ed9286))


### Refactoring

* actors inject sub-options directly instead of NjordOptions ([9cf6a20](https://github.com/st0o0/njord/commit/9cf6a205dcfaba0235e40c434ab5183e2bbc34fd))
* add consensus query vocabulary and split Compute into Consensus and Contracts ([98b829c](https://github.com/st0o0/njord/commit/98b829cc84e9ffc24b6f2425f506f5dbf976a483))
* add OpenMeteoRequestBuilder, eliminate URL string interpolation ([b2d6a76](https://github.com/st0o0/njord/commit/b2d6a765e68fdc55b08aaf1021d25bac0ef92404))
* add shared TestOptionsMonitor and replace duplicated fakes ([beaab8c](https://github.com/st0o0/njord/commit/beaab8c83315296ac28f634104281cb6ceb78cfd))
* add sync init path to StreamConsumerActor and all subclasses ([3b50395](https://github.com/st0o0/njord/commit/3b50395a3d6ba3005a52f1e6162bc8a86ff98408))
* align actor state pattern with FunkArr conventions ([7e6d02b](https://github.com/st0o0/njord/commit/7e6d02b8a180f5fe541789c1ad5ee8f593ed6914))
* align codebase with FunkArr conventions - messages, persistence, logging, naming ([9784f95](https://github.com/st0o0/njord/commit/9784f95eb06c0bf6b6d851eaa3bf756e3da5ab5f))
* align logging with FunkArr conventions ([104860f](https://github.com/st0o0/njord/commit/104860fb650d52e6cf52f1e4d5ba106695404c8f))
* align message response naming to QueryNounResponse convention ([f89e9e5](https://github.com/st0o0/njord/commit/f89e9e53daf92287b1cae302bb54643299be292f))
* attach pipeline consumers only on start, pipeline termination or own graph loss ([8ae6509](https://github.com/st0o0/njord/commit/8ae65091db65110945859a73204ab3073ae03d3e))
* bind one options type per section ([f58481b](https://github.com/st0o0/njord/commit/f58481b312a664df8141e223f263aa43c5f768c9))
* build MQTT state payloads from typed records and serializer converters instead of JsonObject ([5f6291a](https://github.com/st0o0/njord/commit/5f6291abf964a1aae249dc98f9ac0eccf1b7850f))
* clean options DI - bind enrichment sub-options, use IOptions&lt;T&gt; in analyzers and MqttNetPublisher ([42c27aa](https://github.com/st0o0/njord/commit/42c27aabf41f0445cdbc482ce660ac43204f8edc))
* consolidate alert abstractions in Core, eliminate Settings types, unify DI pattern ([4963109](https://github.com/st0o0/njord/commit/4963109e630c7c7c18bf5af09437a649c4db4de4))
* consolidate AlertWindow into shared ForecastWindow helper ([d3b293c](https://github.com/st0o0/njord/commit/d3b293cc2277a2e129de3cc7142954d064fba5d8))
* consolidate compute logic into feature libs, rename DTOs to Persisted* prefix ([2f3d62d](https://github.com/st0o0/njord/commit/2f3d62d7a8ccd1c0354e6b65b5144df7bd668879))
* convert AirQualityAlertHandler to IMultiFeatureHandler pattern ([1107736](https://github.com/st0o0/njord/commit/110773668a537179a5d6459267861220bc183413))
* core cleanup and convention hygiene - relocate helpers, typed constants, FunkArr persistence alignment ([6b9382d](https://github.com/st0o0/njord/commit/6b9382d5d2878b962cc86c18d7067bfdd72b6c6e))
* declare each weather parameter once as a typed field and move parameter selection and MQTT keys to their owners ([ede966d](https://github.com/st0o0/njord/commit/ede966dfa0aa7fb008b461e6c8f8063772c64c4a))
* delete legacy Enrichments migration, breaking change is fine ([934a5fc](https://github.com/st0o0/njord/commit/934a5fc7ef2abf243db146a7ddd3efed04743542))
* delete Njord.Tests host test project and clean up references ([bb2a59f](https://github.com/st0o0/njord/commit/bb2a59fc8d7393ddfd1740bff52e3ce1db9b0054))
* dissolve Njord.Tests into IntegrationTests and domain projects ([8e57f84](https://github.com/st0o0/njord/commit/8e57f840315944ed47c0aef548d2e1ed71e50fe6))
* drop dead JSON annotations from analysis records ([2e6e2b6](https://github.com/st0o0/njord/commit/2e6e2b647dd16602373ad96a35dbb0324fef6103))
* drop unused IConfiguration parameter from AddFeatures ([d420f2f](https://github.com/st0o0/njord/commit/d420f2f9746c5c874b6732fe7a967e0ecad52cca))
* eliminate FetchCoreAsync, add HTTP resilience pipeline ([237d9cb](https://github.com/st0o0/njord/commit/237d9cbae625d41775edd2e03dc5882765055259))
* enforce zone architecture rules and typed Akka failures ([c0f9738](https://github.com/st0o0/njord/commit/c0f973830d37d0c4f032197b58641b43296402ed))
* extract actor state records following FunkArr pattern ([9fc2a10](https://github.com/st0o0/njord/commit/9fc2a106cb4c1279481254173540220ba7e2395a))
* extract AlertThresholdValues and simplify IndexConfigUpdate ([99e646c](https://github.com/st0o0/njord/commit/99e646c0270ee7e794eb838c38f4f875e8b0ae00))
* extract BuildForecast helper, clean up mappers and request builder ([6897e81](https://github.com/st0o0/njord/commit/6897e81527f036b9b9cd02714ee775df8255141a))
* extract ConfigUpdateHandler from ConfigManager (486 → 323 LOC) ([e2a37ec](https://github.com/st0o0/njord/commit/e2a37ecd1a6a534e7695a83d665ee0b5b6464a1e))
* extract Domain, Persistence, Messages and Core projects ([ed1754c](https://github.com/st0o0/njord/commit/ed1754c3c2171dbda28a5244d9969ad0a5120855))
* extract Mqtt and Enrichment libraries ([095f823](https://github.com/st0o0/njord/commit/095f823f237d924117c315eba9c57b60683afde8))
* extract Mqtt, Enrichment, Ingest, Sensors test projects ([6ff605f](https://github.com/st0o0/njord/commit/6ff605ff1b8152f167f32148751dcfa760e070d6))
* extract Njord.Compute as pure math library from Njord.Core ([813b56d](https://github.com/st0o0/njord/commit/813b56de31075f29545958678762faa6a11a10de))
* extract Njord.Polling from Njord.Pipeline ([f0d7595](https://github.com/st0o0/njord/commit/f0d75957c639f9e49cf865534cfc210ab59a6fc7))
* extract persistence selection from AkkaSetupContainer and spec it ([8554545](https://github.com/st0o0/njord/commit/8554545bf86b5dd86640abbcebca5dd2cad5d05a))
* extract Pipeline and Egress libraries ([c4e0d76](https://github.com/st0o0/njord/commit/c4e0d76ffbdfc164090ce9c716de6acdd11c0cc6))
* extract Sensors, Ingest and Grpc libraries ([b59ebbb](https://github.com/st0o0/njord/commit/b59ebbb9e17a7f12d696c3a1326f49a17cee37eb))
* extract sub-options with SectionName and per-feature registration ([3da36de](https://github.com/st0o0/njord/commit/3da36de4a7e19d2f85d94dd0647e09e4f36f97a7))
* extract the host composition into NjordHost ([3017203](https://github.com/st0o0/njord/commit/30172039fcdf578ae8718f31d67cfe1ca07b6876))
* finish align-ioptions-with-funkarr, archive it ([c55c6b5](https://github.com/st0o0/njord/commit/c55c6b53f645f90a1dd9699015a2cc6815920691))
* FunkArr-level options owned by their libraries ([6887d6f](https://github.com/st0o0/njord/commit/6887d6f3e3fcc12ca23a32a534cb53feefe25ae3))
* give compute immutable settings records instead of options ([def081d](https://github.com/st0o0/njord/commit/def081dff1344152cb4cbb52dedb891ed4567f7b))
* grpc-consumer redesign, healthcheck ask-pattern, options defaults on properties ([966a9ab](https://github.com/st0o0/njord/commit/966a9ab1d016d78ce4372d082c9efb1e693176d1))
* immortal pipeline hubs - eliminate StreamConsumerActor, resume-all supervision, one-shot consumers ([e4f3b84](https://github.com/st0o0/njord/commit/e4f3b84289dcafb80033d8c9b2245c4ccee4b593))
* implement gateway-pipeline-redesign - extract OpenMeteoGateway, Budget project, rename actors to FunkArr-style ([be2299b](https://github.com/st0o0/njord/commit/be2299b7e637dc6379bff77df3d7e4ca9409d72d))
* improve test quality and reduce duplication ([94e7b93](https://github.com/st0o0/njord/commit/94e7b93690a101576a3a3951db6443f529f16e18))
* let analysis features own their options and move configuration orchestration to the host ([fa30b18](https://github.com/st0o0/njord/commit/fa30b18c913b7b9fdbdf1806e305597bc6969ce9))
* make feature config section names private ([64080f3](https://github.com/st0o0/njord/commit/64080f37748604d6e2f3232b47958318d4c8dc2b))
* make Models and Horizons optional in post-consensus architecture ([a63ad4d](https://github.com/st0o0/njord/commit/a63ad4da9821ac30a7d08958c6bc0e959f443183))
* make Njord.Messages the Akka-free contract project ([764072b](https://github.com/st0o0/njord/commit/764072b5286a99f66bd2aa741ff9876c186b8382))
* make point fields nullable, add HasFields validation ([560d040](https://github.com/st0o0/njord/commit/560d04051f78a416dd51cc50babbac0c4f4aab05))
* make the gRPC API a pure ask-and-map adapter ([0759022](https://github.com/st0o0/njord/commit/0759022749a634d9ff64188235dde855307c2497))
* merge Analysis and Options from Domain into Core, add solution folders ([867c4f5](https://github.com/st0o0/njord/commit/867c4f521135fa847340a0795d811ad3b95e2825))
* move actor plumbing to owners and MappingExtensions naming ([f0e10cb](https://github.com/st0o0/njord/commit/f0e10cb5e633639d364f32af48ae184a84bd2e8c))
* move all MQTT-specific code into Njord.Mqtt and replace name-based enum conversions with explicit switches ([caafdfa](https://github.com/st0o0/njord/commit/caafdfab9c630ced6a7ffbe4d83255db20457a8e))
* move analysis contracts into Njord.Messages folders and rename Njord.Compute to Njord.Consensus ([0774501](https://github.com/st0o0/njord/commit/07745012c2a3efd0c79bc500aa759c096653c929))
* move derived and trend computation into their feature libraries ([96acf63](https://github.com/st0o0/njord/commit/96acf638ffe88208d03f3568a64db6593244328e))
* move event, Open-Meteo series and log level mapping out of actors and the client into mapping classes ([d00391f](https://github.com/st0o0/njord/commit/d00391fcf753502101fa0ac389662aa3f7078160))
* move gRPC read model into the pipeline ([5413b4a](https://github.com/st0o0/njord/commit/5413b4a8d8ce472a0f92eee42c8139597f75b1ec))
* move index scoring into Njord.Indices ([26c0adc](https://github.com/st0o0/njord/commit/26c0adc8107aa3b283d084a4430abfd3b9c8672b))
* narrow options injection and guard options monitor use ([1750209](https://github.com/st0o0/njord/commit/175020934cc2b2e62aa6c481894effb0b66d6f75))
* per-domain options, thin clients, enrichment-driven variables ([3375618](https://github.com/st0o0/njord/commit/337561809b1d155389ee9ccb7d6ce6fe97620786))
* persist the poll phase as a persisted enum and move persistence mappers out of the state types ([cb6b2a8](https://github.com/st0o0/njord/commit/cb6b2a8686efffff4ea5f351eb09f8487c5b1f9d))
* PollManager immutable state pattern with Apply extensions and immutable PollCycleTracker ([f633109](https://github.com/st0o0/njord/commit/f633109b046ac93a9a0a7184d806ca6750b1725f))
* PollWorker per-domain independent timers, eliminate round concept ([6a2ce3a](https://github.com/st0o0/njord/commit/6a2ce3afe8189ace443ebcd5fd3203f55814ae1d))
* read scalar option defaults from the shipped appsettings.json instead of code initializers ([e16ef51](https://github.com/st0o0/njord/commit/e16ef517e752ff2aa52996077d65aee01f43832e))
* redesign health checks and telemetry with push pattern ([02d9ac4](https://github.com/st0o0/njord/commit/02d9ac424e12bb9a0f34979ef8dec00f1f048c36))
* redesign pipeline with projection groups and domain-based enrichment ([45fef03](https://github.com/st0o0/njord/commit/45fef0393941609a6ea73e84d70f718f9a17ca4d))
* redesign poll worker from per-model to per-location with ensemble support ([c00aacb](https://github.com/st0o0/njord/commit/c00aacbac5ce90ee02de576feb5e35e3a18b6890))
* remove 44 low-value tests (compiler-guaranteed records, redundant enum mappings, library tests) ([b549c43](https://github.com/st0o0/njord/commit/b549c43dda86f0d2507296042c6033d4ba1f1568))
* remove dead code and fix type locations ([c39d784](https://github.com/st0o0/njord/commit/c39d784dd757e40dcb01dad8abe068845140732f))
* remove dead code, unused packages, and fix stale references after pipeline refactors ([923b228](https://github.com/st0o0/njord/commit/923b22825926c0942eba53a79fef3ae98d36ae62))
* remove EgressActor hub and replace with producer-owned BroadcastHubs ([40ee588](https://github.com/st0o0/njord/commit/40ee5886a5121730076d37ca54594c0809f83b6d))
* remove EnergyDemand, clean PollWorker, hardcode client URLs ([3455af0](https://github.com/st0o0/njord/commit/3455af031386e351f938b2dba4f87c1e9d291d35))
* remove geocoding and climate change APIs ([65faf8a](https://github.com/st0o0/njord/commit/65faf8a926f12b187541bcf5b15447e4abca1e5a))
* remove legacy consensus, derived, trends, indices, and forecast projects ([040e677](https://github.com/st0o0/njord/commit/040e6770133dadeb70e647edbdd096d0bbedd852))
* remove legacy domain handler and alert option system ([76a1632](https://github.com/st0o0/njord/commit/76a1632a03ca32860ed71cd66d1bb2bff9bca9b9))
* remove Njord.Enrichment, Njord.Egress, presenters, and MQTT discovery ([84f5fee](https://github.com/st0o0/njord/commit/84f5feef919d434aa8f8578bc1689b8a35d8adb2))
* Remove Njord.IntegrationTests directory ([9405893](https://github.com/st0o0/njord/commit/940589374e87fd3209045944c878cfe337d95b65))
* remove Njord.Mqtt project ([8248c58](https://github.com/st0o0/njord/commit/8248c5831bf6c3fb550c9f0b869ec8e8bd86384f))
* remove runtime config management system ([5d8b9ae](https://github.com/st0o0/njord/commit/5d8b9aed4910ebb8ef53dd828cd5ff7df5e3612a))
* remove sensor hub and indoor sensor input ([a45be5e](https://github.com/st0o0/njord/commit/a45be5ea1362fb0730512ee10094024e3bf41d36))
* remove the unused Ack message and describe the final consumer shape ([0e31a16](https://github.com/st0o0/njord/commit/0e31a16ea91723fff59587367120063b3ad7c082))
* remove unused usings and tidy style ([dadaed7](https://github.com/st0o0/njord/commit/dadaed7a3ac4b3c22701d39cf322e0b9a9832fea))
* rename EgressEvent to PipelineEvent with typed subtypes ([39b0dec](https://github.com/st0o0/njord/commit/39b0dec5a3a68d904fffbe2a97b0bb4e6b4fc60f))
* rename Njord.Model to Njord.Forecast ([245dcc7](https://github.com/st0o0/njord/commit/245dcc781951fd039f67061b082c37e317fe757f))
* rename Param enums to plural (Sonar S2344) ([ce34c59](https://github.com/st0o0/njord/commit/ce34c59f7b9d037dcfaf8673fc859dda3e737270))
* reorganize handler projects by OutputKind pattern ([f048e8e](https://github.com/st0o0/njord/commit/f048e8e3d6aa57369361dfe2cdf9953270e98cab))
* replace AnalysisFixture with DomainForecast builder ([43c9b3e](https://github.com/st0o0/njord/commit/43c9b3e012e59b8c8fc84f1e26f082ca150ceb82))
* replace ConfigKey and EnrichmentOptions with FeatureDescriptor and FeatureOptions ([1628bba](https://github.com/st0o0/njord/commit/1628bbadb4a5224c1b35019df9134bd5eeed2fd0))
* replace domain handlers with FeatureRule and FeatureEngine ([d473ff2](https://github.com/st0o0/njord/commit/d473ff2d335186b4d128ebb136765bf743828945))
* replace EnabledDomains with enrichment-driven config ([d3e131e](https://github.com/st0o0/njord/commit/d3e131e9535e8312f27ad1a85476f1aeb818def2))
* replace enrichment framework with simple IDomainHandler pattern ([b5c033c](https://github.com/st0o0/njord/commit/b5c033c2b14424f24e7d34492030e40ffe1c177e))
* replace FeatureKind/AlertType/LifestyleKind with OutputKind and feature names ([ac7c1ec](https://github.com/st0o0/njord/commit/ac7c1ec837d8f7164f7f32bf01b4136d73347229))
* replace flat DayScoreSet with dictionary keyed by IndexKind ([8ecac91](https://github.com/st0o0/njord/commit/8ecac91c606344602be39d107925fc3d7e73f9a5))
* replace LocationId with Location throughout codebase ([daac52e](https://github.com/st0o0/njord/commit/daac52e5ae14ba9bea2cfcb7af6b0af3cbc55ba1))
* replace monolithic setup with domain-specific Servus containers and shard snapshot actors ([abcf1ee](https://github.com/st0o0/njord/commit/abcf1ee8886a83240828a584f330b15e6f96bb90))
* replace RetryBackoff with Servus BackoffPolicy ([f7c4c17](https://github.com/st0o0/njord/commit/f7c4c1756325953d969a2cd574e9dc91d671cefd))
* replace the poll scheduler with a poll manager and persistent per-target poll workers ([a4ae165](https://github.com/st0o0/njord/commit/a4ae1657d06d5b708c91093d57f934c7d1e6296c))
* replace the shared health monitor by feature-owned health state and checks ([0c5ce27](https://github.com/st0o0/njord/commit/0c5ce27ff2c9ffb7c2af128c8f85ae0672b5fd2a))
* replace Triggers with DomainRequirements on IDomainHandler ([050cdd6](https://github.com/st0o0/njord/commit/050cdd6a440db3d41786b2ce4362c13b6b049105))
* replace untyped analysis pipeline with typed PipelineEvent subtypes and remove History feature ([dcafe45](https://github.com/st0o0/njord/commit/dcafe45565ffec2a9fcc0698480c1922c66b73f5))
* reserve Snapshot for persisted actor state and name contract types after what they hold ([b275e1f](https://github.com/st0o0/njord/commit/b275e1f357962257e8f62ddd6043361849a98e8b))
* return a single result from IFeatureRule.Execute ([e715949](https://github.com/st0o0/njord/commit/e71594999f8e132642e812ecb627542bee82ef51))
* rewrite gateway with thick clients (FunkArr pattern) ([c4ecee4](https://github.com/st0o0/njord/commit/c4ecee497060c75fa94e13aba7fe7916e8887f7c))
* rewrite OpenMeteo with clean API models and generic fetch ([2116925](https://github.com/st0o0/njord/commit/21169259bbbbf924530b9bb22c8eabe025dc2015))
* rewrite OpenMeteo with typed per-domain clients and response models ([45b0e51](https://github.com/st0o0/njord/commit/45b0e51299b19d4505b01c4817b621566409d4e0))
* rewrite PollWorker with single per-domain state layer ([97b0617](https://github.com/st0o0/njord/commit/97b0617b0a48b682aa67e51f934e8be5b49f778f))
* rewrite PollWorker with typed events, no object, no PersistAll ([d6ad6c8](https://github.com/st0o0/njord/commit/d6ad6c8c0b04d1383f9cef64df1f830c1c1a65e1))
* route GetForecast through GrpcConsumerActor and remove ForecastSnapshotActor ([f16d67d](https://github.com/st0o0/njord/commit/f16d67dcf3da75fe90962118ecb3e98508b7ac4d))
* scope Verify to Persistence + Grpc only, remove from 5 test projects ([850090c](https://github.com/st0o0/njord/commit/850090c2c38bbb0ad501f0eab52146c610e179ba))
* simplify config - remove Models, Horizons, Parameters, internalize weather variables ([846552d](https://github.com/st0o0/njord/commit/846552d2da005e9b64fa10763f1fa5b97a59fcb4))
* simplify gateway with generic domain routing and one-shot attach ([2707b32](https://github.com/st0o0/njord/commit/2707b32fbf736a0fac58565da4619cb164bcc96c))
* simplify health checks with direct static state ([67e4b72](https://github.com/st0o0/njord/commit/67e4b72005bcedb567f073ba37aafc26b9031fa8))
* Simplify imports and modernize List creation ([d2da668](https://github.com/st0o0/njord/commit/d2da6681762f0b8bdbccb8791eae2a442ded25e1))
* simplify mappers to pure field assignment, extract trimming ([c11ff7b](https://github.com/st0o0/njord/commit/c11ff7b7081ea18dfa6255243c3dd24dd0259839))
* simplify Model project to pure vocabulary types ([9acc81a](https://github.com/st0o0/njord/commit/9acc81aff49258ad8488f6ac46eaccff5cc61cdb))
* simplify persistence records - positional style, DateTimeOffset, drop JsonProperty ([d655aea](https://github.com/st0o0/njord/commit/d655aeaf07176f6719b1093395fc8016bc9718de))
* simplify Scheduler and Pipeline actor init to sync resolution ([ce65e70](https://github.com/st0o0/njord/commit/ce65e70f6be57bb6235c581c36fee6c8779e42ee))
* Simplify state updates and remove unused imports ([9aec82f](https://github.com/st0o0/njord/commit/9aec82fbede430860b6452f652872d3be86c972e))
* split alert domains into per-domain projects ([74b3e17](https://github.com/st0o0/njord/commit/74b3e17ae40055652d5e7c82409ef340f30de1b1))
* split alert evaluation into one rule per alert type ([9b60b9b](https://github.com/st0o0/njord/commit/9b60b9bc2f75e8983ba5f741c94a9f70e55637be))
* split god-specs into focused test files following FunkArr pattern ([d3063f4](https://github.com/st0o0/njord/commit/d3063f4d457979157871560417716f3dad6710b8))
* split Njord.Weather into Njord.Model and Njord.Forecast ([a60864f](https://github.com/st0o0/njord/commit/a60864f5d83a4ffac57cda1c8e13cee8c37f33d7))
* split polling, Open-Meteo and storage options into their own sections and register options through one helper ([59421ea](https://github.com/st0o0/njord/commit/59421ea9b5dee957afc9551e3c6c89522ecb8244))
* split tests into per-library test projects ([37ac890](https://github.com/st0o0/njord/commit/37ac89016192bbab04fc7dc635b3bec585932413))
* switch actor registration from WithActors to WithSingleton ([3b447a1](https://github.com/st0o0/njord/commit/3b447a1f03a2e98183f093a76c3a786ec92e5070))
* **tests:** remove dead test infrastructure and redundant wiring tests ([3b5ee10](https://github.com/st0o0/njord/commit/3b5ee1066f3097f492cd3d4a7b6c110be2abbf01))
* type alert details and severity thresholds ([239a55a](https://github.com/st0o0/njord/commit/239a55a93d461ca12339945a2e5eaa3a1edcebe4))
* type analysis horizons ([aba6560](https://github.com/st0o0/njord/commit/aba6560355cd69cd2582c1984140847effeeae34))
* type categorical analysis results ([92f8318](https://github.com/st0o0/njord/commit/92f83189f4b6d64e25829c388409197ce6ebf59c))
* type index kinds and locations in analysis results ([5220194](https://github.com/st0o0/njord/commit/522019498abcf98baaa5625405be83823b904120))
* typed feature results and proto oneof on the wire ([a911210](https://github.com/st0o0/njord/commit/a91121020c2a6296d765c04ee2d1672c5aaa3b73))
* unify alert type hierarchies into single AlertType enum ([b5ac9c1](https://github.com/st0o0/njord/commit/b5ac9c18584f01e93738159cf28e7f6ae6b6be4b))
* unify mappings into per-domain static extension classes and split the core setup container ([2977df5](https://github.com/st0o0/njord/commit/2977df5442ae1ac1cc291013809977adf2f0b8a2))
* update gRPC and MQTT consumers for domain-based pipeline events ([0ae9d80](https://github.com/st0o0/njord/commit/0ae9d8026ef72e15aa75f87e724a29036842e5a3))
* use instance request ids in OpenMeteoGateway ([0da4e5f](https://github.com/st0o0/njord/commit/0da4e5fa4622e92afe0347eb28511a807889b42f))
* use resolved requirements in BudgetCalculator, remove WeatherVariables ([18a0781](https://github.com/st0o0/njord/commit/18a0781a8f34f32dbd0e617d69718501ddd95635))


### Dependencies

* bump Akka.NET packages to 1.5.72 and add cluster sharding ([53bf1bd](https://github.com/st0o0/njord/commit/53bf1bd1025b081cc94042fac69eea6f353a07b1))
