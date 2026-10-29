#!/usr/bin/env bash
set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────
DURATION="4h"
INTERVAL="5m"
RESTART_AT="2h"
HA_TOKEN=""
NJORD_URL="http://localhost:8080"
GRPC_HOST="localhost:8081"
HA_URL="http://localhost:8123"
OUTPUT_DIR="e2e/soak"
CONTAINER_NAME="njord-e2e"

# ── Argument parsing ─────────────────────────────────────────────
usage() {
  cat <<'EOF'
Usage: soak-collector.sh --ha-token TOKEN [OPTIONS]

Options:
  --duration DURATION     Total run time (default: 4h)
  --interval INTERVAL     Tick interval (default: 5m)
  --restart-at TIME       Container restart time (default: 2h)
  --ha-token TOKEN        HA long-lived access token (required)
  --njord-url URL         njord HTTP base URL (default: http://localhost:8080)
  --grpc-host HOST:PORT   gRPC endpoint (default: localhost:8081)
  --ha-url URL            Home Assistant base URL (default: http://localhost:8123)
  --output-dir DIR        Output directory (default: e2e/soak)
  --container NAME        Docker container name (default: njord-e2e)
  --help                  Show this help
EOF
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --duration)     DURATION="$2"; shift 2 ;;
    --interval)     INTERVAL="$2"; shift 2 ;;
    --restart-at)   RESTART_AT="$2"; shift 2 ;;
    --ha-token)     HA_TOKEN="$2"; shift 2 ;;
    --njord-url)    NJORD_URL="$2"; shift 2 ;;
    --grpc-host)    GRPC_HOST="$2"; shift 2 ;;
    --ha-url)       HA_URL="$2"; shift 2 ;;
    --output-dir)   OUTPUT_DIR="$2"; shift 2 ;;
    --container)    CONTAINER_NAME="$2"; shift 2 ;;
    --help)         usage ;;
    *)              echo "Unknown option: $1"; usage ;;
  esac
done

if [[ -z "$HA_TOKEN" ]]; then
  echo "ERROR: --ha-token is required"
  usage
fi

# ── Duration parsing ─────────────────────────────────────────────
parse_duration() {
  local input="$1"
  local seconds=0
  if [[ "$input" =~ ^([0-9]+)h$ ]]; then
    seconds=$(( ${BASH_REMATCH[1]} * 3600 ))
  elif [[ "$input" =~ ^([0-9]+)m$ ]]; then
    seconds=$(( ${BASH_REMATCH[1]} * 60 ))
  elif [[ "$input" =~ ^([0-9]+)s$ ]]; then
    seconds=${BASH_REMATCH[1]}
  elif [[ "$input" =~ ^([0-9]+)h([0-9]+)m$ ]]; then
    seconds=$(( ${BASH_REMATCH[1]} * 3600 + ${BASH_REMATCH[2]} * 60 ))
  else
    echo "ERROR: Cannot parse duration '$input' (use Nh, Nm, Ns, or NhMm)"
    exit 1
  fi
  echo "$seconds"
}

DURATION_SEC=$(parse_duration "$DURATION")
INTERVAL_SEC=$(parse_duration "$INTERVAL")
RESTART_AT_SEC=$(parse_duration "$RESTART_AT")

# ── Output files ─────────────────────────────────────────────────
mkdir -p "$OUTPUT_DIR"
RESULTS_FILE="$OUTPUT_DIR/soak-results.jsonl"
EVENTS_FILE="$OUTPUT_DIR/soak-events.log"

> "$RESULTS_FILE"
> "$EVENTS_FILE"

# ── Logging ──────────────────────────────────────────────────────
log_event() {
  local level="$1"
  local msg="$2"
  local ts
  ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  echo "$ts [$level] $msg" >> "$EVENTS_FILE"
  echo "$ts [$level] $msg"
}

# ── JSON helpers ─────────────────────────────────────────────────
json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  s="${s//$'\n'/\\n}"
  s="${s//$'\t'/\\t}"
  printf '%s' "$s"
}

# ── Collection functions ─────────────────────────────────────────

collect_health() {
  local body http_code alive_ok

  body=$(curl -sf --max-time 5 "$NJORD_URL/healthz" 2>/dev/null) || body="unreachable"
  http_code=$(curl -so /dev/null --max-time 5 -w "%{http_code}" "$NJORD_URL/healthz" 2>/dev/null) || http_code="0"
  if curl -sf --max-time 5 "$NJORD_URL/alive" >/dev/null 2>&1; then
    alive_ok="true"
  else
    alive_ok="false"
  fi

  printf '{"status":"%s","http_code":%s,"alive":%s}' \
    "$(json_escape "$body")" "$http_code" "$alive_ok"
}

