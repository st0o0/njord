## 1. Development launch profile

- [x] 1.1 Add `src/Njord/Properties/launchSettings.json` with one profile (`"commandName": "Project"`, `"launchBrowser": false`, `"environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }`) and no `applicationUrl`, so the Kestrel endpoints from `appsettings.json` (8080/8081) stay in charge
- [x] 1.2 Verify from `src/`: `dotnet run --project Njord/Njord.csproj` logs `Hosting environment: Development` and uses the values from `appsettings.Development.json` (for example `ForecastDays` 16 and the `borken`/`winterswijk` locations); no spec is added because launch profiles are not part of the build output and not loaded by tests or the container image

## 2. Developer documentation

- [x] 2.1 `docs/development/service.md`: remove the "Quality gates" section (`dotnet tool restore` / `dotnet slopwatch`; there is no `.config/dotnet-tools.json`)
- [x] 2.2 `docs/development/service.md`: replace the `Njord.IntegrationTests`/Testcontainers paragraph with the real layout (one test project per library plus the host project `Njord.Tests`, all run with `dotnet run --project`, no Docker needed; full-stack checks are the `e2e-test` skill), matching AGENTS.md "Build & test"
- [x] 2.3 `docs/development/service.md`: state that `dotnet run` picks the Development environment from `Properties/launchSettings.json`
- [x] 2.4 `docs/development/setup.md`: remove `Njord__Enrichment__History__Enabled` from the compose example
- [x] 2.5 `AGENTS.md` conventions: `EnrichmentKind` lives in `Njord.Messages.Pipeline` (not `Njord.Core.Egress`), `ConsensusMethod` in `Njord.Compute.Settings` (not `Njord.Compute.Configuration`); solution structure: `Njord.Persistence.Tests` holds roundtrip and inline wire-format specs (no Verify), `Njord.OpenMeteo.Tests` holds `OpenMeteoClient` and `OptionsBudgetProvider` specs
- [x] 2.6 `openspec/config.yaml` context: replace "publishes Home Assistant entities via MQTT Discovery" with gRPC to Home Assistant (ha-njord) plus optional MQTT state publishing

## 3. User documentation, examples and diagrams

- [x] 3.1 Remove MQTT Discovery from `docs/index.md`, `docs/architecture.md`, `docs/configuration/mqtt.md` (drop the "Discovery" section; `DiscoveryPrefix` is only used for the `{prefix}/status` subscription) and `docs/mqtt-reference.md` (drop "Device discovery"; describe state and availability topics only)
- [x] 3.2 `docs/configuration/index.md`: describe `DiscoveryInterval` as the poll interval of models whose update cycle is not learned yet (not MQTT discovery)
- [x] 3.3 Remove the history feature from `docs/configuration/enrichment.md`, `docs/configuration/persistence.md`, `docs/getting-started.md`, `docs/mqtt-reference.md` (history topic), `docs/data/enrichment.json` and `README.md` ("forecast accuracy tracking")
- [ ] 3.4 Remove history from `docs/home-assistant/index.md`, `options.md`, `diagnostics.md` and `entities.md` after confirming against the current ha-njord entity set (history removed from the `enrichments` diagnostics section and the per-model history attribute note; the History options group, the `model_performance` sensor and the index.md sensor list are ha-njord entities: blocked: needs ha-njord)
- [x] 3.5 Config builder (`docs/.vitepress/theme/builder/ConfigBuilder.vue`, `types.ts`, `serializer.ts`): drop the History memory hint and the `discoveryEnabled` field, and label `DiscoveryInterval` as in 3.2
- [x] 3.6 `docs/models.md`: regenerate the model table from `ModelCoverageRegistry` (for example `icon_d2` 60 h, `meteoswiss_icon_ch1` 42 h)
- [ ] 3.7 `docs/likec4/model.c4` and `views.c4`: model the current architecture (ingest: `OpenMeteoGateway`, `OpenMeteoClient`; domain: `PollScheduler`, `ForecastPipeline`, analysis libraries; consumers: `MqttStatePublisher`, `ForecastReadModel` + gRPC API; Home Assistant via gRPC/ha-njord; optional MQTT broker), then run `pnpm export:diagrams` in `docs/` to regenerate `docs/public/index.png` (model.c4/views.c4 updated and `likec4 validate` passes; blocked: the PNG export needs a Playwright Chromium download, and cdn.playwright.dev is unreachable from this machine, so `docs/public/index.png` is not regenerated)
- [x] 3.8 `docker-compose.example.yml`: remove the History block, use the real alert keys (`FrostThresholds__0..2`, `StormGustThresholds__0..2`), add `Njord__DiscoveryInterval`, `Njord__OpenMeteoBaseUrl` and the remaining options properties as commented defaults
- [x] 3.9 `src/Njord/appsettings.Example.json`: replace the stream-tracing stage list with the existing stages (`mqtt-state-in`, `mqtt-state-out`) and remove entries for removed stages and actors
- [x] 3.10 Verify the docs build: `pnpm install && pnpm build` in `docs/`, and `npx likec4 validate` in `docs/`

