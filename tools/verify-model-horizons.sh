#!/usr/bin/env bash
# Verify actual forecast horizons of Open-Meteo models by probing the API.
# Usage: ./tools/verify-model-horizons.sh [model_id ...]
# Without arguments, checks all models from the registry.
#
# For each model, requests forecast_days=16 (maximum) and counts
# how many non-null hourly temperature_2m values the API returns.

set -euo pipefail

API="https://api.open-meteo.com/v1/forecast"
LAT=47.05
LON=8.31

MODELS=(
  # Global
  icon_global ecmwf_ifs025 gfs_seamless aifs
  ukmo_global_deterministic_10km arpege_world gem_global
  jma_gsm kma_gdps bom_access_global cma_grapes_global
  # Europe
  icon_eu arpege_europe
  knmi_harmonie_arome_europe dmi_harmonie_arome_europe
  # Regional CH/DE/AT
  icon_d2 meteoswiss_icon_ch1 meteoswiss_icon_ch2
  geosphere_arome_austria
  # Regional other
  knmi_harmonie_arome_netherlands metno_nordic
  arome_france arome_france_hd
  ukmo_uk_2km arpae_2i
  # US/CA
  hrrr_us_conus nbm_us_conus nam_us_conus
  gem_regional gem_hrdps_continental
  # Asia
  jma_msm kma_ldps
)

if [ $# -gt 0 ]; then
  MODELS=("$@")
fi

printf "%-45s %6s %6s %s\n" "MODEL" "HOURLY" "DAILY" "LAST_HOURLY_OFFSET"
printf "%-45s %6s %6s %s\n" "-----" "------" "------" "------------------"

for model in "${MODELS[@]}"; do
  url="${API}?latitude=${LAT}&longitude=${LON}&models=${model}&hourly=temperature_2m&daily=temperature_2m_max&wind_speed_unit=ms&timeformat=unixtime&forecast_days=16"

  response=$(curl -s --max-time 10 "$url" 2>/dev/null || echo '{"error":true}')

  if echo "$response" | grep -q '"error"'; then
    reason=$(echo "$response" | grep -o '"reason":"[^"]*"' | head -1 | cut -d'"' -f4)
    printf "%-45s %6s %6s %s\n" "$model" "ERROR" "-" "${reason:-unknown}"
    sleep 0.5
    continue
  fi

  # Count non-null hourly values
  hourly_count=$(echo "$response" | python3 -c "
import sys, json
d = json.load(sys.stdin)
temps = d.get('hourly', {}).get('temperature_2m', [])
non_null = [t for t in temps if t is not None]
times = d.get('hourly', {}).get('time', [])
if non_null and times:
    first = times[0]
    last_idx = max(i for i, t in enumerate(temps) if t is not None)
    last = times[last_idx]
    hours = (last - first) / 3600
    print(f'{len(non_null)} {hours:.0f}')
else:
    print('0 0')
" 2>/dev/null || echo "0 0")

  h_count=$(echo "$hourly_count" | cut -d' ' -f1)
  h_hours=$(echo "$hourly_count" | cut -d' ' -f2)

  # Count non-null daily values
  daily_count=$(echo "$response" | python3 -c "
import sys, json
d = json.load(sys.stdin)
temps = d.get('daily', {}).get('temperature_2m_max', [])
non_null = [t for t in temps if t is not None]
print(len(non_null))
" 2>/dev/null || echo "0")

  printf "%-45s %6s %6s %sh\n" "$model" "$h_count" "${daily_count}d" "$h_hours"

  sleep 0.5
done