collect_metrics() {
  local raw
  raw=$(curl -sf --max-time 10 "$NJORD_URL/metrics" 2>/dev/null) || {
    printf '{"metrics":{},"budget":{}}'
    return
  }

  extract_sum() {
    local name="$1"
    echo "$raw" | grep "^${name}" | grep -v '^#' | awk '{s+=$2} END {printf "%.0f", s+0}'
  }

  extract_gauge() {
    local name="$1"
    echo "$raw" | grep "^${name} " | grep -v '^#' | awk '{printf "%.0f", $2+0}'
  }

  local fetch_total polls_total cycle_learned events_total skipped_total failed_total changed_total
  local budget_monthly budget_daily budget_limit_monthly budget_limit_daily

  fetch_total=$(extract_sum "njord_openmeteo_fetch_total")
  polls_total=$(extract_sum "njord_pipeline_domain_polls_total")
  cycle_learned=$(extract_sum "njord_pipeline_cycle_learned_total")
  events_total=$(extract_sum "njord_pipeline_events_total")
  skipped_total=$(extract_sum "njord_pipeline_handler_skipped_total")
  failed_total=$(extract_sum "njord_pipeline_producer_failed_total")
  changed_total=$(extract_sum "njord_pipeline_data_changed_total")

  budget_monthly=$(extract_gauge "njord_budget_used_monthly")
  budget_daily=$(extract_gauge "njord_budget_used_daily")
  budget_limit_monthly=$(extract_gauge "njord_budget_limit_monthly")
  budget_limit_daily=$(extract_gauge "njord_budget_limit_daily")

  : "${fetch_total:=0}" "${polls_total:=0}" "${cycle_learned:=0}"
  : "${events_total:=0}" "${skipped_total:=0}" "${failed_total:=0}" "${changed_total:=0}"
  : "${budget_monthly:=0}" "${budget_daily:=0}" "${budget_limit_monthly:=0}" "${budget_limit_daily:=0}"

  printf '{"metrics":{"fetch_total":%s,"polls_total":%s,"cycle_learned_total":%s,"pipeline_events_total":%s,"pipeline_skipped_total":%s,"pipeline_failed_total":%s,"data_changed_total":%s},"budget":{"monthly_used":%s,"daily_used":%s,"monthly_limit":%s,"daily_limit":%s}}' \
    "$fetch_total" "$polls_total" "$cycle_learned" \
    "$events_total" "$skipped_total" "$failed_total" "$changed_total" \
    "$budget_monthly" "$budget_daily" "$budget_limit_monthly" "$budget_limit_daily"
}

collect_grpc_status() {
  local raw
  raw=$(grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto \
    "$GRPC_HOST" njord.v2.OpsService/GetStatus 2>/dev/null) || {
    printf '{"reachable":false}'
    return
  }

  local uptime phase miss_count cycle_seconds enrichment_count budget_partial models_partial

  uptime=$(echo "$raw" | jq -r '.uptimeSeconds // "0"')
  phase=$(echo "$raw" | jq -r '.models[0].phase // "unknown"')
  miss_count=$(echo "$raw" | jq -r '.models[0].missCount // 0')
  cycle_seconds=$(echo "$raw" | jq -r '.models[0].cycleSeconds // "0"')
  enrichment_count=$(echo "$raw" | jq -r '.activeEnrichments | length')
  budget_partial=$(echo "$raw" | jq -r '.budgetPartial // false')
  models_partial=$(echo "$raw" | jq -r '.modelsPartial // false')

  printf '{"reachable":true,"uptime_seconds":%s,"phase":"%s","miss_count":%s,"cycle_seconds":%s,"enrichment_count":%s,"budget_partial":%s,"models_partial":%s}' \
    "$uptime" "$phase" "$miss_count" "$cycle_seconds" "$enrichment_count" "$budget_partial" "$models_partial"
}

