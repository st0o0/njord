## 1. Core utility

- [x] 1.1 Add ForecastQueries.Upcoming helper to Njord.Core
- [x] 1.2 Add Scoring helper to Njord.Weather

## 2. Weather-domain handlers

- [x] 2.1 Implement VentilationWindowHandler (Weather × AirQuality)
- [x] 2.2 Implement ExerciseAdvisoryHandler (Weather × AirQuality)
- [x] 2.3 Implement LaundryScoreHandler (Weather)
- [x] 2.4 Implement CommuteSafetyHandler (Weather)
- [x] 2.5 Implement NightVentilationHandler (Weather)
- [x] 2.6 Implement ConfidenceTrendHandler (Weather)

## 3. AirQuality-domain handler

- [x] 3.1 Implement PollenTrendHandler (AirQuality)

## 4. PipelineEvent types

- [x] 4.1 Add 7 new PipelineEvent record types to Njord.Messages

## 5. DI registration

- [x] 5.1 Register weather lifestyle handlers in AddWeatherDomain
- [x] 5.2 Register PollenTrendHandler in AddAirQualityDomain

## 6. Tests

- [x] 6.1 VentilationWindowHandlerSpec (3 facts)
- [x] 6.2 ExerciseAdvisoryHandlerSpec (3 facts)
- [x] 6.3 LaundryScoreHandlerSpec (2 facts)
- [x] 6.4 CommuteSafetyHandlerSpec (3 facts)
- [x] 6.5 NightVentilationHandlerSpec (2 facts)
- [x] 6.6 ConfidenceTrendHandlerSpec (2 facts)
- [x] 6.7 PollenTrendHandlerSpec (3 facts)

## 7. Validation

- [x] 7.1 dotnet build Njord.slnx
- [x] 7.2 All affected test projects pass
