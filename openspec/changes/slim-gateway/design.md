## Context

Gateway has 7 private record types for domain-specific fetch completion. Each domain fetch follows identical structure: call client → pipe result → map → offer to pipeline.

## Goals / Non-Goals

**Goals:** Generic routing, no per-domain boilerplate. Config-driven domain selection.
**Non-Goals:** New domains.

## Decisions

### D1: One DomainFetchResult for all domains
```csharp
private sealed record DomainFetchDone(DomainKind Domain, IDomainForecast Forecast);
private sealed record DomainFetchError(DomainKind Domain, Exception Cause);
```
Gateway handler switches on `DomainKind` only for the initial client call, result handling is uniform.

### D2: Client helper for shared HTTP pattern
```csharp
private async Task<OpenMeteoTimeSeries> FetchAndParseAsync(string url, CancellationToken ct)
```
Each public method builds the URL, calls the helper, then maps domain-specifically.

### D3: EnabledDomains as options
`NjordOptions.EnabledDomains` — `IReadOnlyList<DomainKind>`, default `[Weather, AirQuality, Flood]`. PollWorker reads at construction.
