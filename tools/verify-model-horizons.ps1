<#
.SYNOPSIS
  Verify actual forecast horizons of Open-Meteo models by probing the live API.
.DESCRIPTION
  For each model, requests forecast_days=16 and counts non-null hourly values
  to determine the real forecast horizon. Compares against ModelCoverageRegistry.
.EXAMPLE
  pwsh tools/verify-model-horizons.ps1
  pwsh tools/verify-model-horizons.ps1 -Models icon_d2,ecmwf_ifs025
#>
param(
    [string]$Models = "",
    [double]$Lat = 47.05,
    [double]$Lon = 8.31
)

$api = "https://api.open-meteo.com/v1/forecast"

$allModels = @(
    # Global
    "icon_global", "ecmwf_ifs025", "gfs_seamless", "aifs",
    "ukmo_global_deterministic_10km", "arpege_world", "gem_global",
    "jma_gsm", "kma_gdps", "bom_access_global", "cma_grapes_global",
    # Europe
    "icon_eu", "arpege_europe",
    "knmi_harmonie_arome_europe", "dmi_harmonie_arome_europe",
    # Regional
    "icon_d2", "meteoswiss_icon_ch1", "meteoswiss_icon_ch2",
    "geosphere_arome_austria", "knmi_harmonie_arome_netherlands",
    "metno_nordic", "arome_france", "arome_france_hd",
    "ukmo_uk_2km", "arpae_cosmo_2i",
    # US/CA
    "hrrr_conus", "nbm_conus", "nam_conus",
    "gem_regional", "gem_hrdps_continental",
    # Asia
    "jma_msm", "kma_ldps"
)

if ($Models) {
    $allModels = $Models -split ","
}

$results = @()

foreach ($model in $allModels) {
    $url = "${api}?latitude=${Lat}&longitude=${Lon}&models=${model}&hourly=temperature_2m&daily=temperature_2m_max&wind_speed_unit=ms&timeformat=unixtime&forecast_days=16"

    try {
        $resp = Invoke-RestMethod -Uri $url -TimeoutSec 15 -ErrorAction Stop

        $times = $resp.hourly.time
        $temps = $resp.hourly.temperature_2m

        $nonNull = 0
        $lastIdx = -1
        for ($i = 0; $i -lt $temps.Count; $i++) {
            if ($null -ne $temps[$i]) {
                $nonNull++
                $lastIdx = $i
            }
        }

        $hourlyHorizon = 0
        if ($lastIdx -ge 0 -and $times.Count -gt 0) {
            $hourlyHorizon = [math]::Floor(($times[$lastIdx] - $times[0]) / 3600)
        }

        $dailyTemps = $resp.daily.temperature_2m_max
        $dailyNonNull = 0
        if ($dailyTemps) {
            foreach ($v in $dailyTemps) {
                if ($null -ne $v) { $dailyNonNull++ }
            }
        }

        $results += [PSCustomObject]@{
            Model       = $model
            HourlyPts   = $nonNull
            HourlyHours = "${hourlyHorizon}h"
            DailyDays   = "${dailyNonNull}d"
            Status      = "OK"
        }
    }
    catch {
        $msg = $_.Exception.Message
        if ($msg -match '"reason":"([^"]+)"') {
            $msg = $Matches[1]
        }
        $results += [PSCustomObject]@{
            Model       = $model
            HourlyPts   = "-"
            HourlyHours = "-"
            DailyDays   = "-"
            Status      = $msg.Substring(0, [math]::Min(60, $msg.Length))
        }
    }

    Start-Sleep -Milliseconds 500
}

$results | Format-Table -AutoSize
