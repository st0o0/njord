## Context

The 2026-10-08 E2E test run exposed 3 code bugs: (1) `SetBudget(0)` accepted
without validation, poisoning the entire system; (2) config arrays (horizons,
thresholds) accumulate instead of replacing when env vars coexist with the
override file; (3) ha-njord's `_ALERT_TYPE_MAP` is incomplete, leaving 5 of 14
alert sensors stuck at "unknown". Plus 4 test-plan expectations that don't match
the implemented design.

## Goals / Non-Goals

**Goals:**
- Reject invalid budget values before they reach persistence
- Ensure `WritableNjordOptions` array mutations fully replace, never accumulate
- Complete the ha-njord alert type mapping
- Correct E2E test plan expectations to match actual behavior

**Non-Goals:**
- Redesigning the writable-options pattern (incremental fix only)
- Changing usage sensor units or HA unit conversion behavior

## Decisions

### D1: SetBudget validation — guard before mutation

Add a validation guard at the top of `AdminGrpcService.SetBudget()`, before
the `writableOptions.Update(...)` call. Reject when either provided field is
≤ 0:

```
if (request.HasRequestsPerMonth && request.RequestsPerMonth <= 0)
    return Rejected("requests_per_month must be greater than zero");
if (request.HasRequestsPerMinute && request.RequestsPerMinute <= 0)
    return Rejected("requests_per_minute must be greater than zero");
```

**Why here, not in the validator:** The `NjordOptionsValidator` already catches
the downstream effect (projected usage exceeds 0-budget guard), but that
exception is unrecoverable — it fires on every subsequent `IOptionsMonitor`
resolve. Validating at the API boundary prevents the bad value from ever
reaching persistence.

**Alternative considered:** Adding a `BudgetOverride` validator in
`NjordOptionsValidator`. Rejected because: (a) the validator throws
`OptionsValidationException` which is hard to recover from once the value is
persisted; (b) the API should reject bad input, not rely on downstream
validation.

### D2: Array accumulation — clear env-var indices with a sentinel

The root cause: `Microsoft.Extensions.Configuration` merges array-typed
properties by **index key**. When the override file writes `Horizons[0]=6,
Horizons[1]=24` (2 entries) and env vars define `Horizons[0..5]` (6 entries),
the binder produces 6 entries — the file wins for indices 0-1, env vars fill
indices 2-5. Worse: the next `Update()` call clones the merged 6-entry array
and writes it back, which then merges with env vars to produce 12 entries.

**Fix: write array-clear sentinels before the values.** Before serializing
any array property that was mutated, emit enough `null` entries after the real
values to cover the maximum env-var index. Since the override file has higher
priority, its `null` entries override the env-var entries at those indices.
The options binder ignores null array elements, producing a clean array.

Actually, that won't work — JSON `null` in an array is still bound as an
element by the config system.

**Revised fix: re-add env vars with lower priority than the override file.**
Currently `Program.cs` does:

```
builder.Configuration.AddJsonFile("data/appsettings.Override.json", ...);
```

The default builder adds env vars before this, so the override file already
has higher priority. But array merging is the issue — not priority.

**Actual fix: serialize only the mutation delta, not the full options clone.**
`WritableNjordOptions.Update()` currently serializes the **entire** cloned
`NjordOptions`. This captures every array at its current merged length. On
reload the file's full-length arrays merge with env vars producing
duplicates.

Instead, track which properties the mutation lambda actually changed by
comparing the clone before/after the `applyChanges` callback, and serialize
only the changed properties. This means the override file contains only the
explicit mutations, and unchanged properties (like horizons when only alerts
are toggled) never appear in the file — they keep their env-var values
without duplication.

**Implementation:** Use a before/after diff approach in
`WritableNjordOptions.Update()`:

1. `DeepClone` the current options → `before`
2. `DeepClone` again → `after`
3. Apply `applyChanges(after)`
4. Build a `Dictionary<string, object?>` containing only the properties
   where `before` ≠ `after` (use JSON serialization comparison per
   property)
5. Serialize the delta dictionary under the `"Njord"` section key
6. Write to the override file and reload

For arrays specifically: when the delta includes an array property, the
file wins for all indices it covers (higher priority). Indices beyond the
file's array length still come from env vars — but since the file now
contains only the *intended* values (not the previously-merged ones),
this is the correct behavior.

**Edge case:** When the first mutation touches horizons and the second
touches only alerts, the second mutation's delta won't include horizons.
But the override file from the first mutation already has the correct
horizons. We need to **merge** the new delta into the existing override
file rather than replacing it.

**Revised implementation:**

1. Read the existing override file (if any) → `existingOverrides`
2. `DeepClone` current options → `before`
3. `DeepClone` again → `after`; apply `applyChanges(after)`
4. Diff `before` vs `after` → `changedProperties`
5. Merge `changedProperties` into `existingOverrides`
6. Write merged result to file and reload

This ensures each mutation adds only its own delta to the file, and
previous mutations' overrides persist.

### D3: ha-njord alert type map — add missing entries

Extend `_ALERT_TYPE_MAP` in `ha-njord/custom_components/njord/grpc_client.py`
with the 5 missing entries:

```python
_ALERT_TYPE_MAP: dict[int, str] = {
    0: "unspecified",
    1: "frost",
    2: "heat",
    3: "storm",
    4: "heavy_rain",
    5: "uv",
    6: "fog",
    7: "snow",
    8: "pressure_drop",
    9: "thunderstorm",
    10: "ice",           # ADDED
    11: "wind_chill",    # ADDED
    12: "visibility",    # ADDED
    13: "tropical_night", # ADDED
    14: "humidity",      # ADDED
}
```

The enum values 10-14 match `common.proto`'s `AlertType` definition.

### D4: E2E test plan corrections

Four pass-criteria corrections in `e2e/E2E-TEST-PLAN.md`:

1. **S9.3-4 (usage unit):** Change expected `unit_of_measurement` from
   `"requests"` to `"%"`. The `budget-usage-sensors` spec explicitly
   defines usage as a percentage.

2. **S7.3 (wind speed unit):** Change expectation to accept `wind_speed_unit`
   as whatever HA's configured unit system produces (typically km/h for
   metric-DE). Or check `native_wind_speed_unit` = "m/s" instead.

3. **S16.3-5 (multi-cycle timestamp):** Change the polling target from
   `weather.lucerne_icon_d2` to an enrichment entity like
   `sensor.lucerne_weather_trend`, whose `last_updated` advances even when
   forecast data is unchanged.

4. **S23.2-3 (disconnect timing):** Increase the post-stop wait from 10 s
   to 60 s and document that gRPC keepalive / `expire_after` makes
   disconnect detection slower.

## Risks / Trade-offs

- **[D2 complexity]** The delta-serialization approach is more complex than
  full-clone. If the diff misses a property, the mutation won't persist.
  → Mitigate with comprehensive tests for all property types (scalar,
  array, nested object).
- **[D2 existing overrides]** If a corrupted override file already exists
  from the accumulation bug, the first post-fix mutation will re-serialize
  a delta over the bad file content.
  → Mitigate by clearing/rewriting the file on first start after upgrade
  (or accept that a manual volume wipe fixes it, as in Docker).
- **[D3 cross-repo]** The ha-njord fix is in a separate repo. Both repos
  need coordinated releases.
  → Low risk: the fix is additive (new map entries), backward compatible.
