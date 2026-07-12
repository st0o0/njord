# Tasks — add-mqtt-egress

## 1. Configuration: Mqtt section and horizons

- [x] 1.1 TDD `MqttOptions` (Host required, Port 1883, optional credentials,
      DiscoveryPrefix `homeassistant`, BaseTopic `njord`) in
      `src/Njord/Configuration/MqttOptions.cs`, bound under `Njord:Mqtt`;
      validation in `src/Njord/Configuration/NjordOptionsValidator.cs`
      (missing host fails, password never in messages); tests in
      `src/Njord.Tests/Configuration/NjordOptionsValidatorSpec.cs`
- [x] 1.2 TDD `Horizons` list on `src/Njord/Configuration/NjordOptions.cs`
      (default 3/6/12/24/48/72; reject empty, non-positive, > 96 h) +
      validator tests

## 2. Packages

- [x] 2.1 `dotnet add Njord/Njord.csproj package MQTTnet`;
      `dotnet add Njord.Tests/Njord.Tests.csproj package Verify.XunitV3` and
      `package Testcontainers` (CPM: versions land in
      `src/Directory.Packages.props` automatically)

## 3. Egress: pure builders (Ingest/Egress never meet — domain types only)

- [x] 3.1 TDD `src/Njord/Egress/TopicScheme.cs`: device id, config topic,
      state topic, service availability topic, unique_ids
      (`njord_<loc>_<model>_<param>_h<h>`); tests in
      `src/Njord.Tests/Egress/TopicSchemeSpec.cs`
- [x] 3.2 TDD `src/Njord/Egress/DiscoveryPayloadBuilder.cs`: device-based
      payload (`dev`, `o`, shared `state_topic` + `availability` with mode
      `all`, `cmps` per (parameter, horizon) with `p: sensor`, unique_id,
      device_class/unit from the domain, `value_template`,
      `availability_template`, `expire_after` = 2 × poll interval, no
      `state_class`); snapshot tests via Verify in
      `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs`
- [x] 3.3 TDD `src/Njord/Egress/StatePayloadBuilder.cs`: `ModelForecast` →
      state JSON keyed `h<h>` with parameter values + anchored `valid_at`
      (ceil to next full grid hour ≥ tick + horizon); absent values are JSON
      `null`; tests incl. the 19:31-tick anchoring case and a short-horizon
      model

## 4. Egress: connection actor and flows

- [x] 4.1 TDD `src/Njord/Egress/IMqttPublisher.cs` seam +
      `src/Njord/Egress/MqttConnectionActor.cs`: connect/backoff, LWT
      registration, retained `online` after connect, `homeassistant/status`
      subscription → re-publish discovery, publish messages for discovery
      (incl. tombstones for removed devices) and telemetry; actor tests with
      a fake publisher in `src/Njord.Tests/Egress/MqttConnectionActorSpec.cs`
- [x] 4.2 MQTTnet-backed publisher implementation
      (`src/Njord/Egress/MqttNetPublisher.cs`) +
      `src/Njord/Egress/EgressServiceCollectionExtensions.cs`; wire the
      pipeline sink to `Tell` cycle results to the actor while the guardian
      keeps its summary log (`src/Njord/Pipeline/PipelineGuardianActor.cs`,
      `src/Njord/Program.cs`)

## 5. Integration tests (Docker-gated)

- [x] 5.1 Testcontainers/Mosquitto spec `src/Njord.Tests/Egress/MqttEgressIntegrationSpec.cs`
      (gated on `NJORD_DOCKER_TESTS=1`): retained discovery appears with 54
      components, retained state appears and satisfies the value_templates,
      LWT flips to `offline` on ungraceful disconnect, tombstone clears a
      removed device

## 6. Docs and config sample

- [x] 6.1 `src/Njord/appsettings.json`: add `Mqtt` section (host empty on
      purpose — startup names it) and `Horizons`; update `CLAUDE.md`
      decisions (1:1 egress before consensus, model devices enabled by
      default, recorder-exclude recommendation, MQTTnet + seam)

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` (from `src/`) — clean build
- [x] 7.2 `dotnet run --project Njord.Tests/Njord.Tests.csproj` (from `src/`)
      — full suite green
- [x] 7.3 `dotnet slopwatch` (from repo root) — no new findings
- [x] 7.4 End-to-end against a local Mosquitto container: run njord from
      `src/Njord/` with `Njord__Mqtt__Host=localhost`, then verify via
      `mosquitto_sub` that 8 retained device configs and 8 retained states
      exist and that `njord/status` is `online`
