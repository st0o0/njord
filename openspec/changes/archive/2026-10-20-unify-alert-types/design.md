## Context

The v2 redesign added AQ and Flood alerts alongside the existing weather alerts, each mimicking but diverging from the weather alert pattern. This created three parallel type systems.

## Goals / Non-Goals

**Goals:** One alert type system, one pipeline event, one consumer path.
**Non-Goals:** Changing alert computation logic or thresholds.

## Decisions

### D1: AlertType enum includes all domains
```
Frost, Ice, Heat, HeavyRain, Snow, Storm, Thunderstorm, Fog,
Humidity, PressureDrop, TropicalNight, UV, Visibility, WindChill,
Pollen, Pm25, FloodRisk, FloodRain
```
Proto enum values already exist (15-18); mapping becomes direct.

### D2: Alert record is domain-agnostic
`Alert(AlertType Type, AlertSeverity Severity, double Confidence, AlertDetails Details)` — same for all domains. Details carries peak value, hours until, duration as before.

### D3: IAlertRule unified interface
All rules implement `IAlertRule { AlertType Type; Alert? Evaluate(input, settings, now); }`. Weather rules take `DomainForecast<WeatherPoint>`, AQ rules take `DomainForecast<AirQualityPoint>`, Flood rules take `DomainForecast<FloodPoint>` — the projector handles the type narrowing, the rule interface is the same.

### D4: One AlertRaised event
Delete `AirQualityAlertRaised` and `FloodAlertRaised`. All alerts flow as `AlertRaised(Location, Alert, DateTimeOffset)`.
