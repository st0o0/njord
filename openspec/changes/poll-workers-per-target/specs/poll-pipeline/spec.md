## MODIFIED Requirements

### Requirement: A fetch that throws reports back as a failure
When `IOpenMeteoClient.FetchAsync` throws instead of returning a `PollOutcome`, the gateway SHALL treat it as a `PollOutcome.Failure` with reason `Transport` and a detail naming the exception type, log a Warning with the exception, and reply `FetchForecastFailed(location, modelId, Transport, detail)` to the remembered requester. The fetch stream SHALL keep running, so later fetches are processed normally, and nothing SHALL be sent to the pipeline input for the failed target.

#### Scenario: Throwing fetch is answered with FetchForecastFailed
- **WHEN** `FetchAsync` for (lucerne, broken) throws an `InvalidOperationException`
- **THEN** the requester receives `FetchForecastFailed` for (lucerne, broken) with reason `Transport` and a detail containing `InvalidOperationException`

#### Scenario: The next fetch still works after a throwing fetch
- **WHEN** a fetch for (lucerne, broken) has thrown and a requester then sends `FetchForecast(lucerne, icon_d2)`
- **THEN** the requester receives `FetchForecastCompleted` for (lucerne, icon_d2)
