## Context

After Changes A (enrichment-type-system) and B (parameter-identity) are
implemented, two domain boundary issues remain: actor protocol messages
living in the Domain layer, and `DailyForecastPoint` using untyped `object?`
values. Both are independent of A and B and can be done in any order.

## Goals / Non-Goals

**Goals:**

- Domain layer contains only domain types — no actor protocol messages.
- `DailyForecastPoint` provides the same type safety as `ForecastPoint`.
- No duplicate types (`ForecastRecorded` eliminated).

**Non-Goals:**

- Changing `ForecastPoint` (hourly) — already correct.
- Restructuring the Domain folder hierarchy beyond the message move.
- Changing enrichment compute logic.

## Decisions

### Decision 1: Actor messages move to a single file next to the actor

`RecordSnapshot`, `QueryHistory`, and `HistoryResponse` move to
`src/Njord/Enrichment/ForecastHistoryMessages.cs`. They stay together in
one file because they form a cohesive actor protocol — command, query,
response.

`ForecastRecorded` is deleted. It is structurally identical to
`ForecastRecord` and adds no semantic value. The `ForecastHistoryActor`
uses `ForecastRecord` directly:
- As the Akka persistence event type (replacing `ForecastRecorded`)
- As the record stored in `ForecastHistory`

**Why not move `ForecastRecord` and `ForecastHistory` too:** These are
domain types — they model what a historical forecast record IS and how
retention works. The actor uses them, but they have meaning independent
of the actor. `QueryHistory` (an empty record meaning "give me your state")
has no domain meaning.

### Decision 2: DailyForecastPoint with two dictionaries

```csharp
public sealed record DailyForecastPoint(
    DateOnly Date,
    IReadOnlyDictionary<ParameterDef, double?> NumericValues,
    IReadOnlyDictionary<ParameterDef, string?> MetaValues)
{
    public double? GetNumeric(ParameterDef param)
        => NumericValues.GetValueOrDefault(param);

    public string? GetMeta(ParameterDef param)
        => MetaValues.GetValueOrDefault(param);
}
```

**Why two dictionaries, not a union type:** A `ForecastValue` union type
(`record struct` with `double? Numeric` + `string? Text`) would keep a
single dictionary but adds 16 bytes per entry (struct with two nullable
fields), makes pattern matching verbose at every access site, and
introduces a new type that callers must learn. Two dictionaries are
simpler: numeric consumers call `GetNumeric`, text consumers call
`GetMeta` — no casting, no pattern matching, no boxing.

**Routing during parsing:** The Open-Meteo response parser already knows
each parameter's `ValueType` (from `ParameterDef`). During daily response
mapping, numeric parameters (`ValueType == Numeric`) go into
`NumericValues`, time/string parameters (`ValueType == TimeString`) go
into `MetaValues`. This is a mechanical change in the daily mapping code.

### Decision 3: ForecastHistoryActor persistence event migration

The actor currently persists `ForecastRecorded` events. After the change,
it persists `ForecastRecord` events. Since `ForecastRecorded` and
`ForecastRecord` have identical fields, the serialised form is compatible
— Akka.Persistence.Sql serialises by type name + fields, and we can add
a type alias or simply accept that old journals use the old type name
(recovery handles both).

For simplicity: since njord is pre-release and the journal is local
SQLite with retention cleanup, old events will naturally age out. No
explicit migration is needed.

## Risks / Trade-offs

- **[DailyForecastPoint consumers need updates]** Every call to the old
  `Get()` method must change to `GetNumeric()` or `GetMeta()`. The compiler
  will catch all of these since the old method is removed. Blast radius is
  small — daily parameters are used mainly in `AlertEvaluator` (precipitation
  sum, sunrise/sunset).

- **[Persistence event rename]** Old `ForecastRecorded` events in the
  journal may fail to deserialise if Akka cannot resolve the old type name.
  Mitigated by the pre-release status and retention cleanup. If needed, a
  manifest alias can map the old name to `ForecastRecord`.
