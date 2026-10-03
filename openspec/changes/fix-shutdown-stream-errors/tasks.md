## 1. Red: failing specs

- [ ] 1.1 `PipelineActorShutdownSpec` (Akka.Hosting.TestKit, `sealed`, `[Fact(Timeout = 5000)]`): start `PipelineActor` with fakes from `Njord.Tests.Shared` and `FakeTimeProvider`, send `StopStreams`, expect `StreamsStopped`; capture logs with a TestKit `EventFilter.Exception<AbruptTerminationException>().Expect(0, ...)` (or a test logger via `ConfigureLoggers`) around the stop and the subsequent system shutdown; assert zero hits
- [ ] 1.2 `EgressActorShutdownSpec`: same for `EgressActor` (`egress-hub` stage), also asserting a connected SourceRef consumer sees stream completion
- [ ] 1.3 Spec that CoordinatedShutdown run on the ActorSystem (`CoordinatedShutdown.Get(Sys).Run(...)`) with both actors started via the host setup completes without `AbruptTerminationException` and within the timeout; plus an unresponsive-actor case that logs a warning and still completes
- [ ] 1.4 Confirm all new specs fail for the right reason (error currently logged)

## 2. Green: fix per actor

- [ ] 2.1 Add `StopStreams` / `StreamsStopped` / `StreamsStopFailed` records next to `PipelineActor` and `EgressActor` (Pattern B)
- [ ] 2.2 `PipelineActor`: insert `UniqueKillSwitch` behind the MergeHub source, keep completion tasks (fetch graph and hash consumer), handle `StopStreams` in Initializing (stash) and Ready, reply via `PipeTo` with success/failure mappers
- [ ] 2.3 `EgressActor`: same for the egress-hub graph
- [ ] 2.4 Host: register the CoordinatedShutdown task in `NjordActorSystemSetup` (`before-service-unbind`, Pipeline then Egress, 5 s ask timeout, warning on failure)
- [ ] 2.5 Update `AGENTS.md`/docs only if they state "no KillSwitch" (grep), then run the full suite and `dotnet slopwatch`

## 3. Smoke check

- [ ] 3.1 Start the service with `ASPNETCORE_ENVIRONMENT=Development`, wait until the pipeline is Ready, send SIGTERM, count ERR lines in the output (expected 0), verify the process exited and no process is left running (`pgrep -a Njord`)
