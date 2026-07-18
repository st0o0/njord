# Changelog

## 0.1.0 (2026-07-18)


### ⚠ BREAKING CHANGES

* expand forecast parameters from closed 9-enum to full Open-Meteo registry
* switch ingest from Kachelmann to keyless Open-Meteo

* reset version manifest for initial release ([b19c209](https://github.com/st0o0/njord/commit/b19c209f3a363c582e9727c47578bfd9b5b73003))


### Features

* adaptive per-model poll scheduling with Akka.Persistence ([bc22040](https://github.com/st0o0/njord/commit/bc220402fee98414301f94ff5b746c90f97c516f))
* add /healthz endpoint spec and integration test ([e24b3aa](https://github.com/st0o0/njord/commit/e24b3aab2f9e986231eff9b4072cd0d18f00e910))
* add building energy management consumer (M6) ([bcf6760](https://github.com/st0o0/njord/commit/bcf6760ba1463a100c0c674ace1174fcd6f36688))
* add daily-life activity indices consumer (M5) ([cc1de60](https://github.com/st0o0/njord/commit/cc1de60a97f25e6686fcc5ca8e9c7c8533b2ab7f))
* add derived meteorological values consumer (M3) ([8747dc4](https://github.com/st0o0/njord/commit/8747dc4ddca086f3069edff2b7edb90e0eb1e71b))
* add documentation and configuration builder ([77b2437](https://github.com/st0o0/njord/commit/77b24378540ac5d5b5b46abafadffd48d4a14830))
* add enrichment pipeline infrastructure and consensus (M0+M1) ([068a837](https://github.com/st0o0/njord/commit/068a8377c9e46374b9be616d20f61f8e322d2780))
* add gRPC API with forecast/config services and snapshot actors ([574bfce](https://github.com/st0o0/njord/commit/574bfce71223d1cb3afb5a420b7cf7abedd4ad9f))
* add historical learning consumer with Akka.Persistence (M7) ([ec3553a](https://github.com/st0o0/njord/commit/ec3553a5cb34f7d10d3786e94d13644c0b099d02))
* add Kachelmann ingest foundation ([baabcb9](https://github.com/st0o0/njord/commit/baabcb906ff592be5f844ad7425628896ab9da72))
* add publisher-agnostic EgressActor with registration protocol ([70aea85](https://github.com/st0o0/njord/commit/70aea85dffa305396b872513317014bbbb05865a))
* add telemetry infrastructure with Serilog, OpenTelemetry, and health checks ([aac787d](https://github.com/st0o0/njord/commit/aac787db8b7f08e968bfbc9cf4ba03ac1655a116))
* add temporal trend analysis consumer (M4) ([b6d9a57](https://github.com/st0o0/njord/commit/b6d9a57539f9412c0d7b3d84db90c65ac74f2e90))
* add threshold alerts with multi-model confidence (M2) ([11a55d3](https://github.com/st0o0/njord/commit/11a55d3a6089bb4a01b65df46629c56201af993f))
* add TriggerImmediatePoll command to SchedulerActor ([c761986](https://github.com/st0o0/njord/commit/c76198663ec0dde780152ff59d27261402f5274a))
* configurable base URL and daily TimeString unix-to-ISO conversion ([0dfe5c2](https://github.com/st0o0/njord/commit/0dfe5c25fff96f31c3553667f22fc0791dbb67b4))
* **dev:** add Aspire 13 AppHost and docker-compose reference ([9b9ca3c](https://github.com/st0o0/njord/commit/9b9ca3c652115ea18220b536d657a81f6914b2e5))
* **docker:** Add ServiceDefaults to build ([d25282d](https://github.com/st0o0/njord/commit/d25282d595d6eb6da80af3bd9298309f021ce3dc))
* dynamic budget-aware throttle with IBudgetGate abstraction ([6e14e4a](https://github.com/st0o0/njord/commit/6e14e4a722c45b2336588106958a0428341c9083))
* **enrichment:** add data enrichment pipeline ([2050ab0](https://github.com/st0o0/njord/commit/2050ab0450fb71cc8d2e852e58728a09b9a530a2))
* expand forecast parameters from closed 9-enum to full Open-Meteo registry ([3d14ecd](https://github.com/st0o0/njord/commit/3d14ecd5ac37de4d06b2f6c84e6ed04ed87c90cc))
* Introduce LikeC4 diagrams and branding ([ad032ea](https://github.com/st0o0/njord/commit/ad032ea3b709562d6b4e04e0b461aca0478a6c3a))
* make MQTT optional and clean up configuration layering ([255f573](https://github.com/st0o0/njord/commit/255f573481bc8a9fa93fb107ad61c99f488c8a49))
* model capability tracking and capability-driven MQTT discovery ([1613f0d](https://github.com/st0o0/njord/commit/1613f0dee91d51bf1c2109bf6a5e4620b8add64d))
* null-stripping and model coverage capping in HorizonProjection ([2984301](https://github.com/st0o0/njord/commit/29843013996e7304c16494828d7a8b0442a3ce43))
* per-location model config with static coverage validation ([06cf680](https://github.com/st0o0/njord/commit/06cf6802e6d4c79453501c7388b0ed0471263ea4))
* publish per-model forecasts to Home Assistant via MQTT discovery ([ac5badf](https://github.com/st0o0/njord/commit/ac5badfe6236ea1de6d60c9c5105c2e4d1738da2))
* switch ingest from Kachelmann to keyless Open-Meteo ([3e43ea6](https://github.com/st0o0/njord/commit/3e43ea696d0d75566606ed63ba8a7f778544492e))
* switch to WebApplication host with ASP.NET Docker image ([51ea4cd](https://github.com/st0o0/njord/commit/51ea4cdea05bf4e7e92f03f85fee458d9fd329fb))
* unified hourly consensus, snapshot recovery fix, request throttling ([2531a47](https://github.com/st0o0/njord/commit/2531a47775053514a4d934f8b1a02e20654e19ba))


### Bug Fixes

* add missing state_topic to enrichment discovery payloads ([4ed02a5](https://github.com/st0o0/njord/commit/4ed02a5be074122f2ee2295d3c6f240470084b08))
* **ci:** add location and model env vars to smoke test ([0305b14](https://github.com/st0o0/njord/commit/0305b1461c00b08c2534c18d73789ba622be7f57))
* create data directory in build stage for chiseled image ([02dc416](https://github.com/st0o0/njord/commit/02dc416ce73e76e8d6c29e90fb99ca09ac3c7570))
* **docs:** set base path for GitHub Pages subpath deployment ([c91f379](https://github.com/st0o0/njord/commit/c91f379e6c00cbc6610fa238fa79e6716595fda0))
* model coverage registry, daily null filter, per-parameter consensus metadata ([cc629f0](https://github.com/st0o0/njord/commit/cc629f0cb13f850b191920142b713e7e1c610bf1))
* **pipeline:** create fresh StreamRefs per request in PipelineActor ([ba5acac](https://github.com/st0o0/njord/commit/ba5acac5c4e13ebd8799c3ff8aff81c2fcf43ffd))
* **pipeline:** stagger initial polls and limit HTTP concurrency ([b763b77](https://github.com/st0o0/njord/commit/b763b771bcfebeb48d210128039bd3aae682cdd9))
* **pipeline:** tolerate 'undefined' units and reschedule on all failures ([1f0a4ad](https://github.com/st0o0/njord/commit/1f0a4ad3bff690d22e975933897004ab6e803def))
* resolve Aspire startup, SQLite persistence, and runtime bugs ([a83c6cf](https://github.com/st0o0/njord/commit/a83c6cfd85b787d30d8646e9c0e6ba2f2d7198b5))
* resolve critical and medium audit findings across enrichment, discovery, and egress ([dab94b9](https://github.com/st0o0/njord/commit/dab94b9e22eb0d8d943200dfab7366750a06baec))
* **scheduler:** include failure detail in FetchFailed message and logs ([13d1818](https://github.com/st0o0/njord/commit/13d1818d99fec2ee58bf26c5a2746c51162f0389))


### Performance

* batch snapshot persistence and slim history records ([cfeaf32](https://github.com/st0o0/njord/commit/cfeaf32173e0681c742490de4e3f29ed2dfb6adf))
* reduce BroadcastHub buffer sizes and use ImmutableDictionary for ModelSnapshot ([ec54ced](https://github.com/st0o0/njord/commit/ec54ced98e0ed472826075d5715b1c0bbdae7efd))


### Documentation

* add CLAUDE.md and record verified Kachelmann API facts ([98a78e0](https://github.com/st0o0/njord/commit/98a78e05673ff425032ab087899342d4b376928e))
* add specs for gRPC API, config persistence, and supporting features ([9ba0666](https://github.com/st0o0/njord/commit/9ba06661f00d46093d9e50e93f6b164166585a10))
* fix remaining outdated descriptions and add MQTT toggle to builder ([c3624bc](https://github.com/st0o0/njord/commit/c3624bcea636642d5fd6e8445576986c00832d05))
* **openspec:** add dev-setup change artifacts ([b0f600a](https://github.com/st0o0/njord/commit/b0f600a7bd0a8c4df628cda9fc4a584280490b3b))
* **openspec:** add egress-cleanup-and-failure-routing change artifacts ([c2cefa4](https://github.com/st0o0/njord/commit/c2cefa4b718b1d2d2831d69408b70bed41b40602))
* **openspec:** add enrichment-pipeline change artifacts ([305fd6c](https://github.com/st0o0/njord/commit/305fd6c5fe0971d339830fd21da06640e86bc871))
* **openspec:** add m3 derived-values change artifacts ([0dda661](https://github.com/st0o0/njord/commit/0dda661616d53e773c4271c059f60c91f13b2c0f))
* **openspec:** add m4 trend-analysis change artifacts ([5f13365](https://github.com/st0o0/njord/commit/5f133651647eb053dacdeea8ae6b4a0cfb37f415))
* **openspec:** add m5 activity-indices change artifacts ([f87ec15](https://github.com/st0o0/njord/commit/f87ec15e1fb37262899b759b589aa1195f4361c5))
* **openspec:** add m6 energy-management change artifacts ([66a2a5a](https://github.com/st0o0/njord/commit/66a2a5a0743d9ed5fab604582e4029f04b19f10f))
* **openspec:** add m7 historical-learning change artifacts ([b034e05](https://github.com/st0o0/njord/commit/b034e05d7700d0e8cd25561e573e8b007eb7160a))
* **openspec:** add project-restructure change artifacts ([100748c](https://github.com/st0o0/njord/commit/100748c04dcd929a2039a4f122a95dc68cadb4e3))
* **openspec:** archive add-deployment change ([5e77056](https://github.com/st0o0/njord/commit/5e77056ae1f5afd4b07311d36b99c563f4d36509))
* **openspec:** archive repo-housekeeping and ci-optimization changes ([b25d481](https://github.com/st0o0/njord/commit/b25d48194b2d7e398891a6ae80985793fc30782b))
* **openspec:** propose add-kachelmann-ingest change ([26bf8f2](https://github.com/st0o0/njord/commit/26bf8f25baae967cf68190b258260d66836a6eaa))
* **openspec:** propose add-mqtt-egress ([914567d](https://github.com/st0o0/njord/commit/914567dd7460b39218e5d1980b6931cb3ccbd57c))
* **openspec:** propose replace-kachelmann-with-openmeteo ([85786ba](https://github.com/st0o0/njord/commit/85786badd0e0e492fdeaf042233d513c2e9411b9))
* **openspec:** sync specs and archive add-kachelmann-ingest ([cf13ddf](https://github.com/st0o0/njord/commit/cf13ddf9639ad12fefbaf47f6d042b4807fa8cca))
* **openspec:** sync specs and archive add-mqtt-egress ([d709648](https://github.com/st0o0/njord/commit/d709648c774efa55db1fa4f34c6c83a780f14064))
* **openspec:** sync specs and archive cleanup-cycleid-and-dead-code change ([2def600](https://github.com/st0o0/njord/commit/2def600173def77d39da9cc41cbc26aadb2b9fda))
* **openspec:** sync specs and archive dev-setup change ([f994b91](https://github.com/st0o0/njord/commit/f994b91387edf218a902d4dd8cb097e56546b8c4))
* **openspec:** sync specs and archive egress-cleanup-and-failure-routing change ([2ba058c](https://github.com/st0o0/njord/commit/2ba058c5648dd4c85b334180965630241b8f06ea))
* **openspec:** sync specs and archive egress, pipeline, topic-per-horizon, and adaptive-poll-scheduling changes ([7300876](https://github.com/st0o0/njord/commit/730087646a4402fc7334fec5e1c9a2f0a2cbab73))
* **openspec:** sync specs and archive m3 derived-values change ([7c7f1eb](https://github.com/st0o0/njord/commit/7c7f1eb2dcde15f339b3c6490c6f7b4c349fd589))
* **openspec:** sync specs and archive m4 trend-analysis change ([93fe95a](https://github.com/st0o0/njord/commit/93fe95a06535a9a09c447b358288645a228a62fe))
* **openspec:** sync specs and archive m5 activity-indices change ([9e81423](https://github.com/st0o0/njord/commit/9e8142354814205be5f1a77bae609508f5182a0a))
* **openspec:** sync specs and archive m6 energy-management change ([81efa48](https://github.com/st0o0/njord/commit/81efa4859b9fd70412d034cbd9784edadf194c7d))
* **openspec:** sync specs and archive m7 historical-learning change ([3f8759d](https://github.com/st0o0/njord/commit/3f8759dad6df59b64221f5f94078c26012670f85))
* **openspec:** sync specs and archive pipeline-streamref-broadcasthub change ([e638b29](https://github.com/st0o0/njord/commit/e638b29da10e9d49f113cb6195ca2e1cd5fa92d4))
* **openspec:** sync specs and archive postgresql-persistence change ([257f863](https://github.com/st0o0/njord/commit/257f863ba0be055e1fa64af015a54650f30ccc14))
* **openspec:** sync specs and archive project-restructure change ([6ff9330](https://github.com/st0o0/njord/commit/6ff93309d4ef51bc595915a8ae1ebe50bb755604))
* **openspec:** sync specs and archive replace-kachelmann-with-openmeteo ([4055e38](https://github.com/st0o0/njord/commit/4055e384479ccc8282b30d3c1f73d6cb49350f85))
* **openspec:** sync specs and archive servus-integration change ([5fda43a](https://github.com/st0o0/njord/commit/5fda43a07446cb44be1319b338f9345ad8c4b220))
* update project identity and enhance Config Builder ([1f4a92f](https://github.com/st0o0/njord/commit/1f4a92f4ae335728f5083f2d4d848011f882b444))
* update specs for capability-driven discovery and Aspire test infra ([d764eaa](https://github.com/st0o0/njord/commit/d764eaa73aeb1517ee929f5aefe80393a329255e))


### Refactoring

* clean domain boundaries — typed daily values, move actor messages ([bc8a788](https://github.com/st0o0/njord/commit/bc8a7880d7f4749b4543f8b9c41f2e9e75f5b09a))
* create Njord.Mqtt namespace for MQTT-specific code ([c7cf1b1](https://github.com/st0o0/njord/commit/c7cf1b15f9a9839c35311b0f5508ff8de2d7427d))
* decouple capability tracking from MQTT via EgressEvent hub ([dcfca58](https://github.com/st0o0/njord/commit/dcfca58ec08d2273dd9528f223632741df041890))
* egress actor with stream graph, transport seam, and device-based discovery ([72f5a0c](https://github.com/st0o0/njord/commit/72f5a0c76363ad0d938eda727fe5b68f67338854))
* **egress:** introduce protocol-neutral EgressEvent and MergeHub/BroadcastHub EgressActor ([69c18e7](https://github.com/st0o0/njord/commit/69c18e78b6c44f49ca88c276955d99ecc4086e70))
* fix CycleId semantics and remove dead pipeline code ([bef2d2a](https://github.com/st0o0/njord/commit/bef2d2acee1c8557e7ec693a78cc96e5bcc7762b))
* integrate Servus.Core and Servus.Akka for modular bootstrap ([ccd8426](https://github.com/st0o0/njord/commit/ccd8426fe0d551705b89eb092b5bc505cc4e713e))
* introduce enrichment type system with IEnrichmentFeature hierarchy ([09e2fc1](https://github.com/st0o0/njord/commit/09e2fc1ba28cf67e85c70cc6d441b815d147e12d))
* migrate test infrastructure from Testcontainers to Aspire ([56803f6](https://github.com/st0o0/njord/commit/56803f6cb5644a93c9adcefb744a1b143e9cd178))
* migrate ToMqttMessages from Result records to StatePayloadBuilder ([f887f37](https://github.com/st0o0/njord/commit/f887f3782086b2b26c4b3b7a5a1746e615105d69))
* move MQTT tests to Njord.Tests.Mqtt namespace ([45abbc1](https://github.com/st0o0/njord/commit/45abbc163a2fcf705e4b877fb2290afba7a30ad3))
* pipeline actor with Source.Queue, fetch/expand stages, and hash feedback ([dad1bf2](https://github.com/st0o0/njord/commit/dad1bf27bb6c0cb5b3f9941a7a93c621f8d8f4d3))
* remove Aspire AppHost and integration test projects ([8d9729a](https://github.com/st0o0/njord/commit/8d9729a7469bb7d0a17e43e4a57cb8929d9f2a86))
* remove dead code, add failure routing and discovery toggle ([3a2a054](https://github.com/st0o0/njord/commit/3a2a054eec7b5e87352fa34485f95382ea8fdf3d))
* remove OpenTelemetry instrumentation, keep Serilog-only logging ([be08ae2](https://github.com/st0o0/njord/commit/be08ae254def7b8532f9f76207074bec7ecdbb0f))
* replace Akka.Persistence.Sqlite with Akka.Persistence.Sql.Hosting ([2cf2e4c](https://github.com/st0o0/njord/commit/2cf2e4cf917c04bd6bb3150559234a83b31cdc70))
* replace raw JsonElement navigation with typed [JsonExtensionData] DTO ([ca4f95b](https://github.com/st0o0/njord/commit/ca4f95b4bf3637083e96efcfa5961e02e86ddb7b))
* replace raw queue handles with StreamRefs, MergeHub, and BroadcastHub ([04acd21](https://github.com/st0o0/njord/commit/04acd217b4d0483ea9973c7c482da9dd40b268c5))
* replace string-based parameter lookups with typed registry access ([eb4c6b1](https://github.com/st0o0/njord/commit/eb4c6b17d9c7d001cdcb4a2e8b9777d4e132556d))
* restructure Domain into Weather and Analysis subnamespaces ([bfa6663](https://github.com/st0o0/njord/commit/bfa6663bd2f9842b60fe79e962e4afa83707e7b0))
* split MqttEgressActor into focused single-responsibility actors ([d15ccd3](https://github.com/st0o0/njord/commit/d15ccd3662c07e2f9969f547051889f31efc5bdc))
* sync IBudgetGate (TryAcquire/EstimateDelay) replaces async ([64b9ad5](https://github.com/st0o0/njord/commit/64b9ad5ff83431c58c7e011bf77c44252309adee))
* use Servus.Core AppBuilder/AppRunner for application bootstrap ([2a8469e](https://github.com/st0o0/njord/commit/2a8469e307c4274d88accbd8126eaea9e4957237))

## [0.1.0](https://github.com/st0o0/njord/compare/v0.1.0...v0.1.0) (2026-07-12)


### ⚠ BREAKING CHANGES

* expand forecast parameters from closed 9-enum to full Open-Meteo registry
* switch ingest from Kachelmann to keyless Open-Meteo

### Features

* adaptive per-model poll scheduling with Akka.Persistence ([bc22040](https://github.com/st0o0/njord/commit/bc220402fee98414301f94ff5b746c90f97c516f))
* add /healthz endpoint spec and integration test ([e24b3aa](https://github.com/st0o0/njord/commit/e24b3aab2f9e986231eff9b4072cd0d18f00e910))
* add Kachelmann ingest foundation ([baabcb9](https://github.com/st0o0/njord/commit/baabcb906ff592be5f844ad7425628896ab9da72))
* expand forecast parameters from closed 9-enum to full Open-Meteo registry ([3d14ecd](https://github.com/st0o0/njord/commit/3d14ecd5ac37de4d06b2f6c84e6ed04ed87c90cc))
* publish per-model forecasts to Home Assistant via MQTT discovery ([ac5badf](https://github.com/st0o0/njord/commit/ac5badfe6236ea1de6d60c9c5105c2e4d1738da2))
* switch ingest from Kachelmann to keyless Open-Meteo ([3e43ea6](https://github.com/st0o0/njord/commit/3e43ea696d0d75566606ed63ba8a7f778544492e))
* switch to WebApplication host with ASP.NET Docker image ([51ea4cd](https://github.com/st0o0/njord/commit/51ea4cdea05bf4e7e92f03f85fee458d9fd329fb))


### Bug Fixes

* create data directory in build stage for chiseled image ([02dc416](https://github.com/st0o0/njord/commit/02dc416ce73e76e8d6c29e90fb99ca09ac3c7570))


### Documentation

* add CLAUDE.md and record verified Kachelmann API facts ([98a78e0](https://github.com/st0o0/njord/commit/98a78e05673ff425032ab087899342d4b376928e))
* **openspec:** archive add-deployment change ([5e77056](https://github.com/st0o0/njord/commit/5e77056ae1f5afd4b07311d36b99c563f4d36509))
* **openspec:** propose add-kachelmann-ingest change ([26bf8f2](https://github.com/st0o0/njord/commit/26bf8f25baae967cf68190b258260d66836a6eaa))
* **openspec:** propose add-mqtt-egress ([914567d](https://github.com/st0o0/njord/commit/914567dd7460b39218e5d1980b6931cb3ccbd57c))
* **openspec:** propose replace-kachelmann-with-openmeteo ([85786ba](https://github.com/st0o0/njord/commit/85786badd0e0e492fdeaf042233d513c2e9411b9))
* **openspec:** sync specs and archive add-kachelmann-ingest ([cf13ddf](https://github.com/st0o0/njord/commit/cf13ddf9639ad12fefbaf47f6d042b4807fa8cca))
* **openspec:** sync specs and archive add-mqtt-egress ([d709648](https://github.com/st0o0/njord/commit/d709648c774efa55db1fa4f34c6c83a780f14064))
* **openspec:** sync specs and archive cleanup-cycleid-and-dead-code change ([2def600](https://github.com/st0o0/njord/commit/2def600173def77d39da9cc41cbc26aadb2b9fda))
* **openspec:** sync specs and archive egress, pipeline, topic-per-horizon, and adaptive-poll-scheduling changes ([7300876](https://github.com/st0o0/njord/commit/730087646a4402fc7334fec5e1c9a2f0a2cbab73))
* **openspec:** sync specs and archive pipeline-streamref-broadcasthub change ([e638b29](https://github.com/st0o0/njord/commit/e638b29da10e9d49f113cb6195ca2e1cd5fa92d4))
* **openspec:** sync specs and archive replace-kachelmann-with-openmeteo ([4055e38](https://github.com/st0o0/njord/commit/4055e384479ccc8282b30d3c1f73d6cb49350f85))


### Refactoring

* egress actor with stream graph, transport seam, and device-based discovery ([72f5a0c](https://github.com/st0o0/njord/commit/72f5a0c76363ad0d938eda727fe5b68f67338854))
* fix CycleId semantics and remove dead pipeline code ([bef2d2a](https://github.com/st0o0/njord/commit/bef2d2acee1c8557e7ec693a78cc96e5bcc7762b))
* pipeline actor with Source.Queue, fetch/expand stages, and hash feedback ([dad1bf2](https://github.com/st0o0/njord/commit/dad1bf27bb6c0cb5b3f9941a7a93c621f8d8f4d3))
* replace raw JsonElement navigation with typed [JsonExtensionData] DTO ([ca4f95b](https://github.com/st0o0/njord/commit/ca4f95b4bf3637083e96efcfa5961e02e86ddb7b))
* replace raw queue handles with StreamRefs, MergeHub, and BroadcastHub ([04acd21](https://github.com/st0o0/njord/commit/04acd217b4d0483ea9973c7c482da9dd40b268c5))
