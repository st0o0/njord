## Context

Today `OpenMeteoGateway.OnFetchDomain` does: location lookup, domain whitelist, budget acquire with a Weather special case, `FetchAsync` (telemetry, error mapping), a six-arm `FetchForecastAsync` switch with per-domain mapping, trimming and forecast-day clamping, then pipeline offer and reply. Clients return raw DTOs or null (HTTP 400).

## Decisions

### 1. Contract: IDomainClient and FetchResult
```csharp
public interface IDomainClient
{
    DomainKind Domain { get; }
    int CallWeight { get; }
    Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken ct);
}
public abstract record FetchResult;
public sealed record FetchSuccess(IDomainForecast Forecast) : FetchResult;
public sealed record FetchEmpty : FetchResult;
public sealed record FetchError(PollFailureReason Reason, string Message) : FetchResult;
```
Lives in `Njord.Core/Domain`. `CallWeight` is an addition to the requested shape so the gateway stays domain-agnostic (Weather returns the `ceil(variables / 10)` weight, the others 1). `FetchEmpty` maps to `FetchDomainFailed(MalformedPayload)`, `FetchError` to its reason. Rejected: throwing from clients (the gateway would classify exceptions again).

### 2. Thick clients
Each client owns the URL, the call, mapping (existing `*ResponseMappings`), trimming (existing `ForecastTrimming`), error handling (`HttpRequestException`, `TaskCanceledException`, `JsonException` become `FetchError`; HTTP 400 becomes `FetchEmpty`; cancellation by the caller is not swallowed). Fetch telemetry (`FetchTotal`, `FetchDuration`, same tags) is recorded by the gateway around `client.FetchAsync`, because only the gateway knows the location name; outcome is `success` for `FetchSuccess` and `failure` for `FetchEmpty`, `FetchError` and exceptions, duration only on success.

Inputs come through DI: `IOptions<XOptions>` (forecast days, clamped to the API maximum inside the client) and the resolved variable names from `ResolvedRequirements` (`ToApiNames()`), computed once in the constructor. The gateway no longer sees options or variables.

### 3. URLs without a builder
`QueryHelpers.AddQueryString(endpoint, pairs)` with invariant-culture coordinates; `timeformat=unixtime` and `timezone=UTC` are always set. The helper escapes values but leaves the comma of a list unescaped, so lists are sent as before; the URL specs pin it. No builder type survives.

### 4. Thin gateway
Constructor target: `clients`, `gate`, `timeouts`. Open point, to be settled in the architecture discussion before implementation: the gateway also needs (a) location resolution by name (`IOptions<NjordOptions>` today), (b) `TimeProvider`, (c) the budget gate, which is built inside the actor from `IBudgetProvider` and `Context.GetActor<IBudgetTracker>()`. (a) and (b) cannot simply be dropped; options are to keep them (5 dependencies) or to have `FetchDomain` carry resolved coordinates and pass `now` into the clients from the pipeline side. Default if undecided: keep `NjordOptions` and `TimeProvider`.

**Decided (2026-10-20):** keep `IOptions<NjordOptions>` (location lookup) and `TimeProvider` as gateway dependencies; the budget gate stays built inside the actor from `IBudgetProvider` and the `IBudgetTracker` marker. `FetchDomain` is unchanged. `QueryHelpers` keeps list commas raw (pinned by `ClientRequestSpec`). `Njord.OpenMeteo` gains `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for `QueryHelpers`, like Budget, Polling, Pipeline and Grpc.

Flow: find client (missing -> Rejected) -> `gate.TryAcquire(client.CallWeight)` -> `client.FetchAsync` piped to self -> pipeline offer -> reply. Pending map, `PipelineResolved`/SinkRef attachment and the id scheme stay.

### 5. DI
**Decided:** each client is registered with `AddHttpClient<TClient>` (handler chain with `HttpMetricsHandler` and resilience stays) and exposed as transient `IDomainClient`. A singleton `IReadOnlyDictionary<DomainKind, IDomainClient>` is built once from the registered clients and handed to the gateway. The gateway is a long-lived singleton actor under backoff supervision, so it holds its typed `HttpClient` instances for its lifetime; handler rotation still applies through `IHttpClientFactory` on restart.

### 6. Tests
Gateway specs shrink to orchestration with a fake `IDomainClient` (success, empty, error, budget reject, pipeline unavailable). Per-domain mapping, trimming, error classification and URL tests move to client specs against a stub `HttpMessageHandler`. Existing mapping specs stay. TDD: pin current behavior (error text, telemetry tags) before moving code.

## Risks

- Behavior drift in error text and telemetry tags; mitigated by pinning specs.
- DI lifetime of clients (see 5).
- Sequencing: `multi-model-weather` builds on the thick `WeatherClient`; this change lands first.
