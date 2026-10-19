## Context

The pipeline currently processes `Forecast` through `StatefulSelectMany(computeAll)` with `ModelForecasts` state, computing Consensus → Alerts → Derived → Trends → Indices. The redesign processes `DomainDataReceived` through ProjectionGroup-based evaluation with `LocationForecast` state.

## Goals / Non-Goals

**Goals:** New pipeline on DomainDataReceived, ProjectionGroup evaluation, Weather alerts migrated.
**Non-Goals:** Adding non-Weather enrichments (Phase 4). Changing consumer attachment pattern.

## Decisions

### D1: MergeHub<DomainDataReceived> input

Each successful domain fetch produces a `DomainDataReceived(Location, DomainKind, IDomainForecast)`. The MergeHub collects these from all PollWorkers. The pipeline emits a `DataReceived` event for each, then runs enrichments.

### D2: LocationForecast as StatefulSelectMany state

State per location: `(LocationForecast Current, LocationForecast? Previous)`. On `DomainDataReceived`: update current's domain slot, then evaluate affected ProjectionGroups.

### D3: Reactive evaluation via AffectedBy

Each ProjectionGroup declares `RequiredDomains`. When Weather data arrives, only groups requiring Weather run. This avoids re-evaluating Flood enrichments on a Weather update.

### D4: Weather alerts as first ProjectionGroup

`WeatherAlertProjection` projects `DomainForecast<WeatherPoint>` from the LocationForecast. All 14 alert rules migrated to work on `DomainForecast<WeatherPoint>` instead of `LocationConsensus`. Confidence derived from Ensemble StdDev (low std = high confidence) instead of model agreement count.

### D5: New PipelineEvent types

`WeatherDataReceived`, `AirQualityDataReceived`, etc. (one per domain). `AlertRaised(Location, AlertType, Severity, Confidence, Details)` replaces `AlertComputed`.

## Risks / Trade-offs

- **[Big bang pipeline change]** → System doesn't run between this and the consumer update. Acceptable per user decision.
- **[Alert confidence model changes]** → StdDev-based confidence is statistically more meaningful than model-count agreement.
