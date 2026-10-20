## Why

PollWorker currently hashes only the Weather response to learn the API update cycle. AQ, Flood and Marine update on completely different schedules (AQ hourly, Flood every 6-12h, Marine every 3h) but the worker is blind to their cycles — it polls all domains at the same interval. This wastes API budget: Flood data rarely changes in a 60-minute window, while AQ might update twice.

Per-domain discovery lets each domain independently learn its update cadence, reducing wasted calls and improving data freshness.

## What Changes

- `PollWorkerState.DomainStates` extended with per-domain response hash, learned cycle, miss count and phase (Discovery/Steady).
- Timer tick evaluates which domains are _due_ for a poll based on their individual cadence, not polling all domains every tick.
- Each domain independently transitions Discovery → Steady when it detects a hash change and learns the update interval.
- A domain in Steady polls at its learned cycle. A domain in Discovery polls every `DiscoveryInterval`.
- Persistence events carry per-domain phase/cycle data.
- Budget savings: Weather at 6h cycle + AQ at 1h + Flood at 12h costs far less than all three at 1h.

## Non-goals

- Changing the DiscoveryInterval config property (it stays global as the initial discovery cadence).
- Per-domain config of poll intervals (the system learns them automatically).
- API budget impact: **positive** — fewer wasted calls.

## Capabilities

### New Capabilities

- `per-domain-discovery`: Independent cycle learning per DomainKind with hash tracking, phase transitions and per-domain polling schedules.

### Modified Capabilities

- `multi-domain-polling`: PollWorker polls domains on independent cadences instead of all-at-once.

## Impact

- **Njord.Pipeline**: PollWorker timer logic, PollWorkerState.DomainStates extension.
- **Njord.Persistence**: DomainState persistence records carry phase/cycle/hash.
- **Njord.Core**: Possible new types for domain poll phase.
- **Telemetry**: Per-domain poll metrics (cycle learned, misses).
