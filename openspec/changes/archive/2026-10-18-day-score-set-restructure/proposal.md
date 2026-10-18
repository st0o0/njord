## Why

`DayScoreSet` carries 18 positional params: `DayOffset`, 8 named score ints, `HoursIncluded`, and 8 optional `ScoreEnvelope?` — one pair per `IndexKind`. `IndexAnalyzer` already builds a `Dictionary<IndexKind, ScoredIndex>` internally and then destructures it into the 16 named fields. Both consumers (gRPC and MQTT mappers) reconstruct the kind-value pairing by reading each field individually. Adding a new `IndexKind` requires adding 2 params to `DayScoreSet` plus updating every consumer — the flat record is a serialization convenience that leaked into the domain contract.

## What Changes

- `DayScoreSet` simplified from 18 positional params to `(int DayOffset, IReadOnlyDictionary<IndexKind, ScoredIndex> Scores, int HoursIncluded)`.
- `ScoredIndex` already exists as `(int Value, ScoreEnvelope? Envelope)` — it becomes the dictionary value.
- `IndexAnalyzer` passes the dictionary directly instead of destructuring.
- gRPC and MQTT mappers iterate the dictionary instead of reading named fields.
- Wire format unchanged — mappers produce the same output by iterating `IndexKind` in enum order.

## Non-goals

- Changing `IndexResult` (it already wraps `IReadOnlyList<DayScoreSet>`).
- Changing `ScoreEnvelope` or `ScoredIndex`.
- Changing the proto or MQTT wire format.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `daily-slice-scoring`: `DayScoreSet` structure changes from named fields to dictionary.

## Impact

- `Njord.Messages/Indices/DayScoreSet.cs`: record simplified
- `Njord.Indices/IndexAnalyzer.cs`: passes dictionary directly
- `Njord.Grpc/Mapping/IndexProtoMappings.cs`: iterates dictionary
- `Njord.Mqtt/Mapping/AnalysisPayloadMappings.cs`: iterates dictionary
- Tests: IndexProtoMappingsSpec, AnalysisPayloadMappingsSpec, IndexAnalyzerSpec updated
