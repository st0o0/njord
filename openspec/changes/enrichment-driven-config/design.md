## Context

njord currently uses `NjordOptions.EnabledDomains` (a list of DomainKind) to decide which API endpoints to poll. This forces users to think in API terms. The enrichment-driven approach inverts this: users toggle features, the system derives domains.

## Goals / Non-Goals

**Goals:** Replace EnabledDomains with enrichment toggles, derive domains automatically, integrate with budget.
**Non-Goals:** New enrichments, changing handler logic, UI changes beyond config.

## Decisions

### D1: EnrichmentOptions as flat bool properties

One sealed class with `bool WeatherAlerts { get; init; } = true;` etc. Simple, discoverable, bindable to `Njord:Enrichments:WeatherAlerts=true` env vars. No dictionary, no dynamic keys.

`IsEnabled(string key)` uses a compiled dictionary built in the static constructor from reflection — called once at startup, not per event.

### D2: Domain resolution as a startup-computed IReadOnlySet<DomainKind>

Computed once at startup in a setup container:
```
var enabledKeys = enrichmentOptions.EnabledKeys();
var handlers = allHandlers.Where(h => enabledKeys.Contains(h.ConfigKey));
var domains = handlers.SelectMany(h => h.Triggers).ToHashSet();
```

Registered as `IReadOnlySet<DomainKind>` singleton. PollWorker, BudgetCalculator, and the pipeline all consume it.

### D3: Remove EnabledDomains from NjordOptions

`NjordOptions.EnabledDomains` is deleted. The validator no longer checks it. The domain set is derived, not configured.

### D4: ConfigKey is a string constant on each handler

Each handler declares `public string ConfigKey => "WeatherAlerts";`. The key must match the property name on EnrichmentOptions exactly. An architecture test verifies every registered handler has a valid key.

## Risks / Trade-offs

- **[Breaking config change]** → `Njord__EnabledDomains__*` env vars stop working. Migration: replace with `Njord__Enrichments__*` toggles.
- **[Reflection in IsEnabled]** → One-time at startup via cached dictionary. Zero runtime cost.
