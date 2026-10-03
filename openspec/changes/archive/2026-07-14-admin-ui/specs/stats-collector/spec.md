# stats-collector Specification

## Purpose

An in-process ring-buffer service that captures snapshots of NjordTelemetry counter and histogram values at regular intervals, providing windowed time-series data for the admin dashboard without requiring an external metrics backend.

## ADDED Requirements

### Requirement: StatsCollector captures telemetry snapshots at regular intervals
The `StatsCollector` SHALL be an `IHostedService` that reads the current values of all `NjordTelemetry` instruments (counters, histograms, up-down counters) every 30 seconds and stores them in an in-memory ring buffer.

#### Scenario: Snapshot captured every 30 seconds
- **WHEN** 30 seconds elapse since the last snapshot
- **THEN** a new snapshot of all instrument values is added to the ring buffer

#### Scenario: Service starts and stops cleanly
- **WHEN** the host starts
- **THEN** the `StatsCollector` begins capturing; when the host stops, the timer is disposed

### Requirement: Ring buffer retains a configurable time window
The ring buffer SHALL retain snapshots for a configurable duration (default 24 hours). Snapshots older than the retention window SHALL be evicted on each capture cycle. At 30-second intervals, 24 hours requires ~2,880 entries per metric.

#### Scenario: Old snapshots evicted
- **WHEN** a snapshot is older than 24 hours
- **THEN** it is removed from the ring buffer on the next capture cycle

#### Scenario: Buffer size is bounded
- **WHEN** the service has been running for 48 hours
- **THEN** the buffer contains at most ~2,880 entries per metric (24 hours worth)

### Requirement: StatsCollector captures per-dimension counter values
For counters with dimensions (e.g., `fetch.total` with `location` and `model` tags), the collector SHALL capture per-dimension breakdowns. The time-series query SHALL support optional dimension filters.

#### Scenario: Per-model fetch counts captured
- **WHEN** `njord.fetch.total` has values for `(lucerne, icon_d2)` and `(lucerne, ecmwf_ifs025)`
- **THEN** both dimension combinations are captured as separate time-series

#### Scenario: Querying with dimension filter
- **WHEN** a time-series query requests `fetch.total` filtered by `model=icon_d2`
- **THEN** only the `icon_d2` dimension's values are returned

### Requirement: StatsCollector captures histogram percentiles
For histogram instruments (e.g., `fetch.duration`), the collector SHALL capture P50, P95, and P99 percentile values at each snapshot interval.

#### Scenario: Fetch duration percentiles captured
- **WHEN** a snapshot is captured for `njord.fetch.duration`
- **THEN** the snapshot includes P50, P95, and P99 values

### Requirement: StatsCollector uses MeterListener
The `StatsCollector` SHALL use `System.Diagnostics.Metrics.MeterListener` to subscribe to the `"njord"` meter and collect instrument measurements. It SHALL NOT scrape OTLP endpoints or depend on any external metrics infrastructure.

#### Scenario: MeterListener subscription
- **WHEN** the `StatsCollector` starts
- **THEN** it creates a `MeterListener` filtering for the `"njord"` meter

### Requirement: StatsCollector provides a queryable API
The `StatsCollector` SHALL expose methods for querying time-series data:
- `GetTimeSeries(string metricName, TimeSpan window, IDictionary<string, string>? dimensionFilter)` → `IReadOnlyList<TimeSeriesPoint>`
- `GetCurrentValues()` → snapshot of all current counter totals
- `GetBudgetStatus()` → projected and actual API budget usage

#### Scenario: Time-series query with window
- **WHEN** `GetTimeSeries("fetch.total", TimeSpan.FromHours(1))` is called
- **THEN** it returns points from the last hour (up to 120 points at 30-second intervals)

#### Scenario: Current values snapshot
- **WHEN** `GetCurrentValues()` is called
- **THEN** it returns the latest cumulative values for all counters

### Requirement: StatsCollector is thread-safe
The ring buffer and query methods SHALL be thread-safe for concurrent reads (API requests) and writes (snapshot timer). The implementation SHALL use lock-free or minimally-locked data structures.

#### Scenario: Concurrent read and write
- **WHEN** an API request queries time-series while the snapshot timer fires
- **THEN** both operations complete without corruption or deadlock
