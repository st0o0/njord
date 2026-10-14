## ADDED Requirements

### Requirement: A fetch that throws reports back as a failure
When `IOpenMeteoClient.FetchAsync` throws instead of returning a `PollOutcome`, the gateway SHALL treat it as a `PollOutcome.Failure` with reason `Transport` and a detail naming the exception type, log a Warning with the exception, and reply `FetchForecastFailed(location, modelId, Transport, detail)` to the remembered requester. The fetch stream SHALL keep running, so later fetches are processed normally, and nothing SHALL be offered to the forecast hub for the failed target.

#### Scenario: Throwing fetch is answered with FetchForecastFailed
- **WHEN** `FetchAsync` for (lucerne, broken) throws an `InvalidOperationException`
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, broken) with reason `Transport` and a detail containing `InvalidOperationException`

#### Scenario: The next fetch still works after a throwing fetch
- **WHEN** a fetch for (lucerne, broken) has thrown and the scheduler then sends `FetchForecast(lucerne, icon_d2)`
- **THEN** the requester receives `FetchForecastCompleted` for (lucerne, icon_d2)

### Requirement: A stopped fetch stream is recovered in place
If the gateway's fetch stream completes or fails while the gateway is running, the gateway SHALL log a Warning, answer every request that has not been answered yet with `FetchForecastFailed(location, modelId, Transport, detail)`, and re-materialize the fetch stream with the same budget gate. The forecast hub and any SourceRef handed out from it SHALL stay untouched. A completion signal from a fetch stream that was already replaced SHALL be ignored.

#### Scenario: In-flight requests are failed and fetching resumes
- **WHEN** the fetch stream stops while a request for (lucerne, icon_d2) is in flight
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, icon_d2) with reason `Transport`
- **AND** a subsequent `FetchForecast(lucerne, icon_d2)` is answered with `FetchForecastCompleted`

#### Scenario: Forecast stream survives the recovery
- **WHEN** the fetch stream is re-materialized
- **THEN** a forecast fetched afterwards still reaches a SourceRef obtained before the recovery
