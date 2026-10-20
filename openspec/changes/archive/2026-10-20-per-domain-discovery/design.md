## Context

PollWorker has a Discovery/Steady state machine for learning the weather model's update cycle. After the v2 redesign, the worker fetches multiple domains but only hashes weather. Other domains are always polled at PollInterval, regardless of their actual update frequency.

## Goals / Non-Goals

**Goals:** Per-domain hash tracking, per-domain cycle learning, staggered per-domain polling.
**Non-Goals:** Per-domain config (all automatic), changing the global DiscoveryInterval.

## Decisions

### D1: Extend DomainState with cycle-learning fields

Each entry in `PollWorkerState.DomainStates` gains:
- `string? LastHash` — response hash for change detection
- `TimeSpan? LearnedCycle` — discovered update interval (null in Discovery)
- `PollPhase Phase` — Discovery or Steady
- `int MissCount` — consecutive polls with no hash change
- `DateTimeOffset? LastChanged` — when the hash last changed (for cycle measurement)
- `DateTimeOffset NextPollAt` — when this domain is next due

### D2: Timer tick checks per-domain due times

Instead of "tick → poll all domains", the tick evaluates: for each enabled domain, is `now >= domain.NextPollAt`? Only due domains are fetched. The timer interval becomes `min(all domains' NextPollAt) - now`, or DiscoveryInterval as fallback.

### D3: Cycle learning per domain

When a domain response hash changes:
- Measure elapsed since last change → candidate cycle
- After 2-3 consistent measurements → transition to Steady with learned cycle
- In Steady: NextPollAt = last poll + learnedCycle - small buffer
- After N consecutive misses in Steady → fall back to Discovery

### D4: Persistence carries per-domain state

`PersistedDomainState` gains `Phase`, `LearnedCycle`, `MissCount`, `LastHash`. On recovery, the worker resumes with the learned cycles — no re-discovery needed after restart.

## Risks / Trade-offs

- **[Complexity increase in PollWorker]** → Mitigated by the state-record pattern: all logic in pure DomainState extensions, actor is thin shell.
- **[Timer precision]** → .NET timers aren't precise; a 30s buffer on learned cycles handles jitter.
