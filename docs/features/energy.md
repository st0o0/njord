# Energy Features

Energy features help solar and wind energy owners forecast production, track performance, and optimize self-consumption.

## SolarYield

Forecasts your solar panel energy output using Global Tilted Irradiance (GTI) -- the solar radiation that actually hits your panels at their specific tilt and orientation.

Configure your system:
- **PeakKw**: installed peak power in kW (default: 5.0), `Njord:Features:SolarYield:PeakKw`
- **SystemEfficiency**: system efficiency factor (default: 0.8)
- **BatteryCapacityKwh**: battery capacity in kWh, 0 disables the battery strategy (default: 0)
- **Tilt**: panel tilt angle in degrees from horizontal (default: 30), `Njord:Satellite:Tilt`
- **Azimuth**: panel compass direction, 180 = south (default: 180), `Njord:Satellite:Azimuth`

**Entity:** `sensor.<location>_solar_yield`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__SolarYield__Enabled: "true"
Njord__Features__SolarYield__PeakKw: "5.0"
Njord__Satellite__Tilt: "30"
Njord__Satellite__Azimuth: "180"
```

**Automation ideas:**
- Schedule high-power appliances (dishwasher, EV charging) during peak solar hours
- Compare forecast vs actual production from your inverter
- Send morning notification: "Expected yield today: 28 kWh"

---

## SatelliteRadiation

Blends real satellite observations of solar irradiance with the weather forecast. The satellite data gives ground-truth for the last few hours; the forecast extends into the future. The blend transitions smoothly at the observation boundary.

Requires the same tilt/azimuth configuration as SolarYield. The satellite data comes from a separate Open-Meteo API endpoint.

**Entity:** `sensor.<location>_satellite_radiation`
**Domains:** Satellite | **Default:** Off

```yaml
Njord__Features__SatelliteRadiation__Enabled: "true"
Njord__Satellite__PastHours: "6"
```

::: tip
SatelliteRadiation uses about 720 API calls/month/location. Enable it only if you need the satellite ground-truth blend for solar optimization.
:::

---

## ElevatedWind

Reports wind speed and direction at turbine hub heights (80 m and 120 m above ground). Ground-level wind (10 m) underestimates what a turbine experiences due to the wind speed profile increasing with altitude.

**Entity:** `sensor.<location>_elevated_wind`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__ElevatedWind__Enabled: "true"
```

**Automation ideas:**
- Estimate wind turbine output from 80 m or 120 m wind speed
- Alert when wind exceeds turbine cut-out speed (typically 25 m/s)
- Dashboard showing wind speed at ground vs hub height

---

## HeatPumpCop

Estimates the heat pump coefficient of performance (COP) from the outdoor temperature: `COP = 6.81 - 0.121 x (target - outdoor)`. Reports a recommendation:

| Recommendation | COP |
|----------------|-----|
| ideal | > 4.0 |
| normal | 2.5 -- 4.0 |
| inefficient | 1.5 -- 2.5 |
| avoid | < 1.5 |

**Entity:** `sensor.<location>_heat_pump_cop`
**Domains:** Forecast | **Default:** Off

```yaml
Njord__Features__HeatPumpCop__Enabled: "true"
Njord__Features__HeatPumpCop__TargetTemperature: "35"
```

`TargetTemperature` is the flow temperature of the heating circuit (default 35 °C).

**Automation ideas:**
- Shift hot water heating to hours with an `ideal` COP
- Switch to a backup heater when the recommendation is `avoid`
- Pre-heat the house during efficient hours

