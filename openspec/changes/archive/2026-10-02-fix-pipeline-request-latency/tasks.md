## 1. Red

- [x] 1.1 Confirm `Requests_have_no_serialization_gap_from_scheduler` fails on Linux (`dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.PipelineConnectionSpec"` from `src/`); this existing failure is the red test
- [x] 1.2 Add a small spec (or helper test) asserting the gap conversion yields real milliseconds, e.g. two timestamps `Stopwatch.Frequency / 100` ticks apart convert to 10 ms; confirm it fails with the current formula

## 2. Green

- [x] 2.1 Replace `(timestamps[i] - timestamps[i - 1]) / (double)TimeSpan.TicksPerMillisecond` with `Stopwatch.GetElapsedTime(timestamps[i - 1], timestamps[i]).TotalMilliseconds` in `PipelineConnectionSpec.cs`
- [x] 2.2 Run `PipelineConnectionSpec` repeatedly (at least 10 runs) and confirm it passes with real gaps well below 1000 ms

## 3. Verify

- [x] 3.1 Run the full suite from `src/` (expect 780/780) and `dotnet slopwatch` from the repo root
