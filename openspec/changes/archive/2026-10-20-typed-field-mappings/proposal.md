## Why

The Weather, AirQuality and Marine response mappers each repeat the same loop (guard, `lastValued` tracking, `FromUnixTimeSeconds`, trailing trim), and Weather and AirQuality read the DTO through a 600-character positional deconstruction. `OpenMeteoRequestBuilder` concatenates raw keys and values and `Build()` mutates its parameter list.

## What Changes

- Extract `ForecastBuilding.BuildForecast<TPoint>`: the shared loop and trailing trim; each mapper supplies a `hasValue` and a `create` lambda.
- Weather and AirQuality read the DTO by property access (`response.Hourly is not { } h`), like Marine.
- `OpenMeteoRequestBuilder` encodes `With()` keys and values and list entries with `Uri.EscapeDataString` (commas stay literal); `Build()` no longer mutates state and is idempotent.

## Decisions

A typed field table per domain (FieldDefinition, generic mapper, completeness specs) was designed and rejected as an abstraction layer too close to AutoMapper. Mappers keep explicit field assignments; the `Has()` and `ToApiName()` switches stay. Flood and Seasonal mappers are unchanged.

## Capabilities

- `request-url-encoding`: Open-Meteo request URLs encode keys and values.

## API-budget impact

None. Requested variables, call weights, cadence and URLs are unchanged.
