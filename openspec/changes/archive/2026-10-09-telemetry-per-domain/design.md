## Context

FunkArr has per-domain `Telemetry.cs` files with `private static readonly Meter` and
`internal static readonly` instrument fields, registered explicitly via
`.AddMeter("FunkArr.<Domain>")`. njord uses a global `NjordMetrics` singleton with
extension methods that create fresh instruments per call, registered via a magic string
filter.

## Goals / Non-Goals

**Goals:**
- Per-domain meter isolation matching FunkArr's pattern.
- Compile-time instrument singleton guarantee via `static readonly` fields.
- Explicit meter registration in the host.
- Zero change to metric names or tag schemas (Prometheus scrape stays identical).

**Non-Goals:**
- Not adding or removing metrics.
- Not changing prometheus-net configuration beyond meter registration.

## Decisions

1. **Per-domain `Telemetry` class** in each feature library: `internal static class
   Telemetry { private static readonly Meter _meter = new("Njord.<Domain>"); internal
   static readonly Counter<long> FetchTotal = _meter.CreateCounter<long>(...); }`. Each
   domain owns its instruments as `static readonly` fields — no method call, no possible
   double-creation.

2. **Delete `NjordMetrics.cs`** and the five `*MetricsExtensions.cs` files from
   `Njord.Core/Diagnostics/`. The `Diagnostics/` directory in Core keeps only the
   telemetry setup container (if one exists) or becomes empty and is removed.

3. **Explicit meter registration**: replace the
   `InstrumentFilterPredicate = i => i.Meter.Name == "Njord"` with per-domain
   `.AddMeter("Njord.Ingest", "Njord.Pipeline", "Njord.Mqtt", "Njord.Egress",
   "Njord.Enrichment")`. When a new domain adds metrics, it adds its meter name — fail-fast
   (meter not registered = metrics not exported, visible in Prometheus scrape).

4. **Migration order**: one domain at a time, verifiable via Prometheus scrape or
   `prometheus-net` test endpoint after each step.

## Risks / Trade-offs

- [Risk] Splitting from one meter to five changes the `meter.name` label in Prometheus. →
  Mitigated by keeping metric names unchanged (`njord_ingest_fetch_total` stays); only the
  meter name metadata changes, which most dashboards don't filter on.
- [Risk] Feature libraries currently don't depend on `System.Diagnostics.DiagnosticSource`
  directly (they get it transitively through Core). → Each domain project needs the
  `System.Diagnostics.DiagnosticSource` package reference (or it may already come
  transitively through Akka). Verify per project.

## Migration Plan

One domain at a time, each independently deployable. Order: Ingest (smallest), Pipeline,
Egress, Mqtt, Enrichment.

## Open Questions

None.
