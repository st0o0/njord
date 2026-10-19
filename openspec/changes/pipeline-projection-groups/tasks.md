## 1. Pipeline input change

- [x] 1.1 Define `DomainDataReceived` message type
- [x] 1.2 Change `MergeHub<Forecast>` to `MergeHub<DomainDataReceived>` in ForecastPipeline
- [x] 1.3 Update SinkRef handshake for new input type
- [x] 1.4 Tests for new MergeHub input

## 2. LocationForecast state in StatefulSelectMany

- [x] 2.1 Replace `ModelForecasts` state with `(LocationForecast Current, LocationForecast? Previous)` per location
- [x] 2.2 On DomainDataReceived: update current domain slot, shift previous
- [x] 2.3 Emit DataReceived event for each domain update
- [x] 2.4 Tests for state management (update, previous tracking)

## 3. ProjectionGroup evaluation engine

- [x] 3.1 Implement ProjectionGroup evaluation loop in StatefulSelectMany
- [x] 3.2 Implement AffectedBy(DomainKind) filtering
- [x] 3.3 For each affected group: Project → if non-null → Evaluate all enrichments
- [x] 3.4 Tests for group routing, null projection skipping, reactive filtering

## 4. Weather Alert ProjectionGroup

- [x] 4.1 Create `WeatherAlertInput` record type
- [x] 4.2 Create `WeatherAlertProjector` implementing IProjector<WeatherAlertInput>
- [x] 4.3 Migrate all 14 alert rules to work on `DomainForecast<WeatherPoint>` instead of `LocationConsensus`
- [x] 4.4 Change confidence model: Ensemble StdDev → confidence level
- [x] 4.5 Register WeatherAlertProjectionGroup
- [x] 4.6 Tests for each migrated alert rule

## 5. New PipelineEvent types

- [x] 5.1 Define domain-received events (WeatherDataReceived, AirQualityDataReceived, etc.)
- [x] 5.2 Define AlertRaised event (replaces AlertComputed)
- [x] 5.3 Update BroadcastHub typing
- [x] 5.4 Tests for new event types

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx`
- [x] 6.2 Pipeline tests pass with new architecture
- [x] 6.3 Alert tests pass with DomainForecast input
