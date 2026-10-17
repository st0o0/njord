# Changelog

## [0.5.0](https://github.com/st0o0/njord/compare/v0.4.1...v0.5.0) (2026-10-17)


### ⚠ BREAKING CHANGES

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

* add ConfigManager for hot config reload ([fbec8bd](https://github.com/st0o0/njord/commit/fbec8bd3b84684449df0edfc5770e1332e339881))
* add IAnalysisStage and wire full analysis pipeline into PipelineActor ([580cea6](https://github.com/st0o0/njord/commit/580cea6090bd177c3d0a2addd367f01c4cb350c5))
* add poll target contracts and the shard message extractor ([862afb5](https://github.com/st0o0/njord/commit/862afb5f2bdac9bb2e20b0e9d16f1cb4efc19dbf))
* add SourceRef consumer architecture for MQTT, gRPC, and pipeline output ([e8fd06e](https://github.com/st0o0/njord/commit/e8fd06ec01b9fa11e27bb4787166d9c50ae04243))
* add the persistent per-target poll worker ([cea4a6b](https://github.com/st0o0/njord/commit/cea4a6beefc0d2ec56bd2d45e223822290254ffd))
* add the poll manager that orchestrates the poll workers ([4ab1d08](https://github.com/st0o0/njord/commit/4ab1d08a5ee9a99528ebe253f7b3497c5dbb3e20))
* add the poll worker state and its persistence records ([820d09d](https://github.com/st0o0/njord/commit/820d09d30955a768a1f0f20b2251a1233fcf169f))
* add vertical feature libraries with typed input interfaces ([acaa2b7](https://github.com/st0o0/njord/commit/acaa2b7e056424e7b3b647eccae28bb22088a50e))
* answer config and budget queries and commands as messages ([447b24b](https://github.com/st0o0/njord/commit/447b24b06a496eb5222c2d5f331fdc0c7beff6c3))
* gateway answers fetches request/response and feeds the pipeline's stable input hub via SinkRef ([710398d](https://github.com/st0o0/njord/commit/710398d1d4d3b1d7348dd9edcbba239bf1d28513))
* let a started pipeline fetch the current analysis settings and sensor map ([c03e54c](https://github.com/st0o0/njord/commit/c03e54c88b3a4912475d27cc9cc1d9c67ecf84c8))
* pass analysis settings per computation through a pipeline side channel ([8a931e4](https://github.com/st0o0/njord/commit/8a931e4ac315f16e0ffac79185ae077210163f3a))
* publish AnalysisResult events as MQTT state in MqttStateActor ([923e58b](https://github.com/st0o0/njord/commit/923e58be0b4ec29b2eaf755aecdd97e9bd72a358))
* sensor hub pushes snapshot maps of all fresh locations to the pipeline ([2b92477](https://github.com/st0o0/njord/commit/2b92477d0844a6d7759727725c517c235763671e))


### Bug Fixes

* adapt to Testcontainers 4.15.0 API and fix gRPC proto codegen warnings ([33cb478](https://github.com/st0o0/njord/commit/33cb4782eebdc75501772fa4ab7a65a16cd9baa9))
* add failure handlers to all PipeTo dependency-resolution calls ([80b0be8](https://github.com/st0o0/njord/commit/80b0be83531ee669661ee57464059b35eee4da18))
* answer every gateway fetch request once and match locations case-insensitively ([4bec681](https://github.com/st0o0/njord/commit/4bec6815b4b5813c7fe694a9e99bc585ba37e18d))
* complete poll cycles with failed fetches and keep the gateway fetch stream alive ([281f180](https://github.com/st0o0/njord/commit/281f180496db8f4125df6269577d68454162b6e4))
* do not restart poll workers that the shard region stops during shutdown ([8bd3f4b](https://github.com/st0o0/njord/commit/8bd3f4bf8b9312ea40b92f97451dac203a4bd2cf))
* drain the read model stream hub and buffer each streaming client ([80b299e](https://github.com/st0o0/njord/commit/80b299e2cc81b76089a4fa57a83de61a8aa018ce))
* enforce test conventions and remove XML doc comments ([70aad79](https://github.com/st0o0/njord/commit/70aad7952ec9c6cc58648ccd2396cad2f2f5c36e))
* feed live sensor readings of each forecast's location into the indices ([ff306b1](https://github.com/st0o0/njord/commit/ff306b10182a337f22f34970a2f9493cfff74dd6))
* form cluster via SeedNodes to fix singleton proxy startup race ([c34f960](https://github.com/st0o0/njord/commit/c34f96019a3d286a41a62a4aba8c18c76555fbc4))
* give fetch requests a 60 s admission window and tell queue expiry apart from budget rejections ([b34d222](https://github.com/st0o0/njord/commit/b34d222b7fd1e95bf07f8c298cbc305994624a43))
* handle every MQTT connection lifecycle failure as a message instead of restarting the connection actor ([144cd6a](https://github.com/st0o0/njord/commit/144cd6a8e3554b462f536121a0ab13766e084b6b))
* honor the consensus toggle by suppressing consensus output when disabled ([fcaa1af](https://github.com/st0o0/njord/commit/fcaa1af1d4dea1618d5c96740899e54ab2c8f230))
* keep MQTT state publishing alive after a failed publish ([037dec7](https://github.com/st0o0/njord/commit/037dec70ab9945c0cd747c26ec5ca985ce7c7a79))
* log actor events under their actor class and derive the Akka log level from the Serilog minimum levels ([8644015](https://github.com/st0o0/njord/commit/8644015af0ea6361289e313227025161222aa50b))
* reject zero budget, delta-serialize writable options, correct E2E test plan ([85dd311](https://github.com/st0o0/njord/commit/85dd3116cea60238016763275bc6506889bcb342))
* replace ConfigPersistence with WritableNjordOptions, move Kestrel to appsettings ([2cc5531](https://github.com/st0o0/njord/commit/2cc55314da2d4ec2453062e6e1dbf5c1a86c5fa7))
* retry polls rejected while the gateway attaches to the pipeline after five seconds ([e451df4](https://github.com/st0o0/njord/commit/e451df4ff99d5be2b62cc3b06019880e36cf9aab))
* return every enrichment kind from GetEnrichments and filter enrichment streams by location ([0aea6b2](https://github.com/st0o0/njord/commit/0aea6b20f065d0057ddbad59840777f99f18afc0))
* rewire stream consumers and the analysis pipeline when their graph completes or fails ([31913b6](https://github.com/st0o0/njord/commit/31913b6a677a12c454658e0d727b7f7697613663))
* size the gateway fetch queue for a full startup round and log each analyzed forecast ([68b5bb1](https://github.com/st0o0/njord/commit/68b5bb19b680cb7d9ef426b6eeb56e1349e5df38))
* stabilize flaky egress and architecture tests ([6d47876](https://github.com/st0o0/njord/commit/6d47876014a1fd47ebb2f6e1c1fb9381620a82bc))
* stabilize specs, complete streams on shutdown, restore slopwatch ([9d40a9d](https://github.com/st0o0/njord/commit/9d40a9d2ef82dfab7f40cf6f8143f1460c0e90ee))
* start with a corrupt config override file and keep the last good values on reload ([1fbcfed](https://github.com/st0o0/njord/commit/1fbcfed007c48267c6610b247ecaaf10ae430d70))
* **tests:** apply whitespace formatting and remove stale pipeline init race test ([d6a84dc](https://github.com/st0o0/njord/commit/d6a84dc0bba56d9c513c4b9daa46e3cb93f8af77))


### Documentation

* remove em dashes and simplify wording ([6e84b9e](https://github.com/st0o0/njord/commit/6e84b9eba254e745e5f56f503986f77995007308))
* remove MQTT discovery and history from user docs, examples and diagrams ([0499617](https://github.com/st0o0/njord/commit/04996171470e39be25c2b5d0d42e508a85785dd6))


### Refactoring

* actors inject sub-options directly instead of NjordOptions ([9cf6a20](https://github.com/st0o0/njord/commit/9cf6a205dcfaba0235e40c434ab5183e2bbc34fd))
* add consensus query vocabulary and split Compute into Consensus and Contracts ([98b829c](https://github.com/st0o0/njord/commit/98b829cc84e9ffc24b6f2425f506f5dbf976a483))
* add shared TestOptionsMonitor and replace duplicated fakes ([beaab8c](https://github.com/st0o0/njord/commit/beaab8c83315296ac28f634104281cb6ceb78cfd))
* add sync init path to StreamConsumerActor and all subclasses ([3b50395](https://github.com/st0o0/njord/commit/3b50395a3d6ba3005a52f1e6162bc8a86ff98408))
* align actor state pattern with FunkArr conventions ([7e6d02b](https://github.com/st0o0/njord/commit/7e6d02b8a180f5fe541789c1ad5ee8f593ed6914))
* align codebase with FunkArr conventions - messages, persistence, logging, naming ([9784f95](https://github.com/st0o0/njord/commit/9784f95eb06c0bf6b6d851eaa3bf756e3da5ab5f))
* align message response naming to QueryNounResponse convention ([f89e9e5](https://github.com/st0o0/njord/commit/f89e9e53daf92287b1cae302bb54643299be292f))
* attach pipeline consumers only on start, pipeline termination or own graph loss ([8ae6509](https://github.com/st0o0/njord/commit/8ae65091db65110945859a73204ab3073ae03d3e))
* bind one options type per section ([f58481b](https://github.com/st0o0/njord/commit/f58481b312a664df8141e223f263aa43c5f768c9))
* clean options DI - bind enrichment sub-options, use IOptions&lt;T&gt; in analyzers and MqttNetPublisher ([42c27aa](https://github.com/st0o0/njord/commit/42c27aabf41f0445cdbc482ce660ac43204f8edc))
* consolidate compute logic into feature libs, rename DTOs to Persisted* prefix ([2f3d62d](https://github.com/st0o0/njord/commit/2f3d62d7a8ccd1c0354e6b65b5144df7bd668879))
* core cleanup and convention hygiene - relocate helpers, typed constants, FunkArr persistence alignment ([6b9382d](https://github.com/st0o0/njord/commit/6b9382d5d2878b962cc86c18d7067bfdd72b6c6e))
* dissolve Njord.Tests into IntegrationTests and domain projects ([8e57f84](https://github.com/st0o0/njord/commit/8e57f840315944ed47c0aef548d2e1ed71e50fe6))
* drop dead JSON annotations from analysis records ([2e6e2b6](https://github.com/st0o0/njord/commit/2e6e2b647dd16602373ad96a35dbb0324fef6103))
* enforce zone architecture rules and typed Akka failures ([c0f9738](https://github.com/st0o0/njord/commit/c0f973830d37d0c4f032197b58641b43296402ed))
* extract Domain, Persistence, Messages and Core projects ([ed1754c](https://github.com/st0o0/njord/commit/ed1754c3c2171dbda28a5244d9969ad0a5120855))
* extract Mqtt and Enrichment libraries ([095f823](https://github.com/st0o0/njord/commit/095f823f237d924117c315eba9c57b60683afde8))
* extract Mqtt, Enrichment, Ingest, Sensors test projects ([6ff605f](https://github.com/st0o0/njord/commit/6ff605ff1b8152f167f32148751dcfa760e070d6))
* extract Njord.Compute as pure math library from Njord.Core ([813b56d](https://github.com/st0o0/njord/commit/813b56de31075f29545958678762faa6a11a10de))
* extract Pipeline and Egress libraries ([c4e0d76](https://github.com/st0o0/njord/commit/c4e0d76ffbdfc164090ce9c716de6acdd11c0cc6))
* extract Sensors, Ingest and Grpc libraries ([b59ebbb](https://github.com/st0o0/njord/commit/b59ebbb9e17a7f12d696c3a1326f49a17cee37eb))
* extract sub-options with SectionName and per-feature registration ([3da36de](https://github.com/st0o0/njord/commit/3da36de4a7e19d2f85d94dd0647e09e4f36f97a7))
* finish align-ioptions-with-funkarr, archive it ([c55c6b5](https://github.com/st0o0/njord/commit/c55c6b53f645f90a1dd9699015a2cc6815920691))
* give compute immutable settings records instead of options ([def081d](https://github.com/st0o0/njord/commit/def081dff1344152cb4cbb52dedb891ed4567f7b))
* implement gateway-pipeline-redesign - extract OpenMeteoGateway, Budget project, rename actors to FunkArr-style ([be2299b](https://github.com/st0o0/njord/commit/be2299b7e637dc6379bff77df3d7e4ca9409d72d))
* make Njord.Messages the Akka-free contract project ([764072b](https://github.com/st0o0/njord/commit/764072b5286a99f66bd2aa741ff9876c186b8382))
* make the gRPC API a pure ask-and-map adapter ([0759022](https://github.com/st0o0/njord/commit/0759022749a634d9ff64188235dde855307c2497))
* merge Analysis and Options from Domain into Core, add solution folders ([867c4f5](https://github.com/st0o0/njord/commit/867c4f521135fa847340a0795d811ad3b95e2825))
* move derived and trend computation into their feature libraries ([96acf63](https://github.com/st0o0/njord/commit/96acf638ffe88208d03f3568a64db6593244328e))
* move gRPC read model into the pipeline ([5413b4a](https://github.com/st0o0/njord/commit/5413b4a8d8ce472a0f92eee42c8139597f75b1ec))
* move index scoring into Njord.Indices ([26c0adc](https://github.com/st0o0/njord/commit/26c0adc8107aa3b283d084a4430abfd3b9c8672b))
* narrow options injection and guard options monitor use ([1750209](https://github.com/st0o0/njord/commit/175020934cc2b2e62aa6c481894effb0b66d6f75))
* remove dead code, unused packages, and fix stale references after pipeline refactors ([923b228](https://github.com/st0o0/njord/commit/923b22825926c0942eba53a79fef3ae98d36ae62))
* remove EgressActor hub and replace with producer-owned BroadcastHubs ([40ee588](https://github.com/st0o0/njord/commit/40ee5886a5121730076d37ca54594c0809f83b6d))
* remove Njord.Enrichment, Njord.Egress, presenters, and MQTT discovery ([84f5fee](https://github.com/st0o0/njord/commit/84f5feef919d434aa8f8578bc1689b8a35d8adb2))
* Remove Njord.IntegrationTests directory ([9405893](https://github.com/st0o0/njord/commit/940589374e87fd3209045944c878cfe337d95b65))
* remove the unused Ack message and describe the final consumer shape ([0e31a16](https://github.com/st0o0/njord/commit/0e31a16ea91723fff59587367120063b3ad7c082))
* rename EgressEvent to PipelineEvent with typed subtypes ([39b0dec](https://github.com/st0o0/njord/commit/39b0dec5a3a68d904fffbe2a97b0bb4e6b4fc60f))
* replace monolithic setup with domain-specific Servus containers and shard snapshot actors ([abcf1ee](https://github.com/st0o0/njord/commit/abcf1ee8886a83240828a584f330b15e6f96bb90))
* replace RetryBackoff with Servus BackoffPolicy ([f7c4c17](https://github.com/st0o0/njord/commit/f7c4c1756325953d969a2cd574e9dc91d671cefd))
* replace the poll scheduler with a poll manager and persistent per-target poll workers ([a4ae165](https://github.com/st0o0/njord/commit/a4ae1657d06d5b708c91093d57f934c7d1e6296c))
* replace untyped analysis pipeline with typed PipelineEvent subtypes and remove History feature ([dcafe45](https://github.com/st0o0/njord/commit/dcafe45565ffec2a9fcc0698480c1922c66b73f5))
* route GetForecast through GrpcConsumerActor and remove ForecastSnapshotActor ([f16d67d](https://github.com/st0o0/njord/commit/f16d67dcf3da75fe90962118ecb3e98508b7ac4d))
* Simplify imports and modernize List creation ([d2da668](https://github.com/st0o0/njord/commit/d2da6681762f0b8bdbccb8791eae2a442ded25e1))
* simplify persistence records - positional style, DateTimeOffset, drop JsonProperty ([d655aea](https://github.com/st0o0/njord/commit/d655aeaf07176f6719b1093395fc8016bc9718de))
* simplify Scheduler and Pipeline actor init to sync resolution ([ce65e70](https://github.com/st0o0/njord/commit/ce65e70f6be57bb6235c581c36fee6c8779e42ee))
* split alert evaluation into one rule per alert type ([9b60b9b](https://github.com/st0o0/njord/commit/9b60b9bc2f75e8983ba5f741c94a9f70e55637be))
* split tests into per-library test projects ([37ac890](https://github.com/st0o0/njord/commit/37ac89016192bbab04fc7dc635b3bec585932413))
* switch actor registration from WithActors to WithSingleton ([3b447a1](https://github.com/st0o0/njord/commit/3b447a1f03a2e98183f093a76c3a786ec92e5070))
* **tests:** remove dead test infrastructure and redundant wiring tests ([3b5ee10](https://github.com/st0o0/njord/commit/3b5ee1066f3097f492cd3d4a7b6c110be2abbf01))
* type alert details and severity thresholds ([239a55a](https://github.com/st0o0/njord/commit/239a55a93d461ca12339945a2e5eaa3a1edcebe4))
* type analysis horizons ([aba6560](https://github.com/st0o0/njord/commit/aba6560355cd69cd2582c1984140847effeeae34))
* type categorical analysis results ([92f8318](https://github.com/st0o0/njord/commit/92f83189f4b6d64e25829c388409197ce6ebf59c))
* type index kinds and locations in analysis results ([5220194](https://github.com/st0o0/njord/commit/522019498abcf98baaa5625405be83823b904120))


### Dependencies

* bump Akka.NET packages to 1.5.72 and add cluster sharding ([53bf1bd](https://github.com/st0o0/njord/commit/53bf1bd1025b081cc94042fac69eea6f353a07b1))
