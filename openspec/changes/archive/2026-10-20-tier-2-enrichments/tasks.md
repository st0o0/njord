## 1. EnrichmentOptions extensions

- [x] 1.1 Add 7 bool properties to EnrichmentOptions: SolarYield, GardeningAdvisory, EnergyDemand, OutdoorScore, BBQScore, FloodTimeline, ApproachingFront (all default false)
- [x] 1.2 Add SolarYieldOptions (PeakKw, Azimuth, Tilt) and EnergyDemandOptions (HeatingSetpoint, CoolingSetpoint) with validators
- [x] 1.3 Register new options classes

## 2. PipelineEvent types

- [x] 2.1 Add 7 new PipelineEvent records: SolarYieldComputed, GardeningAdvised, EnergyDemandComputed, OutdoorScoreComputed, BBQScoreComputed, FloodTimelineComputed, FrontApproaching

## 3. Weather handlers

- [x] 3.1 SolarYieldHandler in Njord.Weather (reads SolarYieldOptions, computes from radiation data)
- [x] 3.2 GardeningHandler in Njord.Weather (frost check, rain check, temp check)
- [x] 3.3 EnergyDemandHandler in Njord.Weather (reads EnergyDemandOptions, degree-hour computation)
- [x] 3.4 OutdoorScoreHandler in Njord.Weather (Weather × AQ composite, Triggers = {Weather, AirQuality})
- [x] 3.5 BBQScoreHandler in Njord.Weather (temp/wind/rain/cloud scoring)
- [x] 3.6 ApproachingFrontHandler in Njord.Weather (pressure trend, wind shift, needs previous)

## 4. Flood handler

- [x] 4.1 FloodTimelineHandler in Njord.Flood (peak detection, duration above p75, recession slope)

## 5. DI registration

- [x] 5.1 Register all 7 handlers as IDomainHandler in their domain's AddXxxDomain() extension
- [x] 5.2 Verify ConfigKey matches EnrichmentOptions property name

## 6. Tests

- [x] 6.1 2-3 facts per handler (threshold trigger + edge case)
- [x] 6.2 ~20 new tests total

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx`
- [x] 7.2 All test projects pass
