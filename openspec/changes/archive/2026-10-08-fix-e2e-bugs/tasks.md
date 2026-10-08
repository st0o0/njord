## 1. SetBudget Validation (njord)

- [x] 1.1 Add validation guard in `src/Njord.Grpc/AdminGrpcService.cs` `SetBudget()`: reject `RequestsPerMonth <= 0` and `RequestsPerMinute <= 0` with `Rejected(...)` before calling `writableOptions.Update()`
- [x] 1.2 Add test in `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs`: `SetBudget` with `RequestsPerMonth = 0` returns `applied = false`
- [x] 1.3 Add test: `SetBudget` with `RequestsPerMonth = -100` returns `applied = false`
- [x] 1.4 Add test: `SetBudget` with `RequestsPerMinute = 0` returns `applied = false`
- [x] 1.5 Add test: `SetBudget` with valid `RequestsPerMonth = 300000` and `RequestsPerMinute = 0` returns `applied = false`

## 2. WritableNjordOptions Array Fix (njord)

- [x] 2.1 Refactor `WritableNjordOptions.Update()` to serialize only the changed properties (delta) instead of the full options clone. Compare `before` and `after` snapshots via per-property JSON comparison to identify changed fields.
- [x] 2.2 Merge the delta into the existing override file content (read → merge → write) so previous mutations persist.
- [x] 2.3 Add test in `src/Njord.Core.Tests/Configuration/WritableNjordOptionsSpec.cs`: mutation of array property (e.g. `Horizons`) writes only that property to the override file, not the full options.
- [x] 2.4 Add test: successive mutations (first changes horizons, second changes alerts.enabled) produce an override file containing both changes without array duplication.
- [x] 2.5 Add test: when env vars define `Horizons[0..5]` and a mutation sets `Horizons = [6, 24]`, the resolved options after reload contain exactly `[6, 24]`.

## 3. ha-njord Alert Type Map (ha-njord)

- [x] 3.1 Add entries 10-14 to `_ALERT_TYPE_MAP` in `ha-njord/custom_components/njord/grpc_client.py`: `10: "ice"`, `11: "wind_chill"`, `12: "visibility"`, `13: "tropical_night"`, `14: "humidity"`
- [x] 3.2 Update or add tests for the alert mapping to cover all 14 alert types

## 4. E2E Test Plan Corrections

- [x] 4.1 In `e2e/E2E-TEST-PLAN.md` S9.3-4: change expected `unit_of_measurement` from `"requests"` to `"%"` for monthly_usage and daily_usage
- [x] 4.2 In `e2e/E2E-TEST-PLAN.md` S7.3: change wind speed expectation to accept HA unit-system conversion or check `native_wind_speed_unit` instead of `wind_speed_unit`
- [x] 4.3 In `e2e/E2E-TEST-PLAN.md` S16.3-5: change polling target from `weather.lucerne_icon_d2` to an enrichment entity (`sensor.lucerne_weather_trend`) whose `last_updated` advances even with unchanged forecast data
- [x] 4.4 In `e2e/E2E-TEST-PLAN.md` S23.2-3: increase post-stop wait from 10 s to 60 s and note gRPC keepalive / `expire_after` timing

## 5. Verification

- [x] 5.1 Run `dotnet build Njord.slnx` from `src/` — no errors
- [x] 5.2 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — all pass (78/78)
- [x] 5.3 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` — all pass (133/133)
- [x] 5.4 Run `dotnet slopwatch analyze -d . --fail-on warning` from repo root — skipped (no baseline file)
- [x] 5.5 Run `dotnet format --verify-no-changes Njord.slnx` from `src/` — clean (changed projects verified)
