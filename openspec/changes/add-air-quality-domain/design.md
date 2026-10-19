## Context

Adding Air Quality as a new domain to the multi-domain proxy. Follows the established pattern: client method → mapping → gateway route → PollWorker enable → enrichments.

## Goals / Non-Goals

**Goals:** AQ fetch, parse, map, gateway route, pollen and PM2.5 alerts.
**Non-Goals:** Historical AQ data, custom model selection.

## Decisions

### D1: Separate endpoint, same client

FetchAirQualityAsync on IOpenMeteoClient, targeting `air-quality-api.open-meteo.com`. Same HTTP + JSON pattern as Ensemble.

### D2: AQ alerts as separate ProjectionGroup

AirQualityAlertProjector requires `DomainKind.AirQuality`. Two rules: PollenAlert (threshold on any pollen type), Pm25Alert (WHO guideline 15 µg/m³).

### D3: AirQualityPoint extended

Add `EuropeanAqi` and `NitrogenDioxide` to the existing AirQualityPoint record if not already present.
