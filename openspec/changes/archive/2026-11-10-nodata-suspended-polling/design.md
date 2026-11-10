## Context

When a domain API returns HTTP 200 but the response contains no usable data (all values null, no daily block, etc.), the `FloodResponseMappingExtensions.ToDomainForecast()` returns null, which `ClientRequest.ExecuteAsync` wraps as `FetchEmpty`. The gateway's ReplySink switch has no arm for `FetchEmpty`, so it falls through to the `_` default and reports `MalformedPayload`. The PollWorker then applies exponential backoff that never resolves because the data is structurally absent.

The change touches four layers (Messages, Persistence, OpenMeteo, Polling) but each layer gets a small, local modification. No new persistence events, no new actor messages, no stream graph changes.

## Goals / Non-Goals

**Goals:**
- Correctly classify empty-but-valid API responses as `NoData` instead of `MalformedPayload`
- Stop wasting budget on retries that will never succeed
- Let suspended domains self-heal if the upstream model gains coverage (24-hour recheck)
- Persist the suspended state so restarts do not reset it

**Non-Goals:**
- Config-time validation of whether a location has data for a domain
- Making the 24-hour recheck interval configurable (constant is sufficient for now)
- Changing the reporting/health-check behavior for suspended domains

## Decisions

### Decision: New enum members instead of reusing existing values

Add `PollFailureReason.NoData`, `PollPhase.Suspended`, `PersistedPollPhase.Suspended = 2`, and `PersistedDomainPollOutcome.NoData` with explicit int values.

**Why not reuse `MalformedPayload` with a flag?** The semantics are fundamentally different: MalformedPayload means "could not parse", NoData means "parsed correctly but empty". Logging, retry behavior, and operator interpretation all differ. Separate enum members make the switch exhaustive at compile time.

### Decision: Suspended as a PollPhase, not a failure counter threshold

A domain in the `Suspended` phase polls every 24 hours. The alternative was to cap retries after N consecutive `NoData` failures, but that would still waste N budget slots per location per domain and conflate two meanings of `ConsecutiveFailures`.

`Suspended` is a first-class phase alongside `Discovery` and `Steady`. The existing timer and scheduling infrastructure (`NextPollAt`, `ScheduleDomain`) handles it without any special-case code - `Suspended` simply sets `NextPollAt = now + 24h`.

### Decision: Recovery resets to Discovery

When a 24-hour recheck returns data, the domain transitions to `Discovery` (not directly to `Steady`). This reuses the existing cycle-learning logic and is consistent with how a brand-new domain starts. The alternative of jumping to `Steady` with a guessed cycle would be fragile.

### Decision: 24-hour recheck as a constant

The recheck interval is a `static readonly TimeSpan` in `PollWorkerState` (like `RateLimitMinDelay`), not a config option. Upstream model coverage changes are rare; 24 hours is a reasonable balance between detecting new data and not wasting budget. If needed later, it can be promoted to `PollingOptions`.

### Decision: NoData does not increment ConsecutiveFailures

NoData is not a transient failure - it is a structural absence. Incrementing the failure counter would incorrectly influence the backoff calculation if the domain later transitions out of Suspended due to a recheck success followed by a real failure.

## Risks / Trade-offs

- **[Risk] A domain that is transiently empty (API maintenance) gets suspended for 24 hours.** Mitigation: This is acceptable. API maintenance typically lasts minutes to hours; a 24-hour window means at most one extra cycle is missed. Transport errors (which are transient) still use the fast exponential backoff.

- **[Risk] New enum values in persistence records.** Mitigation: 0.x allows breaking changes. The new values use explicit int assignments and are appended to the enums. Old snapshots without `Suspended` recover correctly because `PersistedPollPhase.Discovery = 0` is the default.

## Open Questions

None - the design was explored and agreed in conversation before this proposal.
