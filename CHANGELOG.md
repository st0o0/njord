# Changelog

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
