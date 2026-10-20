## 1. Add resilience package

- [ ] 1.1 Add Microsoft.Extensions.Http.Resilience to Njord.OpenMeteo.csproj

## 2. Configure resilience in DI

- [ ] 2.1 Add .AddStandardResilienceHandler() to each HttpClient registration in OpenMeteoSetupContainer

## 3. Simplify clients

- [ ] 3.1 Delete FetchCoreAsync from WeatherClient
- [ ] 3.2 Delete LogBadRequestAsync
- [ ] 3.3 Simplify WeatherClient.FetchAsync to GetFromJsonAsync + catch 400
- [ ] 3.4 Simplify AirQualityClient.FetchAsync (remove WeatherClient.FetchCoreAsync call)
- [ ] 3.5 Simplify FloodClient.FetchAsync (remove WeatherClient.FetchCoreAsync call)
- [ ] 3.6 Simplify MarineClient.FetchAsync if it exists
- [ ] 3.7 Simplify GeocodingClient if it uses FetchCoreAsync

## 4. Update Gateway

- [ ] 4.1 Gateway handles exceptions from failed fetches (non-transient after retry exhaustion)

## 5. Tests

- [ ] 5.1 Verify URL building still works
- [ ] 5.2 Verify null return on 400

## 6. Validation

- [ ] 6.1 dotnet build Njord.slnx
- [ ] 6.2 All affected test projects pass
