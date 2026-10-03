## 1. Proto: common.proto — New Messages and IndexUpdate Redesign

- [x] 1.1 Add `ScoreEnvelope` message to `protos/njord/v2/common.proto` with fields: `int32 min = 1`, `int32 max = 2`, `double confidence = 3`
- [x] 1.2 Add `FrostInfo` message with fields: `int32 hours_until_frost = 1`, `double confidence = 2`
- [x] 1.3 Add `VpdInfo` message with fields: `double kpa = 1`, `string category = 2`
- [x] 1.4 Add `DayScoreSet` message with fields: `int32 day_offset = 1`, 8 score fields (laundry=2..solar=8, `int32 night_ventilation = 9`), `int32 hours_included = 10`, 8 optional `ScoreEnvelope` envelope fields (laundry_envelope=11..night_ventilation_envelope=18)
- [x] 1.5 Replace `IndexUpdate` message: remove all flat score fields, reserved statements, and energy comments. New body: `repeated DayScoreSet days = 1`, `optional FrostInfo frost = 2`, `optional VpdInfo vpd = 3`
- [x] 1.6 Remove the `// EnergyUpdate and CopOptimalHour removed` comment from `common.proto`

## 2. Proto: weather.proto — Remove Energy Reserved Fields

- [x] 2.1 Remove `reserved 5; reserved "energy";` from `GetEnrichmentsResponse` in `protos/njord/v2/weather.proto`, renumber fields sequentially (derived→5, history→6, consensus→7, consensus_updated_at→8)
- [x] 2.2 Remove `// field 13 reserved (energy removed)` comment from `EnrichmentEvent` oneof, renumber oneof fields sequentially (alerts=10, indices=11, trends=12, derived=13, history=14, consensus=15)

## 3. Proto: admin.proto — Remove Energy and Degree-Day Reserved Fields

- [x] 3.1 Remove `reserved 6; reserved "energy";` from `DetailedEnrichmentConfig` in `protos/njord/v2/admin.proto`, renumber: history→6
- [x] 3.2 Remove `reserved 2, 3; reserved "heating_base_temp", "cooling_base_temp";` from `IndexConfig`, renumber all fields sequentially starting from 1 (enabled=1, indoor_temp=2, ideal_outdoor_temp=3, ..., bbq_ideal_wind_high=12)
- [x] 3.3 Remove `reserved 6; reserved "energy";` from `SetEnrichmentRequest`, renumber: history→6
- [x] 3.4 Remove `// EnergyConfig removed` comment from `admin.proto`

## 4. Mapper: EnrichmentProtoMapper.MapIndices

- [x] 4.1 Rewrite `MapIndices` in `src/Njord/Grpc/EnrichmentProtoMapper.cs` — iterate `result.Days`, create proto `DayScoreSet` per entry with all scores + `night_ventilation` + `hours_included` + envelopes. Map `FrostProtection` → `FrostInfo`, `Vpd` → `VpdInfo` on the `IndexUpdate`
- [x] 4.2 Add private helper `MapEnvelope(ScoreEnvelope?) → V2.ScoreEnvelope?` to keep the mapper DRY

## 5. Tests

- [x] 5.1 Update `EnrichmentProtoMapperSpec` in `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs` — verify `MapIndices` produces `Days` list with correct scores, envelopes, frost, VPD; verify null frost/VPD omitted
- [x] 5.2 Update any other tests that construct `IndexUpdate` or reference old proto field names (`Ventilation`, `FrostHours`, etc.)

## 6. Validation

- [x] 6.1 Build succeeds: `dotnet build Njord.slnx` from `src/`
- [x] 6.2 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 6.3 Run `dotnet slopwatch` from repo root
- [x] 6.4 Run `dotnet format --verify-no-changes` from `src/`
