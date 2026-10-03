# SensorHub: External Sensor Data via gRPC

## Problem

Njord's enrichment pipeline operates exclusively on forecast data from Open-Meteo. Several enrichment computations use static configuration values for parameters that are actually measurable (e.g. `IndoorTemp` defaults to 22.0 in both index and energy calculations). There is no mechanism to feed live sensor readings into the system.

## Proposal

Introduce a **SensorHub** — a domain-driven actor that receives external sensor readings via gRPC and makes them available to the enrichment pipeline. The design follows three principles:

1. **Domain defines the schema** — njord declares which sensor kinds it understands (`SensorKind` enum). Unknown kinds are rejected. The domain is the contract, not the client.
2. **Latest-value semantics** — the SensorHub stores the most recent reading per (Location, SensorKind, Source). Enrichments pull the latest aggregated value at each poll cycle. No reactive re-computation.
3. **Graceful degradation** — if no sensor reading exists for a kind, enrichments fall back to the configured default, then to the hardcoded default. Sensors enhance, never gate.

## Design

### SensorKind (Domain Enum)

Closed enum of physical quantities njord can consume:

| Kind | Unit | Used by | Aggregation |
|---|---|---|---|
| `IndoorTemperature` | C | Indices (Ventilation), future enrichments | Average |
| `IndoorHumidity` | % | Indices (Ventilation), future enrichments | Average |
| `HeatPumpFlowTemp` | C | Future enrichments | Latest |
| `SolarPanelPower` | W | Future enrichments | Sum |
| `BatteryStateOfCharge` | % | Future enrichments | Average |
| `HeatPumpPower` | W | Future enrichments | Latest |

New kinds are added when new enrichment features need them. Each kind defines its unit, plausibility range (for validation), and aggregation strategy.

### SensorHub Actor

```
                    gRPC SensorService
                          |
                          v
                ┌──────────────────┐
                |    SensorHub     |
                |                  |
                |  Readings:       |
                |    Dict<Key, Reading>
                |    Key = (Location, Kind, Source)
                |                  |
                |  Messages:       |
                |    Update(Reading)  <-- from gRPC
                |    GetSnapshot(Location)  --> SensorSnapshot
                |    Expire timer  |
                └──────────────────┘
                          ^
                          | Ask
                          |
                  EnrichmentActor
             (pulls SensorSnapshot per cycle)
```

- **Key**: `(Location, SensorKind, Source)` — Source is a free-form client identifier (e.g. "wohnzimmer", "schlafzimmer")
- **Aggregation**: when multiple sources exist for the same (Location, Kind), aggregate per-kind strategy (average, sum, latest)
- **Staleness**: readings expire after a configurable TTL (default: 2x poll interval). Expired readings are excluded from aggregation.
- **Validation**: each SensorKind defines a plausibility range. Readings outside the range are rejected with `INVALID_ARGUMENT`.

### SensorSnapshot

Immutable record returned by the SensorHub, consumed by enrichments:

```
SensorSnapshot
  Location: string
  Readings: IReadOnlyDictionary<SensorKind, AggregatedReading>

AggregatedReading
  Value: double
  SourceCount: int
  NewestMeasuredAt: DateTimeOffset
```

### Fallback Chain in Enrichments

```
value = sensorSnapshot?.Get(SensorKind.IndoorTemperature)  // live sensor
     ?? options.IndoorTemp                                  // config value
                                                            // (hardcoded default in options class)
```

The `IStatelessEnrichment.Compute` signature changes:

```
// Before:
Compute(ConsensusSnapshot consensus)

// After:
Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors)
```

`SensorSnapshot` is nullable — enrichments must handle the no-sensors case identically to today's behavior.

### gRPC API

```protobuf
enum SensorKind {
  SENSOR_KIND_UNSPECIFIED = 0;
  SENSOR_KIND_INDOOR_TEMPERATURE = 1;
  SENSOR_KIND_INDOOR_HUMIDITY = 2;
  SENSOR_KIND_HEAT_PUMP_FLOW_TEMP = 3;
  SENSOR_KIND_SOLAR_PANEL_POWER = 4;
  SENSOR_KIND_BATTERY_STATE_OF_CHARGE = 5;
  SENSOR_KIND_HEAT_PUMP_POWER = 6;
}

message SensorReading {
  SensorKind kind = 1;
  string location = 2;
  string source = 3;
  double value = 4;
  google.protobuf.Timestamp measured_at = 5;
}

message PushResponse {
  bool accepted = 1;
  string rejection_reason = 2;
}

service SensorService {
  rpc Push (SensorReading) returns (PushResponse);
  rpc StreamPush (stream SensorReading) returns (PushResponse);
}
```

### Configuration

```json
{
  "Njord": {
    "Sensors": {
      "Enabled": true,
      "StalenessSeconds": 7200,
      "Validation": {
        "IndoorTemperature": { "Min": -10, "Max": 60 },
        "HeatPumpFlowTemp": { "Min": 15, "Max": 80 }
      }
    }
  }
}
```

## Scope

### In scope

- `SensorKind` domain enum with validation metadata
- `SensorHub` actor (store, aggregate, expire)
- `SensorSnapshot` / `AggregatedReading` domain records
- `SensorService.proto` + gRPC service implementation
- `IStatelessEnrichment.Compute` signature change (add `SensorSnapshot?`)
- `IStatefulEnrichment.Compute` signature change (add `SensorSnapshot?`)
- Wire SensorHub into EnrichmentActor (Ask before each cycle)
- Index enrichment: use live `IndoorTemperature` in Ventilation score with fallback
- Configuration (`SensorOptions`)
- Tests for all of the above

### Out of scope

- MQTT subscription for sensor data (gRPC only)
- New enrichment features that consume sensor data (separate changes)
- HA integration/addon that pushes sensor data (client-side)
- UI for sensor management

## Dependencies

- Should be implemented after `remove-energy-enrichment` to avoid modifying energy code that will be deleted

## Risks

- **Signature change on enrichment interfaces** touches all 6 remaining enrichment implementations (consensus, alerts, derived, trends, indices, history). Low risk — the new parameter is nullable and existing implementations can ignore it.
- **Actor lifecycle** — SensorHub must be available before EnrichmentActor starts pulling. Standard Akka.Hosting startup ordering handles this.
