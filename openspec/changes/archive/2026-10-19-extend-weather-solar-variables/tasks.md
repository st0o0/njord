## 1. Extend Ensemble request

- [ ] 1.1 Add solar variable names to the Ensemble request URL builder
- [ ] 1.2 Test that the URL includes all 5 solar variables

## 2. Extend WeatherPoint and mapping

- [ ] 2.1 Verify/add solar fields on WeatherPoint (ShortwaveRadiation, DirectRadiation, DiffuseRadiation, DirectNormalIrradiance, SunshineDuration)
- [ ] 2.2 Update OpenMeteoEnsembleMappings to parse solar columns
- [ ] 2.3 Tests for solar field mapping

## 3. Validation

- [ ] 3.1 dotnet build Njord.slnx
- [ ] 3.2 All affected test projects pass
