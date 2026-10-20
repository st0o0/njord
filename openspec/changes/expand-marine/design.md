## Context

`MarineParams` has 4 flags (`WaveHeight`, `WavePeriod`, `WaveDirection`, `WaterTemperature`); `MarineHourlyData` already carries currents that are not mapped to a point. `SwimmingConditionsHandler` requires `WaveHeight` and optionally `WaterTemperature`. Handlers receive the whole `LocationForecast`, so a Marine handler can read Weather wind from the same object without the domain projects referencing each other.

## Goals / Non-Goals

**Goals:** expose wind-wave and swell components and sea level, add a surf/sail quality enrichment.

**Non-Goals:** tide prediction, historical data, spot models.

## Decisions

### D1: Additive point shape

New nullable members next to the existing ones; the existing three keep meaning "combined sea state" (the API's `wave_*` variables). One flag, one `HasFields` arm, one DTO property, one API-name arm and one value-mapping arm per variable: `wind_wave_height|period|direction|peak_period`, `swell_wave_height|period|direction|peak_period`, `sea_level_height_msl`. Switches throw `ArgumentOutOfRangeException` by default and are pinned per member by specs. The flag enum reaches 13 members.

### D2: Request only what is enabled

New flags are optional requirements of `SurfConditions`. With the toggle off nothing is requested beyond today, so default payloads and parsing cost do not change.

### D3: SurfConditionsHandler scoring

A pure `SurfConditionsRule` (testable without actors) scores the next window of hours:

- swell period: below 8 s poor, 8 to 12 s good, above 12 s excellent (thresholds as constants pinned by spec, tuned in task 3.1);
- swell height: usable band with a lower and an upper bound; too small or too large lowers the score;
- wind relative to swell: with Weather wind direction available, wind within a tolerance of the opposite of swell direction is offshore (bonus), roughly aligned is onshore (penalty), otherwise cross; without wind data this factor is omitted and the result is marked as swell-only;
- wind-wave share: a high wind-wave to swell ratio indicates choppy conditions (penalty).

Output is a lifestyle event (`LifestyleKind.SurfConditions`) with score, rating label and key values in the string map, so it travels over the existing `ToLifestyleUpdate()` wire to gRPC and MQTT with no new proto message. The kind needs an explicit wire name in the `LifestyleKind` switch (`surf_conditions`) pinned by a golden master.

Weather wind direction is optional: `Requirements` lists it as optional Weather input, which can pull the Weather domain into the fetch set; this is stated in the enrichment docs and the Config Builder triggers, and budget impact is checked in the spec (the Weather call is usually already required by `WeatherAlerts`).

### D4: SwimmingConditionsHandler unchanged

Swimming cares about total sea state near shore; combined `wave_height` is the right input. Splitting it would change alert behavior for no benefit. Revisit only if swell-dominated conditions prove misleading for swimming (follow-up).

### D5: Sea level is raw

`SeaLevelHeight` is exposed as a series on the point (meters above mean sea level, includes tides per the API; coverage and model resolution verified in task 1.1). No tide extremes or prediction in this change.

## Risks / Trade-offs

- Coastal-only data: inland locations return null for all marine variables -> existing behavior for Marine, unchanged.
- Surf scoring is a heuristic -> documented as indicative, thresholds are constants not options, to keep config small.
- Peak period semantics (`*_peak_period`) differ from mean period -> task 1.1 verifies and the rule uses mean period for classification, peak period is exposed only.
