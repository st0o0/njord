## Why

The Flood API returns HTTP 200 with all-null discharge values for locations without a modeled river (e.g. Barcelona 41.39/2.17). The gateway classifies this as `MalformedPayload` - a misleading name that suggests broken JSON - logs it as WRN, and retries with exponential backoff. The retries never succeed because the data is structurally absent, not transiently unavailable. The same pattern applies to Marine for inland locations and potentially other domains. Each wasted retry consumes a budget slot.

## What Changes

- Add `PollFailureReason.NoData` to distinguish "the API has no data for this location/domain" from `MalformedPayload` (genuinely unparseable responses).
- Add `PollPhase.Suspended` so a domain that returns no data stops the exponential retry loop and rechecks every 24 hours instead.
- Add `PersistedDomainPollOutcome.NoData` and `PersistedPollPhase.Suspended` so the suspended state survives restarts.
- Gateway ReplySink gets an explicit `FetchEmpty` case instead of falling through to the `_` fallback.
- PollWorker logs `NoData` at Info level (not Warning) and does not increment `ConsecutiveFailures`.
- If a 24-hour recheck returns data, the domain resets to Discovery and learns its cycle from scratch.

## Non-goals

- Preventing the operator from enabling Flood/Marine for locations that will never have data. That is a config-time concern, not a runtime one.
- Proactive detection of "will this location have data" before the first fetch. The 24-hour recheck handles late model additions.
- Changing the budget accounting for suspended domains (they already cost nothing while not polling).

## Capabilities

### New Capabilities
- `nodata-domain-suspension`: Covers the NoData failure reason, the Suspended poll phase, 24-hour recheck behavior, recovery back to Discovery, and the gateway FetchEmpty classification.

### Modified Capabilities
- `poll-failure-backoff`: NoData outcomes bypass the exponential backoff entirely - they use a fixed 24-hour delay with no failure count increment.

## Impact

- **Messages**: `PollFailureReason` gains `NoData`; `PollPhase` gains `Suspended`.
- **Persistence**: `PersistedPollPhase` gains `Suspended = 2`; `PersistedDomainPollOutcome` gains `NoData` with an explicit int value. Wire-format tests must be updated.
- **OpenMeteo**: Gateway ReplySink adds one pattern-match arm.
- **Polling**: `PollWorker.ToDomainPolled`, `PollWorkerStateExtensions.Apply`, `PollWorker.LogDomainOutcome` each gain a NoData/Suspended case.
- **Budget**: Net positive - eliminates repeated Flood fetches (~1 call/hour per affected location) that never produce data.
- **API budget estimate**: For a single location with one permanently-empty domain, this change saves ~720 calls/month (hourly retries that plateau at the poll interval) and replaces them with ~30 calls/month (daily rechecks). No new API calls are introduced.
