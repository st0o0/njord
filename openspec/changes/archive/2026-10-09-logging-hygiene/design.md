## Context

Three distinct logging issues found during cross-codebase review against FunkArr, all
violating FunkArr's clean logging discipline.

## Goals / Non-Goals

**Goals:**
- All log templates use named placeholders (structured logging compatible).
- Routine operations demoted to Debug; Info reserved for lifecycle/noteworthy changes.
- Timeout handling in OpsGrpcService surfaces the failure to callers.

**Non-Goals:**
- Not adding new log statements.
- Not changing Serilog configuration.

## Decisions

1. **Named placeholders**: replace `{0}`, `{1}` with named alternatives matching the
   parameter names (e.g. `{PersistenceId}`, `{SequenceNr}`, `{Location}`). Mechanical
   change, no judgment calls.

2. **Log-level demotion**: change routine-operation log calls from `_log.Info(...)` to
   `_log.Debug(...)` in `MqttDiscoveryActor` (7 call sites), `EnrichmentActor` (2),
   `SchedulerActor` (3). Keep Info for: actor started, actor stopped, config reloaded,
   pipeline resolved, MQTT connected/disconnected. Judgment call on "noteworthy state
   changes" — resolved by: if it happens every poll cycle or every discovery cycle, it's
   Debug. If it happens at most once per config load, it's Info.

3. **OpsGrpcService timeout handling**: return a gRPC `StatusCode.OK` response with an
   explicit `partial` bool field (or an `unavailable_sections` repeated field) set when an
   actor didn't respond, instead of silently returning zero values. This is a minimal
   wire-format addition to `StatusResponse` in the proto definition (`bool budget_partial`,
   `bool models_partial`). Alternative: throw `StatusCode.Unavailable`. Rejected — the
   status endpoint should be resilient and return what it can; callers need to know which
   parts are missing, not get a full failure.

4. **Exception objects in log calls**: pass the `Exception` as the first argument to
   `_log.Warning(ex, "...")` instead of interpolating `ex.Message`.

## Risks / Trade-offs

- [Risk] Adding `partial` fields to `StatusResponse` is a proto schema addition (additive,
  not breaking). → Old clients ignore the new fields; new clients can check them.
- [Risk] Demoting Info→Debug may reduce visibility for operators who only watch Info. →
  Mitigated by keeping lifecycle events at Info; document the log-level policy in AGENTS.md.

## Migration Plan

Single deploy. Proto change is additive. Log-level changes take effect immediately.

## Open Questions

None.
