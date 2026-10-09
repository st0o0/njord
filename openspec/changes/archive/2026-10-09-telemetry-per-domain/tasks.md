## 1. Ingest telemetry (pilot)

- [ ] 1.1 Create `src/Njord.Ingest/Telemetry.cs` with `Meter("Njord.Ingest")` and static readonly instruments currently defined in `src/Njord.Core/Diagnostics/IngestMetricsExtensions.cs`.
- [ ] 1.2 Update `OpenMeteoClient` (or whatever class calls `NjordMetrics.Instance.AddFetchTotal()`) to reference `Telemetry.FetchTotal` directly.
- [ ] 1.3 Register `"Njord.Ingest"` in the host's telemetry setup.
- [ ] 1.4 Verify via `Njord.Ingest.Tests` that metrics are still recorded.

## 2. Pipeline telemetry

- [ ] 2.1 Create `src/Njord.Pipeline/Telemetry.cs` with `Meter("Njord.Pipeline")` and instruments from `PipelineMetricsExtensions.cs`.
- [ ] 2.2 Update all Pipeline actors/stages to reference `Telemetry.*` fields directly.
- [ ] 2.3 Register `"Njord.Pipeline"` in the host.

## 3. Egress telemetry

- [ ] 3.1 Create `src/Njord.Egress/Telemetry.cs` with `Meter("Njord.Egress")` and instruments from `EgressMetricsExtensions.cs`.
- [ ] 3.2 Update egress actors to reference `Telemetry.*` fields.
- [ ] 3.3 Register `"Njord.Egress"` in the host.

## 4. Mqtt telemetry

- [ ] 4.1 Create `src/Njord.Mqtt/Telemetry.cs` with `Meter("Njord.Mqtt")` and instruments from `MqttMetricsExtensions.cs`.
- [ ] 4.2 Update MQTT actors to reference `Telemetry.*` fields.
- [ ] 4.3 Register `"Njord.Mqtt"` in the host.

## 5. Enrichment telemetry

- [ ] 5.1 Create `src/Njord.Enrichment/Telemetry.cs` with `Meter("Njord.Enrichment")` and instruments from `EnrichmentMetricsExtensions.cs`.
- [ ] 5.2 Update enrichment actors to reference `Telemetry.*` fields.
- [ ] 5.3 Register `"Njord.Enrichment"` in the host.

## 6. Cleanup

- [ ] 6.1 Delete `src/Njord.Core/Diagnostics/NjordMetrics.cs` and all five `*MetricsExtensions.cs` files.
- [ ] 6.2 Remove the magic-string `InstrumentFilterPredicate` from the host telemetry setup.
- [ ] 6.3 Grep for `NjordMetrics` across the entire `src/` tree to confirm zero remaining references.

## Validation

- [ ] 7.1 `dotnet build Njord.slnx` (run from `src/`)
- [ ] 7.2 Run all test projects sequentially to confirm zero regressions.
- [ ] 7.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
