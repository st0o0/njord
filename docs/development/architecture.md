# Architecture

njord is one .NET 10 service built on [Akka.NET](https://getakka.net/) + Akka.Streams. It polls the [Open-Meteo APIs](https://open-meteo.com/) across six data domains, evaluates 89 feature rules on the data, and streams results to Home Assistant over gRPC.

The diagrams are interactive: zoom, click an element for details, and use the view browser (top left) to navigate to any view.

## System context

<likec4-view view-id="index"></likec4-view>

njord runs as one Docker container without an external database (SQLite by default). It fetches forecasts from Open-Meteo and serves them to consumers over gRPC on port 8081.

## Project architecture

The service is split into independent projects that map to the three architectural zones. Projects in different zones never reference each other; all data flows through the domain model.

<likec4-view view-id="zones"></likec4-view>

| Zone | Projects | Responsibility |
|------|----------|---------------|
| **Ingest** | `Njord.OpenMeteo` | `OpenMeteoGateway` routes each domain request to its typed client (`ForecastClient`, `EnsembleClient`, `AirQualityClient`, `FloodClient`, `MarineClient`, `SeasonalClient`) and rate-limits against the budget. Clients build requests and parse responses. |
| **Domain** | `Njord.Polling`, `Njord.Pipeline`, `Njord.Budget`, `Njord.Features` | `PollManager` and `PollWorker` (one per location) schedule fetches. `ForecastPipeline` with the `FeatureEngine` runs the 89 feature rules. `BudgetTracker` accounts for API usage. |
| **Consumers** | `Njord.Grpc` | `GrpcConsumer` maintains entity state and streams `EntityStateUpdate`s. It attaches to the pipeline via SourceRef. |

## Reference direction

```
Persistence <- Core <- Messages <- feature libraries <- Host
```

- **Core** is the foundation: vocabulary, domain logic, actor infrastructure
- **Messages** references Core and Akka.Streams (for `ISinkRef`/`ISourceRef`)
- Feature libraries (`OpenMeteo`, `Budget`, `Polling`, `Pipeline`, `Features`, `Grpc`) reference Core and Messages, never each other
- `Njord.Architecture.Tests` enforces this with ArchUnitNET

## Domains

Each poll cycle fetches the domains that the enabled features need. The defaults poll Forecast, Ensemble and Air Quality:

| Domain | Open-Meteo API | Content |
|--------|----------------|---------|
| **Forecast** | `api.open-meteo.com/v1/forecast` | High-resolution deterministic forecast using automatic best_match model blending (1-9 km). All weather variables including precipitation probability and UV index. |
| **Ensemble** | `ensemble-api.open-meteo.com/v1/ensemble` | Spread (standard deviation across ensemble members) for 15 variables for alert confidence. Forecasts of the configured ensemble models, compared passively by EnsembleModelComparison. |
| **Air Quality** | `air-quality-api.open-meteo.com` | PM2.5, PM10, ozone, nitrogen dioxide, pollen (birch, grass, alder, mugwort, ragweed, olive), European and US AQI sub-indices |
| **Flood** | `flood-api.open-meteo.com` | River discharge with mean, median, min, max, P25 and P75 |
| **Marine** | `marine-api.open-meteo.com` | Wave height, period, direction (wind waves and swell), sea surface temperature, sea level, ocean currents |
| **Seasonal** | `seasonal-api.open-meteo.com` | Monthly temperature and precipitation anomalies for the next 3-7 months, plus a weekly probabilistic tier (EFI, SOT, anomaly thresholds) |

Forecast and Ensemble are separate domains that poll independently. Features that need confidence (14 of the 22 weather alerts) declare Ensemble in their requirements; features that don't need it use a fixed Medium confidence.

## Streaming pipeline

<likec4-view view-id="pipeline-flow"></likec4-view>

1. **PollWorker** (one per location, started by `PollManager`) persists its learned update cycle and next poll time per domain. When due, it asks the gateway for each enabled domain (`FetchDomain`); fetches run in parallel.
2. **Gateway** routes each request to the matching domain client and throttles to stay within the request budget.
3. **Fetch** parses the response into a `DomainForecast<T>` and offers a `DomainDataReceived` into the pipeline's `MergeHub`.
4. **FeatureEngine** keeps a ring of recent `LocationForecast`s per location, replaces the changed domain slot and runs only the enabled feature rules that read that domain and whose required fields are present.
5. **Feature rules** (`IFeatureRule`, usually `FeatureRule<TInput>`) project the `LocationForecast` into a typed input and return typed `FeatureResult` events.
6. **BroadcastHub** emits typed `PipelineEvent`s to `GrpcConsumer`.

## Feature rules

A feature rule declares its required domains and fields in a `FeatureDescriptor` and runs reactively when one of them is updated. It is switched with `Njord:Features:<Name>:Enabled`.

| OutputKind | Count | Domains | Features |
|------------|-------|---------|----------|
| **Alert** | 30 | Forecast (+ Ensemble for confidence); AirQuality for Pollen, Pm25, Dust, PollenBlast, SmogRisk; Flood for FloodRisk, FloodRain; Marine for SwimmingConditions, CoastalFlood | Frost, Heat, Storm, HeavyRain, Uv, ClearSkyUv, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity, RainProbability, WetBulbDanger, Shower, CoastalFlood, CompoundHeatStress, MigraineRisk, FireRisk, Pollen, Pm25, Dust, SmogRisk, PollenBlast, FloodRisk, FloodRain, SwimmingConditions |
| **Score** | 7 | Forecast (+ AirQuality for AllergySeverity, AsthmaRisk) | Bbq, Laundry, Outdoor, SleepQuality, SprayWindow, AllergySeverity, AsthmaRisk |
| **Condition** | 23 | Forecast, AirQuality, Flood, Marine (varies per feature) | CommuteSafety, ExerciseAdvisory, SoilMoisture, Evapotranspiration, Gardening, VentilationWindow, NightVentilation, SurfConditions, OceanCurrents, SnowDepthWatch, PollenTrend, ElevatedWind, SolarYield, HeatPumpCop, FrostDepth, IrrigationNeed, FloodTimeline, InversionDetection, AtmosphericStability, CloudCeiling, GranularAqi, RagweedPollen, ApproachingFront |
| **Trend** | 1 | Ensemble | ConfidenceTrend |
| **Forecast** | 4 | Forecast, Ensemble, Seasonal (varies) | ForecastModelComparison, EnsembleModelComparison, GrowingDegree, SeasonalOutlook |
| **Weather** | 1 | Forecast | Weather (always-on weather entity with hourly forecast) |
| **Observation** | 23 | Forecast, AirQuality, Flood, Marine (varies) | DewPoint, Visibility, UvIndex, WindGusts, ShortwaveRadiation, SnowDepth, SoilMoisture, SoilTemperature, Evapotranspiration, Pm25, Pm10, Ozone, NitrogenDioxide, SulphurDioxide, CarbonMonoxide, EuropeanAqi, FloodDischarge, DischargeMean, WaveHeight, WavePeriod, WaveDirection, WaterTemperature, OceanCurrentVelocity |

89 feature rules across 7 output kinds. Every feature result reaches gRPC consumers as an `EntityStateUpdate` with a per-OutputKind value type (`AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `WeatherValue`, `ObservationValue`).

## gRPC service

A single `NjordService` (package `njord`, `Njord.Grpc.Proto`) exposes four RPCs:

| RPC | Request / Response | Purpose |
|-----|-------------------|---------|
| `GetManifest` | `ManifestRequest` / `ManifestResponse` | Entity definitions from `FeatureDescriptor.EntityMetadata` x locations (platform, device_class, icon, unit, output_kind) |
| `Subscribe` | `SubscribeRequest` / `stream EntityEvent` | Unified entity stream: snapshot on connect, then `EntityStateUpdate` (per-OutputKind value oneof), `EntityUnavailable`, `EntityAvailable` |
| `GetStatus` | `StatusRequest` / `StatusResponse` | Version, uptime, budget usage, per-location poll state |
| `TriggerPoll` | `TriggerPollRequest` / `TriggerPollResponse` | Manual poll trigger for a location |

## Three type worlds

| World | Project | Purpose |
|---|---|---|
| Messages | `Njord.Messages` | What actors send each other; in memory only |
| Persistence | `Njord.Persistence` | What goes into the journal; extend-only, a stored contract |
| Proto | `Njord.Grpc` | What gRPC clients see (`Njord.Grpc.Proto`) |

Messages <-> Persistence mapping in `<Owner>PersistenceMappingExtensions`, Messages -> Proto in `<Owner>MessageMappingExtensions` and per-OutputKind mappers in `Njord.Grpc.Mapping/`.

## Startup

`Program.cs` applies one Terp setup container per concern (`src/Njord/Configuration/`): `builder.Setups().Add<T>()` before `Build()`. `AkkaSetup : ActorSystemSetup` defines the actor system. Each library registers its actors via `services.AddActorRegistration<T>()`; `AkkaSetup.BuildSystem` calls `builder.ApplyRegistrations(serviceProvider)`.

## The architecture model

The diagrams come from a [LikeC4](https://likec4.dev/) model in `docs/likec4/`:

| File | Content |
|---|---|
| `specification.c4` | Element kinds (singleton, shard, child), tags, relationship kinds |
| `model.c4` | External systems, projects, actors, relationships |
| `views.c4` | System context, project overview, per-project detail views |
| `dynamic.c4` | Poll cycle, feature pipeline, gRPC subscribe flow |

When you add, remove or rename an actor, update `model.c4`, the [Actors](./actors) page and `AGENTS.md` in the same PR.
