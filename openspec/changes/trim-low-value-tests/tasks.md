## 1. Messages.Tests — Remove redundant per-member wire-name rows

- [x] 1.1 SKIPPED — On inspection, per-member Theory rows are NOT redundant: completeness tests only verify coverage (every member has a data row), not mapping correctness (the actual wire string). Removing these would reduce real coverage.
- [x] 1.2 N/A — no changes made.

## 2. Model.Tests — Remove compiler-guaranteed record tests

- [x] 2.1 Removed 6 tests: CycleIdSpec (Timestamp_is_preserved, Equal/Different timestamps equality), ForecastValueSpec (Equality/Inequality), ForecastSpec (entire file — A_forecast_is_fully_attributable). HorizonSpec kept (factory methods with validation). Model 81→76, Forecast 50→49.
- [x] 2.2 Model: 76 pass, Forecast: 49 pass.

## 3. SensorHub.Tests — Remove basic dictionary behavior tests

- [x] 3.1 Removed 6 tests: Empty_state_has_no_readings, Apply_stores_reading, Apply_overwrites_reading_for_same_key, Apply_stores_multiple_keys, GetLocationReadings_is_empty_for_an_empty_state, Expire_returns_same_state_when_nothing_expired. Kept 12 with real logic.
- [x] 3.2 SensorHub: 26 pass.

## 4. TelemetrySpec consolidation — Golden master snapshots

- [x] 4.1–4.5 SKIPPED — On inspection, each project has only 2 compact tests (one Assert.Multiple per project). Total 8 tests, not the estimated 12. Converting to Verify snapshots would be a lateral move with no net benefit.

## 5. Grpc.Tests — Remove redundant enum mapping rows and library test

- [x] 5.1 Removed 6 per-member Theory methods (31 InlineData rows): Poll_phase, Trend_direction, Stability, Dew_point_comfort, Vpd_category, Alert_type mapping tests — all covered by Every_domain_member_has_a_proto_mapping completeness test. Kept Consensus_method (bidirectional, no completeness test) and Alert_severity (no completeness test).
- [x] 5.2 Deleted TimestampConversionSpec.cs (tested Google.Protobuf, not project code).
- [x] 5.3 Grpc: 125 pass.

## 6. Cross-cutting verification

- [x] 6.1 `dotnet build Njord.slnx` — 0 errors, 0 warnings.
- [x] 6.2 Architecture tests: 128 pass (removed stale allow-list entry for CycleIdSpec from TestSourceConventionArchitectureSpec).
- [x] 6.3 Updated AGENTS.md test counts: 1753→1709 (Model 76, Forecast 49, Grpc 125, SensorHub 26).
- [x] 6.4 `dotnet format Njord.slnx --verify-no-changes` — clean.