collect_ha_entities() {
  local raw
  raw=$(curl -sf --max-time 15 \
    -H "Authorization: Bearer $HA_TOKEN" \
    "$HA_URL/api/states" 2>/dev/null) || {
    printf '{"reachable":false}'
    return
  }

  local total unavailable_count weather_state temperature
  local stream_domain stream_enrichment alerts_on alerts_off
  local stalest_entity stalest_age_min unavailable_ids

  total=$(echo "$raw" | jq '[.[] | select(.entity_id | test("njord|barcelona"))] | length')
  unavailable_count=$(echo "$raw" | jq '[.[] | select(.entity_id | test("njord|barcelona")) | select(.state == "unavailable")] | length')
  unavailable_ids=$(echo "$raw" | jq -c '[.[] | select(.entity_id | test("njord|barcelona")) | select(.state == "unavailable") | .entity_id]')

  weather_state=$(echo "$raw" | jq -r '[.[] | select(.entity_id == "weather.barcelona")] | .[0].state // "missing"')
  temperature=$(echo "$raw" | jq -r '[.[] | select(.entity_id == "weather.barcelona")] | .[0].attributes.temperature // "null"')

  stream_domain=$(echo "$raw" | jq -r '[.[] | select(.entity_id | test("server_domain_data_stream"))] | .[0].state // "missing"')
  stream_enrichment=$(echo "$raw" | jq -r '[.[] | select(.entity_id | test("server_enrichment_stream"))] | .[0].state // "missing"')

  alerts_on=$(echo "$raw" | jq '[.[] | select(.entity_id | test("_alert$")) | select(.state == "on")] | length')
  alerts_off=$(echo "$raw" | jq '[.[] | select(.entity_id | test("_alert$")) | select(.state == "off")] | length')

  local now_epoch stalest_epoch stalest_id stalest_diff
  now_epoch=$(date -u +%s)

  stalest_id=$(echo "$raw" | jq -r '
    [.[] | select(.entity_id | test("njord|barcelona")) | select(.state != "unavailable") |
     select(.attributes.last_updated != null) |
     {entity_id, last_updated: .last_updated}] |
    sort_by(.last_updated) | .[0].entity_id // "none"')

  if [[ "$stalest_id" != "none" ]]; then
    local stalest_ts
    stalest_ts=$(echo "$raw" | jq -r --arg eid "$stalest_id" '
      [.[] | select(.entity_id == $eid)] | .[0].last_updated // ""')
    if [[ -n "$stalest_ts" ]]; then
      stalest_epoch=$(date -u -d "${stalest_ts%+*}" +%s 2>/dev/null) || stalest_epoch=$now_epoch
      stalest_diff=$(( (now_epoch - stalest_epoch) / 60 ))
    else
      stalest_diff=0
    fi
  else
    stalest_id="none"
    stalest_diff=0
  fi

  local alerts_on_names
  alerts_on_names=$(echo "$raw" | jq -c '[.[] | select(.entity_id | test("_alert$")) | select(.state == "on") | .entity_id | sub("binary_sensor."; "") | sub("_alert$"; "")]')

  printf '{"reachable":true,"total":%s,"unavailable_count":%s,"unavailable_ids":%s,"weather_state":"%s","temperature":%s,"stream_domain":"%s","stream_enrichment":"%s","alerts_on":%s,"alerts_on_count":%s,"alerts_off_count":%s,"stalest_entity":"%s","stalest_age_min":%s}' \
    "$total" "$unavailable_count" "$unavailable_ids" \
    "$(json_escape "$weather_state")" "$temperature" \
    "$stream_domain" "$stream_enrichment" \
    "$alerts_on_names" "$alerts_on" "$alerts_off" \
    "$(json_escape "$stalest_id")" "$stalest_diff"
}

# ── Phase detection ──────────────────────────────────────────────
PHASE="warmup"
PREV_PHASE=""
FIRST_FETCH_SEEN=false
STEADY_SEEN=false

update_phase() {
  local elapsed_sec="$1"
  local fetch_total="$2"
  local grpc_phase="$3"

  PREV_PHASE="$PHASE"

  if [[ "$PHASE" == "warmup" ]] && [[ "$fetch_total" -gt 0 ]]; then
    PHASE="discovery"
    log_event "MILE" "First poll completed (fetch_total=$fetch_total)"
  fi

  if [[ "$PHASE" == "discovery" ]] && [[ "$grpc_phase" == "POLL_PHASE_STEADY" ]]; then
    PHASE="steady_state"
    log_event "MILE" "Discovery -> Learned transition (phase=STEADY)"
  fi

  if [[ "$PHASE" == "steady_state" ]] && [[ "$elapsed_sec" -ge "$RESTART_AT_SEC" ]]; then
    PHASE="pre_restart"
  fi

  # post_restart and recovery are set by the restart sequence, not here
}

# ── Container restart ────────────────────────────────────────────
PRE_RESTART_BUDGET=""
PRE_RESTART_ENTITIES=""
RESTART_DONE=false
RESTART_RECOVERY_SEC=0
RESTART_TOLERANCE_END=0

do_container_restart() {
  local budget_json="$1"
  local entities_json="$2"

  PRE_RESTART_BUDGET=$(echo "$budget_json" | jq -r '.monthly_used // 0' 2>/dev/null)
  PRE_RESTART_ENTITIES=$(echo "$entities_json" | jq -r '.total // 0' 2>/dev/null)

  log_event "RESTART" "Pre-restart snapshot: budget=$PRE_RESTART_BUDGET entities=$PRE_RESTART_ENTITIES"

  PHASE="restarting"
  local restart_start
  restart_start=$(date -u +%s)

  docker restart "$CONTAINER_NAME" >/dev/null 2>&1 || {
    log_event "FAIL" "docker restart failed"
    PHASE="post_restart"
    RESTART_DONE=true
    return
  }

  log_event "RESTART" "Container restart issued, waiting for /alive..."

  while true; do
    if curl -sf --max-time 3 "$NJORD_URL/alive" >/dev/null 2>&1; then
      RESTART_RECOVERY_SEC=$(( $(date -u +%s) - restart_start ))
      log_event "RESTART" "Alive after ${RESTART_RECOVERY_SEC}s"
      break
    fi
    local elapsed=$(( $(date -u +%s) - restart_start ))
    if [[ "$elapsed" -gt 60 ]]; then
      RESTART_RECOVERY_SEC=60
      log_event "FAIL" "Recovery exceeded 60s timeout"
      break
    fi
    sleep 2
  done

  PHASE="recovery"
  log_event "RESTART" "Waiting 30s for HA reconnection..."
  sleep 30

  PHASE="post_restart"
  RESTART_DONE=true
  RESTART_TOLERANCE_END=$(( $(date -u +%s) + 120 ))

  local post_budget post_entities
  post_budget=$(collect_metrics | jq -r '.budget.monthly_used // 0' 2>/dev/null) || post_budget="error"
  post_entities=$(collect_ha_entities | jq -r '.total // 0' 2>/dev/null) || post_entities="error"

  log_event "RESTART" "Post-restart snapshot: budget=$post_budget entities=$post_entities"

  if [[ "$PRE_RESTART_BUDGET" != "$post_budget" ]] && [[ "$post_budget" != "error" ]]; then
    log_event "FAIL" "Budget changed across restart: $PRE_RESTART_BUDGET -> $post_budget"
  fi

  if [[ "$PRE_RESTART_ENTITIES" != "$post_entities" ]] && [[ "$post_entities" != "error" ]]; then
    log_event "WARN" "Entity count changed across restart: $PRE_RESTART_ENTITIES -> $post_entities"
  fi
}

# ── Main loop ────────────────────────────────────────────────────
START_EPOCH=$(date -u +%s)
TICK=0
TOTAL_TICKS=$(( DURATION_SEC / INTERVAL_SEC ))

log_event "START" "Soak test started: duration=${DURATION} interval=${INTERVAL} restart_at=${RESTART_AT} ticks=${TOTAL_TICKS}"
log_event "START" "Config: njord=${NJORD_URL} grpc=${GRPC_HOST} ha=${HA_URL} container=${CONTAINER_NAME}"

while true; do
  TICK=$(( TICK + 1 ))
  local_ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  now_epoch=$(date -u +%s)
  elapsed_sec=$(( now_epoch - START_EPOCH ))
  elapsed_min=$(( elapsed_sec / 60 ))

  # Check if restart is due
  if [[ "$PHASE" == "pre_restart" ]] && [[ "$RESTART_DONE" == "false" ]]; then
    local metrics_for_restart entities_for_restart
    metrics_for_restart=$(collect_metrics 2>/dev/null) || metrics_for_restart='{}'
    entities_for_restart=$(collect_ha_entities 2>/dev/null) || entities_for_restart='{}'
    budget_for_restart=$(echo "$metrics_for_restart" | jq -c '.budget // {}' 2>/dev/null) || budget_for_restart='{}'
    do_container_restart "$budget_for_restart" "$entities_for_restart"
  fi

  # Check if within restart tolerance window
  in_restart_window="false"
  if [[ "$RESTART_TOLERANCE_END" -gt 0 ]] && [[ "$now_epoch" -lt "$RESTART_TOLERANCE_END" ]]; then
    in_restart_window="true"
  fi

  # Collect data
  health_json=$(collect_health 2>/dev/null) || health_json='{"status":"error","http_code":0,"alive":false}'
  metrics_budget_json=$(collect_metrics 2>/dev/null) || metrics_budget_json='{"metrics":{},"budget":{}}'
  grpc_json=$(collect_grpc_status 2>/dev/null) || grpc_json='{"reachable":false}'
  entities_json=$(collect_ha_entities 2>/dev/null) || entities_json='{"reachable":false}'

  # Extract values for phase detection
  fetch_total=$(echo "$metrics_budget_json" | jq -r '.metrics.fetch_total // 0' 2>/dev/null) || fetch_total=0
  grpc_phase=$(echo "$grpc_json" | jq -r '.phase // "unknown"' 2>/dev/null) || grpc_phase="unknown"

  # Update phase (only if not in restart sequence)
  if [[ "$PHASE" != "restarting" ]] && [[ "$PHASE" != "recovery" ]] && [[ "$PHASE" != "post_restart" ]] && [[ "$PHASE" != "pre_restart" ]]; then
    update_phase "$elapsed_sec" "$fetch_total" "$grpc_phase"
  fi

  # Set final phase
  if [[ "$TICK" -ge "$TOTAL_TICKS" ]]; then
    PHASE="complete"
  fi

  # Build errors array
  errors="[]"
  error_items=""
  if echo "$health_json" | jq -e '.status == "unreachable"' >/dev/null 2>&1; then
    if [[ "$in_restart_window" == "false" ]]; then
      error_items="${error_items:+$error_items,}\"healthz unreachable\""
    fi
  fi
  if echo "$grpc_json" | jq -e '.reachable == false' >/dev/null 2>&1; then
    if [[ "$in_restart_window" == "false" ]]; then
      error_items="${error_items:+$error_items,}\"grpc unreachable\""
    fi
  fi
  if echo "$entities_json" | jq -e '.reachable == false' >/dev/null 2>&1; then
    if [[ "$in_restart_window" == "false" ]]; then
      error_items="${error_items:+$error_items,}\"ha unreachable\""
    fi
  fi
  if [[ -n "$error_items" ]]; then
    errors="[$error_items]"
  fi

  # Extract sub-objects
  metrics_json=$(echo "$metrics_budget_json" | jq -c '.metrics // {}' 2>/dev/null) || metrics_json='{}'
  budget_json=$(echo "$metrics_budget_json" | jq -c '.budget // {}' 2>/dev/null) || budget_json='{}'

  # Assemble and write JSONL line
  printf '{"tick":%d,"ts":"%s","elapsed_min":%d,"phase":"%s","in_restart_window":%s,"health":%s,"metrics":%s,"budget":%s,"grpc_status":%s,"entities":%s,"errors":%s}\n' \
    "$TICK" "$local_ts" "$elapsed_min" "$PHASE" "$in_restart_window" \
    "$health_json" "$metrics_json" "$budget_json" "$grpc_json" "$entities_json" "$errors" \
    >> "$RESULTS_FILE"

  # Log tick
  health_status=$(echo "$health_json" | jq -r '.status // "?"' 2>/dev/null) || health_status="?"
  entity_total=$(echo "$entities_json" | jq -r '.total // "?"' 2>/dev/null) || entity_total="?"
  budget_used=$(echo "$budget_json" | jq -r '.monthly_used // "?"' 2>/dev/null) || budget_used="?"
  log_event "TICK" "tick=$TICK phase=$PHASE health=$health_status entities=$entity_total budget=$budget_used elapsed=${elapsed_min}m"

  # Check for completion
  if [[ "$PHASE" == "complete" ]]; then
    log_event "END" "Soak test complete: ${TICK} ticks, ${elapsed_min} minutes"
    break
  fi

  # Sleep until next tick
  sleep "$INTERVAL_SEC"
done

log_event "END" "Output: $RESULTS_FILE ($TICK entries)"
echo ""
echo "Soak test complete. Results: $RESULTS_FILE"
echo "Events: $EVENTS_FILE"