## 4. E2E artifacts

- [x] 4.1 `e2e/docker-compose.e2e.yml`: remove `Njord__Enrichment__History__Enabled`
- [ ] 4.2 `e2e/E2E-TEST-PLAN.md` and `.claude/skills/e2e-test/SKILL.md`: remove the history steps and entities, and update the expected entity counts after checking them against ha-njord (njord side done: history enrichment config and the GetEnrichments `history` step 6.6 removed; history entities and entity counts: blocked: needs ha-njord)
- [ ] 4.3 Copy the updated `e2e/` directory to `../ha-njord/e2e/` (blocked: needs ha-njord, not cloned on this machine)

## 5. Archive and spec cleanup

- [x] 5.1 Run `openspec validate refresh-stale-specs --no-interactive` (must pass)
- [x] 5.2 Archive with `openspec archive refresh-stale-specs --yes --no-validate` (design D3: openspec 1.7.0 cannot write the 12 emptied specs, so validation of rebuilt specs is skipped here and done in 5.5)
- [x] 5.3 Delete the emptied spec folders: `openspec/specs/{discovery-toggle,dissolve-host-tests,domain-core-merge,egress-event,enrichment-actor,historical-learning,pipeline-actor,read-priority-mailbox,shard-region-entities,snapshot-actors,snapshot-persistence-dtos,snapshot-sharding}/`
- [x] 5.4 Rewrite the stale `## Purpose` paragraphs of `activity-indices`, `daily-forecast`, `delta-publishing`, `dynamic-budget-throttle`, `e2e-enrichment-validation`, `egress-stream-graph`, `enrichment-config-validation`, `failure-routing`, `grpc-enrichment-api`, `hourly-consensus`, `model-capability-tracking`, `mqtt-actor-topology`, `mqtt-egress`, `openmeteo-client`, `pipeline-commands`, `poll-pipeline`, `poll-status-query`, `standalone-sub-options`, `stream-composition`, `telemetry-infrastructure` and `writable-options`, and replace the `TBD` purposes of `consumer-model`, `grpc-mapping-layer`, `options-validation-hygiene`, `per-domain-telemetry`, `persistence-record-hygiene`, `pipeline-composition`, `structured-logging-hygiene`, `test-shared-infrastructure` and `vertical-feature-libraries` (also rewrote the stale `message-response-naming` purpose, which still described the removed `QueryNounResponse` rule)
- [x] 5.5 Run `openspec validate --all --no-interactive` from the repo root (must pass) and `bash scripts/check-openspec-root.sh`

## 6. Validation

- [x] 6.1 From `src/`: `dotnet build Njord.slnx` completes with 0 warnings and 0 errors
- [x] 6.2 From `src/`, run every test project and confirm all 971 tests pass:
  `for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done`
