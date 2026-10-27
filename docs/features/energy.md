# Energy Features

Energy features help solar and wind energy owners forecast production, track performance, and optimize self-consumption.

## SolarYield

Forecasts your solar panel energy output using Global Tilted Irradiance (GTI) -- the solar radiation that actually hits your panels at their specific tilt and orientation.

Configure your system:
- **PeakKw**: installed peak power in kW (default: 5.0)
- **Tilt**: panel tilt angle in degrees from horizontal (default: 30)
- **Azimuth**: panel compass direction, 180 = south (default: 180)

**Entity:** `sensor.<location>_solar_yield`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Energy__SolarYield__Enabled: "true"
Njord__SolarYield__PeakKw: "5.0"
Njord__SolarYield__Tilt: "30"
Njord__SolarYield__Azimuth: "180"
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
Njord__Features__Energy__SatelliteRadiation__Enabled: "true"
Njord__Satellite__PastHours: "6"
```

::: tip
SatelliteRadiation uses about 720 API calls/month/location. Enable it only if you need the satellite ground-truth blend for solar optimization.
:::

---

## ElevatedWind

Reports wind speed and direction at turbine hub heights (80 m and 120 m above ground). Ground-level wind (10 m) underestimates what a turbine experiences due to the wind speed profile increasing with altitude.

**Entity:** `sensor.<location>_elevated_wind`
**Domains:** Weather | **Default:** Off

```yaml
Njord__Features__Energy__ElevatedWind__Enabled: "true"
```

**Automation ideas:**
- Estimate wind turbine output from 80 m or 120 m wind speed
- Alert when wind exceeds turbine cut-out speed (typically 25 m/s)
- Dashboard showing wind speed at ground vs hub height
