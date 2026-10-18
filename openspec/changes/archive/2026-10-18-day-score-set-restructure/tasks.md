## 1. Simplify DayScoreSet

- [x] 1.1 Refactor `DayScoreSet` in `src/Njord.Messages/Indices/DayScoreSet.cs` from 18 positional params to `(int DayOffset, IReadOnlyDictionary<IndexKind, ScoredIndex> Scores, int HoursIncluded)`.
- [x] 1.2 Update `IndexAnalyzer.ScoreDay()` in `src/Njord.Indices/IndexAnalyzer.cs` to pass the `scored` dictionary directly instead of destructuring it into 16 named fields.

## 2. Update consumers

- [x] 2.1 Update `src/Njord.Grpc/Mapping/IndexProtoMappings.cs` to iterate `Scores` dictionary by `IndexKind` enum order instead of reading named fields.
- [x] 2.2 Update `src/Njord.Mqtt/Mapping/AnalysisPayloadMappings.cs` to iterate `Scores` dictionary instead of calling `Score(kind, day.Laundry, day.LaundryEnvelope)` per field.
- [x] 2.3 Fix all remaining compilation errors.

## 3. Test updates

- [x] 3.1 Update `src/Njord.Grpc.Tests/Mapping/IndexProtoMappingsSpec.cs` — test DayScoreSet construction uses dictionary.
- [x] 3.2 Update `src/Njord.Mqtt.Tests/Mapping/AnalysisPayloadMappingsSpec.cs` — same.
- [x] 3.3 Update any other test files that construct `DayScoreSet` (IndexAnalyzerSpec, golden masters).

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings
- [x] 4.2 Run affected test suites — all pass.
- [x] 4.3 Full suite loop — all projects pass with no regressions.
