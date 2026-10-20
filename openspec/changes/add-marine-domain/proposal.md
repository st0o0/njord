## Why

Marine weather data (waves, currents, water temperature) adds value for coastal Home Assistant users — swimming safety, sailing conditions, storm surge risk. Open-Meteo provides a Marine Weather API at `marine-api.open-meteo.com` with hourly forecasts up to 8 days. The domain-handler architecture supports this with zero framework changes.

## What Changes

- Add `IMarineClient` + `MarineClient` typed client in Njord.OpenMeteo.
- Add `MarineResponse` typed response model with `MarineHourlyData`.
- Add `MarineResponseMappings` (response → `DomainForecast<MarinePoint>`).
- Add `DomainKind.Marine` route in OpenMeteoGateway.
- Add `DomainKind.Marine` to default `EnabledDomains` (or opt-in).
- Create `Njord.Marine` project with `SwimmingConditionsHandler` enrichment.
- Create `Njord.Marine.Tests` project.
- Update Njord.slnx, host references, architecture tests.
- API budget: +1 call per cycle per coastal location.

## Non-goals

- Tide predictions (not available from Open-Meteo).
- Sailing/surfing scores (future enrichment).

## Capabilities

### New Capabilities

- `marine-fetch`: Typed MarineClient for marine-api.open-meteo.com.
- `marine-enrichment`: SwimmingConditionsHandler computing safe/moderate/dangerous.

### Modified Capabilities

- `multi-domain-polling`: PollWorker enables Marine domain.
- `open-meteo-multi-endpoint`: Gateway routes Marine.

## Impact

- **Njord.OpenMeteo**: New client, response model, mapping.
- **Njord.Marine**: New project with handler.
- **Njord.Pipeline**: PollWorker enabled domains extended.
- **API budget**: +1 call/cycle for coastal locations.
