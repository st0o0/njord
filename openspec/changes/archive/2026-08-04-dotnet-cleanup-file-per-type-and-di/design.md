## Context

The `Domain/Analysis/` directory has 19 `.cs` files, of which 9 contain multiple public/internal types. Two files have mismatched names (`IndexResult.cs` contains no `IndexResult`; `DaySliceIndexResult.cs` contains `IndexResult`). Five data records carry static `Compute` factory methods that depend on `TimeProvider`, `ResolvedParameterSet`, and `ConsensusSnapshot` — dependencies that should flow through DI rather than method parameters on a data record.

## Goals / Non-Goals

**Goals:**
- Every `.cs` file contains exactly one public/internal type, named after the file
- Data records are pure data — no computation logic
- Computation logic lives in injectable service classes
- Enrichment features receive computers via constructor injection

**Non-Goals:**
- No behavioral changes to any computation
- No changes to pure static utility classes (IndexScorer, ConsensusComputer math, DerivedComputer math, TrendAnalyzer, HistoryAnalyzer, PreferenceResolver, AlertEvaluator)
- No interface extraction for computers (concrete classes are sufficient — the enrichment features are already behind `IStatelessEnrichment`)

## Decisions

### D1: File-per-type splits

Each type gets its own file. Exceptions:
- **Actor messages stay with their actor** (`MqttConnectionActor.cs` keeps `SubscribeInbound`, `MqttConnected`, `MqttInboundMessage`) — this is established Akka convention and the messages have no meaning outside the actor.

Split plan:

| Source file | Types to extract | New files |
|---|---|---|
| `AlertEvaluator.cs` | `AlertType`, `AlertSeverity`, `Alert`, `AlertTypeExtensions` | `AlertType.cs`, `AlertSeverity.cs`, `Alert.cs`, `AlertTypeExtensions.cs` |
| `TrendResult.cs` | `ParameterTrend`, `PrecipTimingInfo`, `ExtremaTimingInfo`, `StabilityInfo`, `DecayInfo` | 5 new files |
| `ConsensusResult.cs` | `OutlierInfo`, `ConfidenceIntervalInfo`, `HorizonConsensus`, `ParameterConsensus` | 4 new files |
| `ConsensusSnapshot.cs` | `HourlyConsensus`, `DailyConsensus` | 2 new files |
| `DerivedResult.cs` | `HorizonDerived`, `ScalarDerived` | 2 new files |
| `TrendAnalyzer.cs` | `WeatherChangeResult` | 1 new file |
| `ForecastHistory.cs` | `ForecastRecord` | 1 new file |
| `IndexResult.cs` | `ScoreEnvelope`, `FrostProtectionInfo`, `VpdInfo` | 3 new files (IndexResult.cs deleted, was just these 3) |
| `DaySliceIndexResult.cs` | `DayScoreSet` | `DayScoreSet.cs` + rename `DaySliceIndexResult.cs` → `IndexResult.cs` |
| `MqttMessages.cs` | `MqttEgressTuning`, `RequestMqttSink`, `MqttSinkResponse` | 3 new files (MqttMessages.cs deleted) |

### D2: Computer service naming

| Record | Static method | New service | File |
|---|---|---|---|
| `IndexResult` | `IndexResult.Compute` | `IndexComputer` | `Domain/Analysis/IndexComputer.cs` |
| `ConsensusSnapshot` | `ConsensusSnapshot.Compute` | `ConsensusSnapshotFactory` | `Domain/Analysis/ConsensusSnapshotFactory.cs` |
| `TrendResult` | `TrendResult.Compute` | `TrendComputer` | `Domain/Analysis/TrendComputer.cs` |
| `DerivedResult` | `DerivedResult.Compute` | `DerivedResultComputer` | `Domain/Analysis/DerivedResultComputer.cs` |
| `HistoryResult` | `HistoryResult.Compute` | `HistoryComputer` | `Domain/Analysis/HistoryComputer.cs` |

`ConsensusSnapshot.Compute` → `ConsensusSnapshotFactory` (not "Computer") because it's a factory that builds a snapshot from a `ModelSnapshot` — the actual math is already in `ConsensusComputer`.

### D3: DI registration pattern

All computers registered as singletons (they're stateless, only hold injected dependencies):

```csharp
services.AddSingleton<IndexComputer>();
services.AddSingleton<ConsensusSnapshotFactory>();
services.AddSingleton<TrendComputer>();
services.AddSingleton<DerivedResultComputer>();
services.AddSingleton<HistoryComputer>();
```

No interfaces — concrete class injection. The enrichments are already abstracted behind `IStatelessEnrichment`; adding interfaces on the computers would be abstraction for abstraction's sake.

### D4: What moves into each computer

Each computer takes the dependencies that were previously passed as parameters to `Record.Compute()`:

- **IndexComputer**: needs `TimeSliceAggregator` logic (inlined — it's only used here), `IndexScorer` (static, called directly)
- **ConsensusSnapshotFactory**: needs `ConsensusComputer` (static, called directly)
- **TrendComputer**: needs `TrendAnalyzer` (static, called directly)
- **DerivedResultComputer**: needs `DerivedComputer` (static, called directly)
- **HistoryComputer**: needs `HistoryAnalyzer` (static, called directly), `ForecastHistory`

The private helper methods (`Mean24hFromConsensus`, `ComputeEnvelopes`, `ComputeFrostFromConsensus`, etc.) move into the computer class as private methods.

### D5: Enrichment feature updates

Each enrichment feature's constructor gains the computer as a parameter. The `Compute` method delegates:

```
Before: var result = IndexResult.Compute(consensus, parameters, timeProvider, prefs);
After:  var result = _indexComputer.Compute(consensus, prefs);
```

The computer holds `ResolvedParameterSet` and `TimeProvider` as constructor-injected fields — they don't change per call.

## Risks / Trade-offs

- **Large diff, zero behavior change** → High review noise for a pure refactoring. Mitigated by doing file splits first (mechanical), then compute extraction (structural) — two clear commit boundaries.
- **Existing ConsensusComputer name collision** → The math utility is `ConsensusComputer`; the new snapshot builder is `ConsensusSnapshotFactory`. Names are distinct.
