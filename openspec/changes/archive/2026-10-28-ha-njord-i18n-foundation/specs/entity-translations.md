# Entity Translations Spec

## Behavior

- Every user-facing entity displays its name from the HA translation system, not a hardcoded string
- The display name changes with the HA installation language (EN default, DE supported)
- Entity IDs remain stable (based on unique_id, not display name)
- Enum sensor states (CommuteSafety, Exercise) display translated labels in the UI
- Score-type lifestyle sensors expose `best_start` (ISO timestamp) and `best_hours` (int) as extra state attributes when available

## Entity Translation Keys

### Alert Binary Sensors (20)

| AlertType value | translation_key | EN name | DE name |
|---|---|---|---|
| frost | frost_alert | Frost Alert | Frostwarnung |
| heat | heat_alert | Heat Alert | Hitzewarnung |
| storm | storm_alert | Storm Alert | Sturmwarnung |
| heavy_rain | heavy_rain_alert | Heavy Rain Alert | Starkregenwarnung |
| uv | uv_alert | UV Alert | UV-Warnung |
| fog | fog_alert | Fog Alert | Nebelwarnung |
| snow | snow_alert | Snow Alert | Schneewarnung |
| pressure_drop | pressure_drop_alert | Pressure Drop Alert | Druckabfallwarnung |
| thunderstorm | thunderstorm_alert | Thunderstorm Alert | Gewitterwarnung |
| ice | ice_alert | Ice Alert | Glättewarnung |
| wind_chill | wind_chill_alert | Wind Chill Alert | Windkühlwarnung |
| visibility | visibility_alert | Visibility Alert | Sichtwarnung |
| tropical_night | tropical_night_alert | Tropical Night Alert | Tropennachtwarnung |
| humidity | humidity_alert | Humidity Alert | Schwülewarnung |
| pollen | pollen_alert | Pollen Alert | Pollenwarnung |
| pm25 | pm25_alert | PM2.5 Alert | PM2.5-Warnung |
| dust | dust_alert | Dust Alert | Staubwarnung |
| flood_risk | flood_risk_alert | Flood Risk Alert | Hochwasserwarnung |
| flood_rain | flood_rain_alert | Flood Rain Alert | Hochwasser-Regenwarnung |
| swimming | swimming_alert | Swimming Conditions Alert | Schwimmwarnung |
| wet_bulb | wet_bulb_alert | Wet Bulb Danger | Feuchtkugelwarnung |
| shower | shower_alert | Shower Alert | Schauerwarnung |
| rain_probability | rain_probability_alert | Rain Probability Alert | Regenwahrscheinlichkeitswarnung |
| smog_risk | smog_risk_alert | Smog Risk Alert | Smogwarnung |
| coastal_flood | coastal_flood_alert | Coastal Flood Alert | Küstenhochwasserwarnung |
| clear_sky_uv | clear_sky_uv_alert | Clear Sky UV Alert | UV-Warnung (wolkenfrei) |
| fire_risk | fire_risk_alert | Fire Risk Alert | Waldbrandwarnung |

### Domain Sensors (12)

| key | translation_key | EN name | DE name |
|---|---|---|---|
| pm25 | pm25 | PM2.5 | PM2.5 |
| pm10 | pm10 | PM10 | PM10 |
| ozone | ozone | Ozone | Ozon |
| birch_pollen | birch_pollen | Birch Pollen | Birkenpollen |
| grass_pollen | grass_pollen | Grass Pollen | Gräserpollen |
| alder_pollen | alder_pollen | Alder Pollen | Erlenpollen |
| mugwort_pollen | mugwort_pollen | Mugwort Pollen | Beifußpollen |
| river_discharge | river_discharge | River Discharge | Abfluss |
| wave_height | wave_height | Wave Height | Wellenhöhe |
| wave_period | wave_period | Wave Period | Wellenperiode |
| wave_direction | wave_direction | Wave Direction | Wellenrichtung |
| water_temperature | water_temperature | Water Temperature | Wassertemperatur |

### Lifestyle Sensors (14)

| key | translation_key | EN name | DE name |
|---|---|---|---|
| laundry_score | laundry_score | Laundry Score | Wäsche-Score |
| commute_safety | commute_safety | Commute Safety | Pendlersicherheit |
| exercise | exercise | Exercise | Sport |
| outdoor_score | outdoor_score | Outdoor Score | Outdoor-Score |
| bbq_score | bbq_score | BBQ Score | Grill-Score |
| surf_conditions | surf_conditions | Surf Conditions | Surfbedingungen |
| pollen_trend | pollen_trend | Pollen Trend | Pollentrend |
| night_ventilation | night_ventilation | Night Ventilation | Nachtlüftung |
| confidence_trend | confidence_trend | Forecast Confidence | Vorhersagekonfidenz |
| solar_yield | solar_yield | Solar Yield | Solarertrag |
| flood_peak | flood_peak | Flood Peak Discharge | Hochwasser-Spitzenabfluss |
| front_approaching | front_approaching | Front Approaching | Frontannäherung |
| seasonal_anomaly | seasonal_anomaly | Seasonal Temperature Anomaly | Saisonale Temperaturanomalie |
| model_spread | model_spread | Model Temperature Spread | Modell-Temperaturspreizung |

### Lifestyle Binary Sensors (2)

| key | translation_key | EN name | DE name |
|---|---|---|---|
| ventilation_window | ventilation_window | Ventilation Window | Lüftungsfenster |
| planting_safe | planting_safe | Planting Safe | Frostsicher zum Pflanzen |

### Diagnostic Sensors (5)

| key | translation_key | EN name | DE name |
|---|---|---|---|
| monthly_usage | monthly_usage | Monthly Usage | Monatlicher Verbrauch |
| daily_usage | daily_usage | Daily Usage | Täglicher Verbrauch |
| version | version | Version | Version |
| uptime | uptime | Uptime | Betriebszeit |
| target_poll | target_poll | Poll {location} | Abfrage {location} |

### Event Entity (1)

| key | translation_key | Status |
|---|---|---|
| weather_alert | weather_alert | Already correct, remove redundant _attr_name |

## Enum States

### Commute Safety

| wire value | EN | DE |
|---|---|---|
| safe | Safe | Sicher |
| caution | Caution | Vorsicht |
| dangerous | Dangerous | Gefährlich |

### Exercise

| wire value | EN | DE |
|---|---|---|
| safe | Safe | Sicher |
| caution | Caution | Vorsicht |
| avoid | Avoid | Vermeiden |

## Score Attributes

Score-type sensors (Laundry, Outdoor, BBQ, Surf) gain two optional attributes:
- `best_start`: ISO 8601 timestamp of optimal window start, or None
- `best_hours`: integer duration of optimal window in hours, or None
