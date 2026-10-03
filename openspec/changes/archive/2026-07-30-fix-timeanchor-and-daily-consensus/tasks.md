## 1. Fix TimeAnchor floor rounding

- [x] 1.1 Change `TimeAnchor.AtHorizon` in `src/Njord/Domain/Weather/TimeAnchor.cs` from ceiling to floor: return the truncated hour unconditionally instead of `floored.AddHours(1)`
- [x] 1.2 Update `src/Njord.Tests/Domain/Weather/TimeAnchorSpec.cs`: fix all ceiling-based expectations to floor-based, add scenario for mid-hour h0 returning current hour (not next), add scenario verifying exact-hour input is unchanged

## 2. Switch ConsensusResult.ComputeHourly to exact point lookup

- [x] 2.1 In `src/Njord/Domain/Analysis/ConsensusResult.cs` method `ComputeHourly`: build a `Dictionary<DateTimeOffset, HourlyDataPoint>` per forecast (outside the horizon loop), replace the `FirstOrDefault` ±30-min fuzzy search with `TryGetValue` exact match
- [x] 2.2 Update `src/Njord.Tests/Domain/Analysis/ConsensusResultSpec.cs`: adjust any tests that relied on fuzzy matching or ceiling-anchored times, add regression test verifying h0 consensus median falls within the range of individual model values

## 3. Update DailyConsensusSummary and HorizonProjection tests

- [x] 3.1 Update `src/Njord.Tests/Domain/Analysis/DailyConsensusSummarySpec.cs`: fix any expectations that assumed ceiling-anchored horizon times for day-boundary grouping
- [x] 3.2 Update `src/Njord.Tests/Egress/HorizonProjectionSpec.cs`: verify that HorizonProjection and ConsensusResult now use the same anchor (floor) and the same lookup strategy (exact match)

## 4. Validation

- [x] 4.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 4.2 Run `dotnet slopwatch` from repo root
- [x] 4.3 Run `dotnet format --verify-no-changes` from `src/`
