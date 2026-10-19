## REMOVED Requirements

### Requirement: DerivedAnalyzer computes derived weather values
**Reason**: All derived values (apparent temperature, dew point, VPD, UV index, WMO weather codes) are available natively from the Open-Meteo API. The DerivedAnalyzer duplicated API-native computations.
**Migration**: Derived values are included in the API response and passed through as part of WeatherPoint fields.
