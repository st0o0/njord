## Context

`DayScoreSet` has 18 positional params (8 score ints + 8 optional envelopes + DayOffset + HoursIncluded). `IndexAnalyzer.ScoreDay()` already builds a `Dictionary<IndexKind, ScoredIndex>` and destructures it. Both consumers (gRPC, MQTT) reconstruct the pairing from named fields.

## Goals / Non-Goals

**Goals:**
- `DayScoreSet` becomes `(int DayOffset, IReadOnlyDictionary<IndexKind, ScoredIndex> Scores, int HoursIncluded)`
- `IndexAnalyzer` passes the dictionary directly
- Consumers iterate the dictionary

**Non-Goals:**
- Changing wire format
- Changing `ScoredIndex` or `ScoreEnvelope`

## Decisions

### D1: Dictionary replaces named fields

The `ScoredIndex(int Value, ScoreEnvelope? Envelope)` record already exists and carries exactly the pair of (score, envelope) that each named field represented. The dictionary key is `IndexKind`.

### D2: Mappers iterate in enum order for deterministic output

gRPC proto mapping iterates `Enum.GetValues<IndexKind>()` to produce the repeated `DayScore` field in consistent order. MQTT mapping does the same for the score array. This preserves wire compatibility.

### D3: IndexAnalyzer removes destructuring

Current: `new DayScoreSet(offset, scored[Laundry].Value, scored[Outdoor].Value, ..., hours, scored[Laundry].Envelope, ...)`
After: `new DayScoreSet(offset, scored, hours)`

## Risks / Trade-offs

- **[Risk]** Proto field ordering depends on enum order → **Mitigation**: `IndexKind` enum members are explicitly ordered and tested by `IndexKindSpec`.
- **[Risk]** Golden masters may need re-approval → **Mitigation**: Wire output is unchanged; only the C# structure differs. If the iteration order matches, snapshots stay valid.
