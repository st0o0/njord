## Why

The flat `EnabledDomains` list forces users to think in API endpoints instead of features. A user who wants pollen alerts shouldn't need to know that requires the AirQuality domain. Worse, enabling a domain without any enrichment using it wastes API budget. Enrichment-driven config inverts the model: users toggle features, njord derives which domains to fetch.

## What Changes

- **BREAKING**: Remove `NjordOptions.EnabledDomains` — replaced by enrichment toggles.
- New `EnrichmentOptions` class (`Njord:Enrichments` section) with one bool per enrichment handler.
- Defaults on: `WeatherAlerts`, `CommuteSafety`, `PollenAlert`, `Pm25Alert` (universally useful, costs Weather + AQ = 2 calls).
- `IDomainHandler` gains a `string ConfigKey` property matching the options property name.
- Domain resolution at startup: `requiredDomains = union of enabled handlers' Triggers`. Only these domains are fetched.
- `PollWorker` reads required domains from the resolved set, not a config property.
- `BudgetCalculator` uses `requiredDomains.Count` for budget projection.
- Pipeline filters handlers at materialization: only enabled handlers run.
- Config Builder updated to show enrichment toggles instead of domain checkboxes.

## Non-goals

- Adding new enrichments — this is infrastructure only.
- Changing enrichment logic — handlers stay as-is, just gain ConfigKey.
- API budget impact: potentially reduced (fewer domains if fewer enrichments enabled).

## Capabilities

### New Capabilities

- `enrichment-config`: EnrichmentOptions, domain resolution from enabled handlers, budget integration.

### Modified Capabilities

- `multi-domain-polling`: PollWorker reads domains from resolved set, not EnabledDomains config.
- `projection-group-pipeline`: Pipeline filters handlers by enrichment config.

## Impact

- **Njord.Core**: New EnrichmentOptions + validator, domain resolution logic.
- **Njord.Core/Domain**: IDomainHandler gains ConfigKey.
- **All domain projects**: Each handler adds ConfigKey property.
- **Njord.Pipeline**: PollWorker uses resolved domains. Pipeline filters handlers.
- **Njord (host)**: Registration resolves domains at startup.
- **Config Builder**: Enrichment toggles replace domain checkboxes.
- **docker-compose.example.yml**: `Njord__Enrichments__*` env vars.
