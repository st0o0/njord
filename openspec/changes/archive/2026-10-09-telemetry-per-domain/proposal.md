## Why

A cross-codebase review against the sibling FunkArr project found that njord's telemetry
pattern (one global `Meter("Njord")` behind `NjordMetrics.Instance` + five
`*MetricsExtensions.cs` files in `Njord.Core/Diagnostics/`) is less clean than FunkArr's
per-domain approach (`Meter("FunkArr.<Domain>")` in each domain's own `Telemetry.cs`):
1. **Instrument-creation footgun**: njord's extension methods (`AddFetchTotal()`,
   `AddMqttDedup()`, etc.) create a fresh instrument on every call. The convention says
   "cache as `private static readonly`" but this is a discipline requirement, not a
   compile-time guard. If called twice (e.g. in two constructors), two Counter instances
   exist on the same meter — silently accepted by OpenTelemetry. FunkArr's `static readonly`
   fields guarantee one instance per instrument at compile time.
2. **Discovery**: njord's metrics are scattered across five extension files in
   `Njord.Core/Diagnostics/`; discovering what metrics a domain exposes requires cross-file
   lookup. FunkArr: each domain's `Telemetry.cs` is the single source of truth.
3. **Registration**: njord uses a magic string filter (`i.Meter.Name == "Njord"`) instead of
   FunkArr's explicit per-meter `.AddMeter("FunkArr.<Domain>")` listing.

## What Changes

- Introduce per-domain `Telemetry` static classes: `IngestTelemetry` (in `Njord.Ingest`),
  `PipelineTelemetry` (in `Njord.Pipeline`), `MqttTelemetry` (in `Njord.Mqtt`),
  `EgressTelemetry` (in `Njord.Egress`), `EnrichmentTelemetry` (in `Njord.Enrichment`).
  Each owns a `private static readonly Meter _meter = new("Njord.<Domain>")` and
  `internal static readonly` instrument fields.
- Remove `NjordMetrics` singleton and the five `*MetricsExtensions.cs` files from
  `Njord.Core/Diagnostics/`.
- Register each domain's meter explicitly in the host's telemetry setup:
  `.AddMeter("Njord.Ingest", "Njord.Pipeline", ...)`.
- Update all call sites (actors/clients that currently call
  `NjordMetrics.Instance.AddXxx()`) to reference their domain's `Telemetry.XxxCounter`
  directly.

## Capabilities

### New Capabilities

- `per-domain-telemetry`: per-domain `Telemetry` classes with static-readonly instruments,
  replacing the global NjordMetrics singleton with extension-method instrument creation.

### Modified Capabilities

None. Metric names, tag schemas, and Prometheus export behavior unchanged.

## Impact

- `src/Njord.Core/Diagnostics/` (NjordMetrics.cs, *MetricsExtensions.cs — deleted)
- New `Telemetry.cs` in each domain project: `Njord.Ingest/`, `Njord.Pipeline/`,
  `Njord.Mqtt/`, `Njord.Egress/`, `Njord.Enrichment/`
- Host telemetry registration
- All call sites that reference `NjordMetrics.Instance`
- No wire-format or API changes.

## Non-goals

- Not changing metric names (`njord_*` prefix stays) or tag schemas.
- Not adding new metrics or removing existing ones.
- Not changing the Prometheus export pipeline (prometheus-net stays).

## API budget impact

None.
