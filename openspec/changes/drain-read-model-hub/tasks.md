## 1. Failing specs first (TDD)

- [x] 1.1 In `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`, let the test pipeline expose its hub source so a spec can attach a second consumer next to the read model (see design D3)
- [x] 1.2 Add `The_read_model_keeps_consuming_when_no_streaming_client_is_attached`: offer about 50 analysis events with no streaming client; assert with a bounded wait that `QueryLatestAnalysis` returns the last one and that the second consumer received all 50 in order
- [x] 1.3 Add `A_streaming_client_that_does_not_pull_never_holds_up_the_read_model`: request a read-model stream without running it, offer 300 events, assert as in 1.2; then run the client's source and assert it receives at most 256 events, the last of which is the last event offered
- [x] 1.4 Add `A_streaming_client_that_keeps_up_receives_every_event_in_order`: attach and run a client before offering events, offer 50, and assert that all 50 arrive in order
- [x] 1.5 Run the suite and confirm 1.2 and 1.3 fail on the current code. If 1.2 passes, record the observed hub behavior in `design.md` (D1) before continuing. Commit the failing specs only together with the fix (2.x), never on their own

Conventions for 1.2–1.4: sealed `Spec` class, BDD names, `[Fact(Timeout = TestTimeouts.Hosted)]`, bounded `AwaitAssertAsync(..., TestTimeouts.AwaitAssertMax)`, `TestContext.Current.CancellationToken` on every await, no `Thread.Sleep`.

## 2. Implementation

- [x] 2.1 `src/Njord.Pipeline/ForecastReadModel.cs` `MaterializeGraph`: run the pre-materialized hub source into `Sink.Ignore<PipelineEvent>()` (design D1)
- [x] 2.2 `src/Njord.Pipeline/ForecastReadModel.cs` `HandleStreamRequest`: insert `.Buffer(256, OverflowStrategy.DropHead)` before `StreamRefs.SourceRef<PipelineEvent>()`, with the capacity as a named constant (design D2)
- [x] 2.3 Specs 1.2–1.4 and the existing `ForecastReadModelSpec` cases pass

## 3. Documentation

- [x] 3.1 `AGENTS.md` architecture guardrails ("Consumers are output adapters via SourceRef"): add one sentence that the read model's streaming hub has a permanent drain and that each streaming client gets a bounded drop-oldest buffer

## 4. Validation

- [x] 4.1 From `src/`: `dotnet build Njord.slnx` gives 0 warnings and 0 errors
- [x] 4.2 From `src/`: `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` passes
- [x] 4.3 From `src/`: `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build` pass
- [x] 4.4 From `src/`: run the full loop (`for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done`); all tests pass and the total is 971 plus the new specs
- [x] 4.5 `git diff --stat` shows no `*.verified.*` change; update the test total in `AGENTS.md` ("Current total")
- [x] 4.6 From the repo root: `openspec validate --all --no-interactive` passes
