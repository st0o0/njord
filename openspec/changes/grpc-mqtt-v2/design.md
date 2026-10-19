## Context

gRPC and MQTT schemas are tied to the old model (per-model forecasts, consensus, derived). They must be updated for multi-domain data and new enrichment events.

## Goals / Non-Goals

**Goals:** Updated gRPC proto for multi-domain, updated MQTT topics/payloads.
**Non-Goals:** Adding domain-specific enrichment detail — consumers forward what the pipeline produces.

## Decisions

### D1: gRPC proto reflects domain structure

New message types per domain. `GetForecast` returns domain-specific data. `StreamForecasts` becomes `StreamDomainData` with domain filtering. Old consensus/derived proto messages removed.

### D2: MQTT topics include domain

Topic scheme: `njord/{location}/{domain}/{parameter}` instead of `njord/{location}/{model}/{parameter}`. Enrichment topics: `njord/{location}/alerts/{alert_type}`.

### D3: GrpcConsumer adapts to new PipelineEvents

GrpcConsumer's internal hub and latest-value state adapt to the new event types. Each streaming client can filter by domain.

### D4: MQTT payloads simplified

No consensus payload, no derived payload. Domain data payloads (weather, air quality, etc.) and alert payloads.
