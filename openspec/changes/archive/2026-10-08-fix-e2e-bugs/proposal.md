## Why

The 2026-10-08 E2E test run (run 2) exposed 3 code bugs and 4 test-plan
inaccuracies. Two bugs are in njord (budget validation gap, config array
accumulation) and one is in ha-njord (incomplete alert type mapping). Together
they caused 18 of 151 steps to fail (88% pass rate). Fixing them brings the
E2E suite to a reliable baseline.

## What Changes

- **SetBudget input validation** — reject `RequestsPerMonth <= 0` (and
  `RequestsPerMinute <= 0`) with a `Rejected(...)` response instead of
  persisting an invalid budget that poisons every subsequent options resolve.
- **WritableNjordOptions array handling** — prevent
  Microsoft.Extensions.Configuration index-based array merging from doubling
  array values when env vars coexist with the override JSON file.
- **ha-njord `_ALERT_TYPE_MAP` completion** — add missing entries for alert
  types 10–14 (ice, wind_chill, visibility, tropical_night, humidity) so all
  14 alert sensors receive correct state and attributes.
- **E2E test plan corrections** — fix 4 incorrect expectations: usage sensor
  unit is "%" (not "requests"), wind_speed_unit reflects HA unit-system
  conversion, weather entity `last_updated` doesn't advance when data is
  unchanged, HA disconnect detection takes >10 s.

## Non-goals

- Changing the usage sensor design (% is intentional per the
  `budget-usage-sensors` spec).
- Changing how HA converts wind speed units (native unit is already m/s).
- Making history enrichment return data on first poll cycle (needs 48 samples
  by design).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `grpc-v2-admin-service`: SetBudget must reject zero/negative budget values.
- `writable-options`: Array properties must survive config-layer merging
  without duplication when environment variables are present.
- `e2e-test-plan`: Correct 4 pass-criteria that don't match the implemented
  design.

## Impact

- `src/Njord.Grpc/AdminGrpcService.cs` — add validation guard in `SetBudget`
- `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` — new test for rejected budget
- `src/Njord.Core/Configuration/WritableNjordOptions.cs` — fix array merge
- `src/Njord.Core.Tests/Configuration/WritableNjordOptionsSpec.cs` — new test
- `ha-njord: custom_components/njord/grpc_client.py` — extend `_ALERT_TYPE_MAP`
- `ha-njord: tests/` — update alert mapping tests
- `e2e/E2E-TEST-PLAN.md` — correct 4 step expectations
