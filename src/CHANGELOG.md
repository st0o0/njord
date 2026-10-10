# Changelog

## [0.5.0](https://github.com/st0o0/njord/compare/v0.4.1...v0.5.0) (2026-10-10)


### ⚠ BREAKING CHANGES

* replace ConfigPersistence with WritableNjordOptions, move Kestrel to appsettings
* replace monolithic setup with domain-specific Servus containers and shard snapshot actors
* dissolve Njord.Tests into IntegrationTests and domain projects
* remove EgressActor hub and replace with producer-owned BroadcastHubs
* merge Analysis and Options from Domain into Core, add solution folders
* align message response naming to QueryNounResponse convention
* persistence DTO types moved to Njord.Persistence assembly; existing persisted scheduler/budget/snapshot data may need to be reset.

### Bug Fixes

* adapt to Testcontainers 4.15.0 API and fix gRPC proto codegen warnings ([33cb478](https://github.com/st0o0/njord/commit/33cb4782eebdc75501772fa4ab7a65a16cd9baa9))
* add failure handlers to all PipeTo dependency-resolution calls ([80b0be8](https://github.com/st0o0/njord/commit/80b0be83531ee669661ee57464059b35eee4da18))
* enforce test conventions and remove XML doc comments ([70aad79](https://github.com/st0o0/njord/commit/70aad7952ec9c6cc58648ccd2396cad2f2f5c36e))
* form cluster via SeedNodes to fix singleton proxy startup race ([c34f960](https://github.com/st0o0/njord/commit/c34f96019a3d286a41a62a4aba8c18c76555fbc4))
* reject zero budget, delta-serialize writable options, correct E2E test plan ([85dd311](https://github.com/st0o0/njord/commit/85dd3116cea60238016763275bc6506889bcb342))
* replace ConfigPersistence with WritableNjordOptions, move Kestrel to appsettings ([2cc5531](https://github.com/st0o0/njord/commit/2cc55314da2d4ec2453062e6e1dbf5c1a86c5fa7))
* stabilize flaky egress and architecture tests ([6d47876](https://github.com/st0o0/njord/commit/6d47876014a1fd47ebb2f6e1c1fb9381620a82bc))
* stabilize specs, complete streams on shutdown, restore slopwatch ([9d40a9d](https://github.com/st0o0/njord/commit/9d40a9d2ef82dfab7f40cf6f8143f1460c0e90ee))
* **tests:** apply whitespace formatting and remove stale pipeline init race test ([d6a84dc](https://github.com/st0o0/njord/commit/d6a84dc0bba56d9c513c4b9daa46e3cb93f8af77))


### Refactoring

* actors inject sub-options directly instead of NjordOptions ([9cf6a20](https://github.com/st0o0/njord/commit/9cf6a205dcfaba0235e40c434ab5183e2bbc34fd))
* add shared TestOptionsMonitor and replace duplicated fakes ([beaab8c](https://github.com/st0o0/njord/commit/beaab8c83315296ac28f634104281cb6ceb78cfd))
* add sync init path to StreamConsumerActor and all subclasses ([3b50395](https://github.com/st0o0/njord/commit/3b50395a3d6ba3005a52f1e6162bc8a86ff98408))
* align message response naming to QueryNounResponse convention ([f89e9e5](https://github.com/st0o0/njord/commit/f89e9e53daf92287b1cae302bb54643299be292f))
* dissolve Njord.Tests into IntegrationTests and domain projects ([8e57f84](https://github.com/st0o0/njord/commit/8e57f840315944ed47c0aef548d2e1ed71e50fe6))
* enforce zone architecture rules and typed Akka failures ([c0f9738](https://github.com/st0o0/njord/commit/c0f973830d37d0c4f032197b58641b43296402ed))
* extract Domain, Persistence, Messages and Core projects ([ed1754c](https://github.com/st0o0/njord/commit/ed1754c3c2171dbda28a5244d9969ad0a5120855))
* extract Mqtt and Enrichment libraries ([095f823](https://github.com/st0o0/njord/commit/095f823f237d924117c315eba9c57b60683afde8))
* extract Mqtt, Enrichment, Ingest, Sensors test projects ([6ff605f](https://github.com/st0o0/njord/commit/6ff605ff1b8152f167f32148751dcfa760e070d6))
* extract Njord.Compute as pure math library from Njord.Core ([813b56d](https://github.com/st0o0/njord/commit/813b56de31075f29545958678762faa6a11a10de))
* extract Pipeline and Egress libraries ([c4e0d76](https://github.com/st0o0/njord/commit/c4e0d76ffbdfc164090ce9c716de6acdd11c0cc6))
* extract Sensors, Ingest and Grpc libraries ([b59ebbb](https://github.com/st0o0/njord/commit/b59ebbb9e17a7f12d696c3a1326f49a17cee37eb))
* extract sub-options with SectionName and per-feature registration ([3da36de](https://github.com/st0o0/njord/commit/3da36de4a7e19d2f85d94dd0647e09e4f36f97a7))
* finish align-ioptions-with-funkarr, archive it ([c55c6b5](https://github.com/st0o0/njord/commit/c55c6b53f645f90a1dd9699015a2cc6815920691))
* merge Analysis and Options from Domain into Core, add solution folders ([867c4f5](https://github.com/st0o0/njord/commit/867c4f521135fa847340a0795d811ad3b95e2825))
* remove EgressActor hub and replace with producer-owned BroadcastHubs ([40ee588](https://github.com/st0o0/njord/commit/40ee5886a5121730076d37ca54594c0809f83b6d))
* Remove Njord.IntegrationTests directory ([9405893](https://github.com/st0o0/njord/commit/940589374e87fd3209045944c878cfe337d95b65))
* replace monolithic setup with domain-specific Servus containers and shard snapshot actors ([abcf1ee](https://github.com/st0o0/njord/commit/abcf1ee8886a83240828a584f330b15e6f96bb90))
* replace RetryBackoff with Servus BackoffPolicy ([f7c4c17](https://github.com/st0o0/njord/commit/f7c4c1756325953d969a2cd574e9dc91d671cefd))
* simplify Scheduler and Pipeline actor init to sync resolution ([ce65e70](https://github.com/st0o0/njord/commit/ce65e70f6be57bb6235c581c36fee6c8779e42ee))
* split tests into per-library test projects ([37ac890](https://github.com/st0o0/njord/commit/37ac89016192bbab04fc7dc635b3bec585932413))
* switch actor registration from WithActors to WithSingleton ([3b447a1](https://github.com/st0o0/njord/commit/3b447a1f03a2e98183f093a76c3a786ec92e5070))
* **tests:** remove dead test infrastructure and redundant wiring tests ([3b5ee10](https://github.com/st0o0/njord/commit/3b5ee1066f3097f492cd3d4a7b6c110be2abbf01))


### Dependencies

* bump Akka.NET packages to 1.5.72 and add cluster sharding ([53bf1bd](https://github.com/st0o0/njord/commit/53bf1bd1025b081cc94042fac69eea6f353a07b1))
