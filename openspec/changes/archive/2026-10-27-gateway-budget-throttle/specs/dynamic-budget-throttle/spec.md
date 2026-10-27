## ADDED Requirements

### Requirement: Native cost-based throttle shapes fetch requests before the budget gate
The gateway stream SHALL include a `Throttle` stage (Akka.Streams built-in, `ThrottleMode.Shaping`) positioned between `Source.Queue` and the budget gate `Select`. The throttle SHALL use cost-based weighting where each element's cost equals the domain client's `CallWeight`. The rate SHALL match the budget provider's `CostPerMinute` and the `maximumBurst` SHALL be at least `max(client.CallWeight)` across all registered domain clients. When the rate budget is exceeded the throttle SHALL delay (backpressure) elements instead of dropping or rejecting them.

#### Scenario: Light domains pass without delay
- **WHEN** AirQuality, Flood, Marine, Seasonal and Satellite fetches (weight=1 each) arrive and the token rate supports them
- **THEN** the throttle emits them without measurable delay

#### Scenario: Heavy Weather fetch is delayed instead of rejected
- **WHEN** a Weather fetch with CallWeight=18 arrives immediately after lighter domains consumed available tokens
- **THEN** the throttle delays it until sufficient cost budget accumulates, then emits it to the gate

#### Scenario: Burst of multi-location Weather fetches is serialized
- **WHEN** 3 locations each send a Weather fetch (weight=18) simultaneously at startup
- **THEN** the throttle admits them one at a time as cost budget refills, with no hard rejections

## MODIFIED Requirements

### Requirement: The gateway budget gate encapsulates the token bucket and usage tracking
`IGatewayBudgetGate` SHALL expose `TryAcquire(int weight)`. Its implementation `GatewayBudgetGate`, created by `OpenMeteoGateway` with the `IBudgetProvider`, the `IBudgetTracker` actor ref and the `TimeProvider`, SHALL keep the token bucket in memory, refill it from the elapsed time, and refresh its rate from `IBudgetProvider` at most every 5 seconds. After each successful acquisition it SHALL send `RecordApiCall(weight)` to the `BudgetTracker` via fire-and-forget `Tell`. The `MaxBurst` SHALL be at least `max(client.CallWeight)` across all registered domain clients so that a single high-weight fetch can always be admitted when the bucket is full. There SHALL be no generic `IBudgetGate<T>` and no `WeightedBudgetGate`.

#### Scenario: Gate acquires immediately when tokens available
- **WHEN** the token bucket holds enough tokens for the weight
- **THEN** `TryAcquire` returns true and sends `RecordApiCall` to the budget tracker

#### Scenario: Gate rejects when tokens insufficient
- **WHEN** the token bucket does not hold enough tokens
- **THEN** `TryAcquire` returns false and sends no `RecordApiCall`

#### Scenario: Gate adapts to rate changes
- **WHEN** `IBudgetProvider` returns a new rate
- **THEN** `TryAcquire` uses the new rate after the next 5-second refresh

#### Scenario: MaxBurst accommodates the heaviest domain client
- **WHEN** the heaviest domain client has CallWeight=18 and the base burst formula yields 16
- **THEN** `MaxBurst` SHALL be 18, not 16

## REMOVED Requirements

### Requirement: GatewayThrottleStage shapes fetch targets according to the current budget rate
**Reason**: Replaced by the native Akka.Streams `Throttle` operator which provides the same rate-shaping behavior without a custom GraphStage. The built-in operator handles cost-based shaping, backpressure, and burst management natively.
**Migration**: Remove `GatewayThrottleStage` (never implemented). The native `Throttle` stage in the gateway stream replaces it. Admission deadlines and the throttle-wait histogram from this requirement are deferred to a future change if needed.
