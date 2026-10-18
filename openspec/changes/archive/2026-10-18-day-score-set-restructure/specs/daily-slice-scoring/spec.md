## MODIFIED Requirements

### Requirement: DayScoreSet carries scores as a dictionary keyed by IndexKind

`DayScoreSet` SHALL be `(int DayOffset, IReadOnlyDictionary<IndexKind, ScoredIndex> Scores, int HoursIncluded)`. Each entry maps an `IndexKind` to its `ScoredIndex(int Value, ScoreEnvelope? Envelope)`. The dictionary SHALL contain exactly one entry per `IndexKind` member.

#### Scenario: All index kinds present
- **WHEN** `IndexAnalyzer` scores a day
- **THEN** `DayScoreSet.Scores` SHALL contain exactly 8 entries, one per `IndexKind`

#### Scenario: Score lookup by kind
- **WHEN** a consumer needs the Laundry score
- **THEN** it SHALL access `dayScoreSet.Scores[IndexKind.Laundry].Value`

#### Scenario: Adding a new IndexKind requires no DayScoreSet change
- **WHEN** a new `IndexKind` member is added with its `IIndexScore` implementation
- **THEN** `DayScoreSet` requires no parameter changes — the new kind appears in the dictionary automatically

#### Scenario: Wire format unchanged
- **WHEN** `DayScoreSet` with the dictionary structure is mapped to gRPC proto or MQTT JSON
- **THEN** the output SHALL be identical to the previous named-field format
