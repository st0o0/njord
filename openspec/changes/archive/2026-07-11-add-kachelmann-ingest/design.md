# Design: add-kachelmann-ingest

## Context

njord is greenfield: the solution scaffold exists (`src/Njord`, `src/Njord.Tests`,
net10.0, CPM with transitive pinning), but no functional code. This change builds
the ingest foundation that the consensus and MQTT/discovery changes will sit on.

Constraints that shape the design:

- Hobby-plan budget of 20,000 requests/month and 60/minute; every wasted request
  is real quota. All Kachelmann plans must be supportable via configuration.
- The `model` query parameter is a free string (OpenAPI documents examples, not
  an enum) — model ids must not be hardcoded as a C# enum.
- `advanced/3h` covers 120 h in one request, so one request per
  location × model × cycle suffices for the +3…+72 h horizons.
- Architecture guardrails from CLAUDE.md: Ingest/Domain/Egress zones, streams
  for data flow, actors for lifecycle, static entity set, `TimeProvider`
  everywhere, never `Zip` model sub-streams.

## Goals / Non-Goals

**Goals:**

- Typed, validated configuration with plan presets and budget projection.
- A domain model that consensus and egress can build on without touching ingest.
- A resilient poll pipeline that degrades gracefully when individual models fail.
- Every piece unit-testable without network or API key.

**Non-Goals:**

- Consensus, MQTT, HA discovery, feels-like/Steadman (later changes).
- HTTP-level retry sophistication (see Decision 5).
- Historical/observation data; only forecasts.

## Decisions

### D1: Folder-per-zone inside a single project

`src/Njord/` gets `Configuration/`, `Domain/`, `Ingest/`, `Pipeline/` folders
(namespaces mirror folders). *Alternative considered:* one project per zone —
rejected as premature for a single deployable; the zone boundary is enforced by
convention and review, and can become project boundaries later without churn.

### D2: Plan presets as data, budget as the real currency

`NjordOptions` (bound from config section `Njord`) carries `Plan` (enum:
`Hobby`, `BusinessStarter`, `BusinessStandard`, `BusinessProfessional`,
`BusinessEnterprise`, `Custom`) and an optional `BudgetOverride`
(`RequestsPerMonth`, `RequestsPerMinute`). A static preset table maps plan →
budget; only Hobby's values (20,000/month, 60/min) are verified today, so the
other presets start with conservative placeholders and `Custom`/override is the
escape hatch. Everything downstream (throttle, validation) consumes the
*resolved budget*, never the plan name. *Alternative:* budget-only config
without plan names — rejected because plan names are the user-facing vocabulary
and presets give correct defaults for free.

Validation is `IValidateOptions<NjordOptions>` + `ValidateOnStart`: projected
monthly usage = locations × models × (cycles/month from poll interval); fail
startup above 80 % of the resolved monthly budget. Failing fast beats silently
burning quota mid-month.

### D3: Model ids are value-wrapped strings, parameters are a closed enum

- `WeatherModel` is a value record wrapping the raw API id (`ICON-D2`, …),
  validated non-empty — because the API takes a free string and plans may
  expose models we don't know about.
- `WeatherParameter` is a closed C# enum (temperature, precipitation,
  windSpeed, windGust, dewpoint, relativeHumidity, cloudCover, pressureMsl)
  with static metadata (unit) — the v1 parameter set is a deliberate product
  decision, and a closed set keeps mapping code exhaustive
  (switch-with-no-default).
- `ForecastPoint` is a record with one nullable property per parameter plus
  `ValidAt` (`DateTimeOffset`); `ForecastSeries` is an ordered list of points;
  `ModelForecast` = model + location + cycle id + series + `RetrievedAt`.
  *Alternative:* `Dictionary<WeatherParameter, double?>` per point — rejected:
  stringly-typed access, no compiler help when consensus arrives.

### D4: Typed client with typed outcomes, System.Text.Json source generation

`IKachelmannClient.FetchAsync(location, model, ct)` returns a `FetchOutcome`
union (records): `Success(ModelForecast)` or `Failure(FetchFailure)` with
reason `AuthFailed | RateLimited | ModelUnavailable | MalformedPayload |
Transport`. Expected failures never throw — the pipeline treats them as data.
Implementation: `IHttpClientFactory` typed client, `X-API-Key` header from
options, `units=mps` (hmm: verify exact units parameter values against a real
response — see Open Questions), DTOs deserialized via a source-generated
`JsonSerializerContext`. The API key never appears in logs or exception
messages.

### D5: No HTTP retries in v1

A failed request has already spent budget; automatic retries multiply spend
during outages exactly when they help least, and the next cycle (≤ 60 min away)
is a natural retry. Failures surface as `FetchOutcome.Failure` and cycle
diagnostics. *Alternative:* Polly retry with jitter — deferred until real-world
failure patterns justify it.

