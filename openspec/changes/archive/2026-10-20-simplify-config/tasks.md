## 1. WeatherOptions

- [x] 1.1 Create `WeatherOptions` in `src/Njord.Core/Configuration/` with `IncludeSolar` (bool, default false) and `ForecastDays` (int, default 4)
- [x] 1.2 Create `WeatherOptionsValidator` (ForecastDays > 0 and <= 16)
- [x] 1.3 Register in a setup container
- [x] 1.4 Tests for defaults and validation

## 2. Internalize weather variables

- [x] 2.1 WeatherClient builds its variable list internally (standard weather vars + solar if IncludeSolar)
- [x] 2.2 Expose `IReadOnlyList<string> HourlyVariables` on IWeatherClient
- [x] 2.3 WeatherClient reads `IOptions<WeatherOptions>` for IncludeSolar and ForecastDays
- [x] 2.4 Remove dependency on ResolvedParameterSet from WeatherClient
- [x] 2.5 Tests for URL building with/without solar

## 3. Remove ParameterOptions + ParameterRegistry

- [x] 3.1 Remove `ParameterOptions`, `ParameterSelection`, `ResolvedParameterSet` from Core
- [x] 3.2 Remove `ParameterDef`, `ParameterRegistry` from Core (moved there earlier from Model)
- [x] 3.3 Remove parameter-related validation from NjordOptionsValidator
- [x] 3.4 Remove `NjordOptions.Parameters` property
- [x] 3.5 Remove or update all tests referencing these types
- [x] 3.6 Update MQTT to read variable names from WeatherClient instead of ResolvedParameterSet

## 4. Remove Models + Horizons

- [x] 4.1 Remove `NjordOptions.Models` and `NjordOptions.Horizons` properties
- [x] 4.2 Remove `LocationOptions.Models` property
- [x] 4.3 Remove model/horizon validation from NjordOptionsValidator
- [x] 4.4 Simplify ServiceInfoMappings: no model resolution, catalog shows "Ensemble Mean"
- [x] 4.5 Simplify or remove `CatalogModel` record
- [x] 4.6 Update gRPC GetCatalog for simplified catalog
- [x] 4.7 Remove `ForecastDays` from NjordOptions (now in WeatherOptions)

## 5. Merge StorageOptions

- [x] 5.1 Add `DataPath` to PersistenceOptions
- [x] 5.2 Update all consumers of StorageOptions.PersistencePath to use PersistenceOptions.DataPath
- [x] 5.3 Delete StorageOptions class and validator
- [x] 5.4 Update tests

## 6. Budget update

- [x] 6.1 BudgetCalculator takes weather variable count (from WeatherClient) instead of ParameterOptions weight
- [x] 6.2 Update budget projection formula
- [x] 6.3 Tests for budget with new inputs

## 7. Config Builder + Docs

- [x] 7.1 Update Config Builder: remove Models/Horizons/Parameters, add Weather.IncludeSolar
- [x] 7.2 Update docker-compose.example.yml: minimal config
- [x] 7.3 Update docs/configuration/ for new config shape
- [x] 7.4 Update AGENTS.md

## 8. Validation

- [x] 8.1 `dotnet build Njord.slnx`
- [x] 8.2 Full test suite passes
- [x] 8.3 Minimal config starts successfully (location names + MQTT host only)
