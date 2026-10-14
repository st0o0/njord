## 1. Guard spec (TDD red)

- [ ] 1.1 Add `TimeFactor` and `RealTimeWaitMax` to `src/Njord.Tests.Shared/TestTimeouts.cs` without changing `AwaitAssertMax` (still 15 s) so the guard compiles
- [ ] 1.2 Add `src/Njord.Core.Tests/TestTimeoutsSpec.cs` (sealed, TestKit with `AddTestTimefactor()`, `[Fact(Timeout = TestTimeouts.Hosted)]` / plain `[Fact]`): configured timefactor equals `TestTimeouts.TimeFactor`; dilated `AwaitAssertMax` + 10 s <= `Hosted`; `RealTimeWaitMax` equals dilated `AwaitAssertMax` and + 10 s <= `Hosted`; dilated TestKit default + 10 s <= `Hosted`
- [ ] 1.3 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "Njord.Core.Tests.TestTimeoutsSpec"` and confirm the `AwaitAssertMax` and `RealTimeWaitMax` facts fail with the old 15 s value

## 2. Fix the constants (TDD green)

- [ ] 2.1 Set `AwaitAssertMax` to 5 s (dilated 15 s) in `src/Njord.Tests.Shared/TestTimeouts.cs` and update its comments
- [ ] 2.2 Make `AddTestTimefactor()` in `src/Njord.Tests.Shared/TestTimefactorConfig.cs` read `TestTimeouts.TimeFactor`
- [ ] 2.3 Re-run `TestTimeoutsSpec` and confirm it passes

## 3. Align specs with the budget

- [ ] 3.1 `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`: real-time waits (`Task.WaitAsync`, deadline loop, failure messages) use `TestTimeouts.RealTimeWaitMax`
- [ ] 3.2 `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs`: async facts use `TestTimeouts.Hosted`; the raw 10 s `AwaitConditionAsync` uses `TestTimeouts.AwaitAssertMax`
- [ ] 3.3 `src/Njord.Pipeline.Tests/PollSchedulerSpec.cs`: the raw 5 s `AwaitConditionAsync` bound uses `TestTimeouts.AwaitAssertMax`
- [ ] 3.4 Grep all test projects again for raw `TimeSpan` bounds passed to dilated TestKit waits that, dilated, exceed their xUnit timeout minus the margin; fix any found

## 4. Docs

- [ ] 4.1 Update the test-conventions paragraph in `AGENTS.md` to the corrected rule (explicit durations dilate; `AwaitAssertMax` is un-dilated; `RealTimeWaitMax` for non-dilated waits; guard spec)
- [ ] 4.2 Update `.claude/skills/njord-actor-spec/SKILL.md` to match

## 5. Validation

- [ ] 5.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings and 0 errors
- [ ] 5.2 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` and the full loop from AGENTS.md twice; all pass, record wall-clock duration per run
- [ ] 5.3 Update "Current total" in `AGENTS.md`; no `*.verified.*` changes