### D6: Pipeline = restartable tick source, one inner collection stream per cycle

*(Revised during implementation — the original GroupBy + TakeWithin sketch had a
flaw: a straggler outcome arriving after its cycle group closed, e.g. an HTTP
timeout longer than the aggregation window, would re-open the group and emit a
second CycleResult for the same cycle.)*

```
RestartSource.WithBackoff(min 5 s, max 5 min, rnd 0.2, () =>
  Source.Tick(initial, PollInterval)
    .Select(_ => CycleId.From(timeProvider))          // cycle id = tick timestamp
    .SelectAsync(parallelism: 1, cycle =>              // cycles never overlap
        Source.From(locations × models)
          .Throttle(budget.RequestsPerMinute, per 1 min)
          .SelectAsyncUnordered(4, client.FetchAsync)
          .TakeWithin(aggregationWindow)               // closes the cycle, keeps partials
          .RunWith(Sink.Seq)                           // → CycleResult
    )
).To(loggingSink)                                      // temporary sink
```

- **Why not `Zip`/`ZipN`:** a single missing model response deadlocks the join —
  banned by guardrail. `TakeWithin` closes every cycle after the aggregation
  window regardless of stragglers; `CycleResult` carries received forecasts,
  failures, *and* the unanswered target list.
- **Why not `GroupBy` on cycle id:** a straggler after group close re-opens the
  group → duplicate CycleResult for one cycle. The per-cycle inner stream makes
  exactly-one-result-per-cycle structural. `SelectAsync(1)` also means an
  overlong cycle backpressures the tick source (ticks are dropped, not queued) —
  budget-friendly degradation.
- The aggregation window defaults to fan-out duration at the throttle rate plus
  one HTTP timeout worth of slack; tests inject a short window.
- The whole graph is wrapped in `RestartSource.WithBackoff` so an unexpected
  stage failure never kills the service.
- The stream is materialized and owned by a small guardian actor
  (`PipelineGuardianActor`, registered via Akka.Hosting `WithActors`) — actors
  own lifecycle per guardrail, and this actor later becomes the natural
  coordination point when the MQTT egress change arrives.
- `TimeProvider` supplies tick timestamps for cycle ids; tests inject
  `FakeTimeProvider`.

### D7: Test seams

- Options validation: pure unit tests over the budget math.
- Client: `HttpMessageHandler` fake + hand-written fixture JSON (schema-derived,
  never captured with a real key committed to the repo).
- Pipeline: `IKachelmannClient` fake + a plain test `ActorSystem`/materializer
  with short tick intervals and an injected aggregation window; no real HTTP.
  (Akka.Hosting.TestKit was dropped — it targets xunit v2, this repo is
  xunit v3.)

## Risks / Trade-offs

- [SWISS1X1 may not be "Super HD", or may be plan-gated] → model list is pure
  config; a probe-request verification task runs before SWISS1X1 enters the
  default config; unknown-model responses map to `ModelUnavailable` and are
  logged loudly, never fatal.
- [Advanced-endpoint response field names are assumed from the OpenAPI spec,
  which is partially generic] → DTO parsing is isolated behind the client;
  first implementation task captures a real (sanitized) sample response with
  the user's key to lock the DTOs; an env-var-gated smoke test exists but is
  skipped when `Njord__ApiKey` is absent.
- [Free-string model ids mean typos surface only at runtime] → accepted;
  startup cannot validate against the API without spending requests. Mitigated
  by loud `ModelUnavailable` logging and later `models_used` diagnostics.
- [Fetch tasks abandoned by `TakeWithin` keep running briefly] → bounded by the
  HttpClient timeout (30 s); they only occupy fetch-parallelism slots of a cycle
  that is already closed, and cycles do not overlap.
- [Budget projection ignores restarts/manual runs] → the 20 % headroom under
  the guard is exactly that buffer.
- [Non-Hobby preset values are unverified placeholders] → documented as such;
  `Custom`/override is the supported path until verified.

## Migration Plan

Greenfield — no migration. Deploy as the existing Docker image pipeline builds;
rollback = previous image. The service starts, validates config, polls, and
logs cycle summaries; no external side effects yet (no MQTT).

## Open Questions

- Is `SWISS1X1` the "Super HD" model, and is it available on the Hobby plan?
  (Probe request task; blocks only the default model list, not the change.)
- Exact `units` parameter value for m/s wind (`mps` assumed from the units
  schema) and exact response field names — locked by the sample-capture task.
- Quorum semantics (how many models make a cycle "good") stay open until the
  consensus change; v1 only records received/missing.
