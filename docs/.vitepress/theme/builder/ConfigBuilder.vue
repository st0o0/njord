<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { defaultConfig, type NjordConfig, type LocationConfig, type ModelInfo, type EnrichmentFeature } from './types'
import { computeBudget, checkCoverage } from './budget'
import { exportAsJson, exportAsEnvVars, exportAsCompose, autoImport, encodeHash, decodeHash } from './serializer'
import modelsData from '../../../data/models.json'
import enrichmentData from '../../../data/enrichment.json'

const models = modelsData as ModelInfo[]
const enrichmentFeatures = enrichmentData as EnrichmentFeature[]
const config = reactive<NjordConfig>(defaultConfig())

function initEnrichment(): Record<string, Record<string, unknown>> {
  const result: Record<string, Record<string, unknown>> = {}
  for (const f of enrichmentFeatures) {
    if (f.enabledByDefault) {
      result[f.name] = { Enabled: true }
    }
  }
  return result
}
config.enrichment = initEnrichment()

function isEnrichmentEnabled(name: string): boolean {
  return !!config.enrichment[name]?.Enabled
}

function toggleEnrichment(name: string) {
  if (isEnrichmentEnabled(name)) {
    delete config.enrichment[name]
  } else {
    config.enrichment[name] = { Enabled: true }
  }
}

function getEnrichmentOption(feature: string, option: string, defaultVal: unknown): unknown {
  return config.enrichment[feature]?.[option] ?? defaultVal
}

function setEnrichmentOption(feature: string, option: string, value: unknown, defaultVal: unknown) {
  if (!config.enrichment[feature]) return
  if (value === defaultVal || value === '' || value === undefined) {
    delete config.enrichment[feature][option]
  } else {
    config.enrichment[feature][option] = value
  }
}

const HORIZON_PRESETS: Record<string, number[]> = {
  standard: [3, 6, 12, 24, 48, 72],
  fine: [1, 2, 3, 6, 8, 12, 18, 24, 36, 48, 72, 96],
}

const horizonPreset = reactive({ active: 'standard', custom: '' })
const exportMode = reactive({ format: 'json' as 'json' | 'env' | 'compose' })
const importText = reactive({ value: '', error: '' })
const copied = reactive({ show: false })

const budget = computed(() => computeBudget(config, models))
const exportOutput = computed(() => {
  if (exportMode.format === 'json') return exportAsJson(config)
  if (exportMode.format === 'compose') return exportAsCompose(config)
  return exportAsEnvVars(config)
})

const commonModels = computed(() =>
  models.filter(m => ['icon_d2', 'icon_eu', 'icon_global', 'ecmwf_ifs025', 'gfs_seamless',
    'ukmo_seamless', 'arpege_europe', 'knmi_harmonie_arome_europe',
    'knmi_harmonie_arome_netherlands', 'dmi_harmonie_arome_europe',
    'meteoswiss_icon_ch1', 'meteoswiss_icon_ch2', 'metno_nordic'].includes(m.id))
)

function toggleModel(id: string) {
  const idx = config.models.indexOf(id)
  if (idx >= 0) config.models.splice(idx, 1)
  else config.models.push(id)
}

interface GeoResult {
  name: string
  country: string
  admin1?: string
  latitude: number
  longitude: number
}

const geoSearch = reactive({ query: '', results: [] as GeoResult[], loading: false })
let geoDebounce: ReturnType<typeof setTimeout> | null = null

function onGeoInput() {
  if (geoDebounce) clearTimeout(geoDebounce)
  if (geoSearch.query.length < 2) { geoSearch.results = []; return }
  geoSearch.loading = true
  geoDebounce = setTimeout(async () => {
    try {
      const res = await fetch(
        `https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(geoSearch.query)}&count=5&language=en`)
      const data = await res.json()
      geoSearch.results = (data.results ?? []).map((r: any) => ({
        name: r.name, country: r.country, admin1: r.admin1,
        latitude: Math.round(r.latitude * 10000) / 10000,
        longitude: Math.round(r.longitude * 10000) / 10000,
      }))
    } catch { geoSearch.results = [] }
    geoSearch.loading = false
  }, 300)
}

function selectGeoResult(r: GeoResult) {
  config.locations.push({ name: r.name.toLowerCase(), latitude: r.latitude, longitude: r.longitude, models: [] })
  geoSearch.query = ''
  geoSearch.results = []
}

function addLocationManual() {
  config.locations.push({ name: '', latitude: 0, longitude: 0, models: [] })
}

function removeLocation(i: number) {
  config.locations.splice(i, 1)
}

function toggleLocationModel(loc: LocationConfig, id: string) {
  const idx = loc.models.indexOf(id)
  if (idx >= 0) loc.models.splice(idx, 1)
  else loc.models.push(id)
}

function setHorizonPreset(name: string) {
  horizonPreset.active = name
  if (HORIZON_PRESETS[name]) config.horizons = [...HORIZON_PRESETS[name]]
}

function applyCustomHorizons() {
  const vals = horizonPreset.custom.split(/[,\s]+/).map(Number).filter(n => n >= 1 && n <= 96)
  if (vals.length) { config.horizons = vals; horizonPreset.active = 'custom' }
}

function toggleGroup(g: string) {
  const idx = config.parameters.groups.indexOf(g)
  if (idx >= 0) config.parameters.groups.splice(idx, 1)
  else config.parameters.groups.push(g)
}

function copyExport() {
  navigator.clipboard.writeText(exportOutput.value)
  copied.show = true
  setTimeout(() => copied.show = false, 2000)
}

function doImport() {
  importText.error = ''
  try {
    const partial = autoImport(importText.value)
    Object.assign(config, { ...defaultConfig(), ...partial })
    importText.value = ''
  } catch (e) {
    importText.error = e instanceof Error ? e.message : 'Invalid input'
  }
}

function modelCovers(modelId: string, loc: LocationConfig): boolean {
  return checkCoverage(modelId, loc.latitude, loc.longitude, models)
}

function modelMaxDays(modelId: string): string {
  const m = models.find(x => x.id === modelId)
  if (!m?.maxForecastHours) return ''
  const days = Math.ceil(m.maxForecastHours / 24)
  return days < config.forecastDays ? `→ capped to ${days}d` : ''
}

watch(config, () => {
  if (typeof window !== 'undefined')
    window.location.hash = 'config=' + encodeHash(config)
}, { deep: true })

onMounted(() => {
  const hash = window.location.hash.replace('#config=', '')
  if (hash) {
    const restored = decodeHash(hash)
    if (restored) Object.assign(config, { ...defaultConfig(), ...restored })
  }
})
</script>

<template>
  <div class="builder">

    <!-- Import -->
    <details class="section">
      <summary>Import existing config</summary>
      <div class="import-box">
        <textarea v-model="importText.value" placeholder="Paste appsettings.json or env vars here..." rows="4" />
        <button @click="doImport" :disabled="!importText.value.trim()">Import</button>
        <p v-if="importText.error" class="error">{{ importText.error }}</p>
      </div>
    </details>

    <!-- Locations -->
    <div class="section">
      <h3>Locations</h3>
      <div v-for="(loc, i) in config.locations" :key="i" class="location-card">
        <div class="location-row">
          <input v-model="loc.name" placeholder="Name" class="input-name" />
          <input v-model.number="loc.latitude" type="number" step="0.001" placeholder="Lat" class="input-coord" />
          <input v-model.number="loc.longitude" type="number" step="0.001" placeholder="Lon" class="input-coord" />
          <button @click="removeLocation(i)" class="btn-remove">Remove</button>
        </div>
        <div class="location-models">
          <span class="label">Per-location models:</span>
          <label v-for="m in commonModels" :key="m.id" class="model-chip" :class="{ warn: !modelCovers(m.id, loc) }">
            <input type="checkbox" :checked="loc.models.includes(m.id)" @change="toggleLocationModel(loc, m.id)" />
            {{ m.id }}
            <span v-if="!modelCovers(m.id, loc)" class="warn-icon" title="May not cover this location">⚠</span>
          </label>
        </div>
      </div>
      <div class="geo-search">
        <div class="geo-input-row">
          <input v-model="geoSearch.query" @input="onGeoInput" placeholder="Search for a city..." class="geo-input" />
          <button @click="addLocationManual" class="btn-add" title="Add manually">+ Manual</button>
        </div>
        <div v-if="geoSearch.results.length" class="geo-results">
          <div v-for="r in geoSearch.results" :key="`${r.latitude}-${r.longitude}`" class="geo-result" @click="selectGeoResult(r)">
            <span class="geo-name">{{ r.name }}</span>
            <span class="geo-detail">{{ [r.admin1, r.country].filter(Boolean).join(', ') }}</span>
            <span class="geo-coords">{{ r.latitude }}°, {{ r.longitude }}°</span>
          </div>
        </div>
        <div v-if="geoSearch.loading" class="geo-loading">Searching...</div>
      </div>
    </div>

    <!-- Models (global) -->
    <div class="section">
      <h3>Global Models</h3>
      <div class="model-grid">
        <label v-for="m in commonModels" :key="m.id" class="model-card" :class="{ selected: config.models.includes(m.id) }">
          <input type="checkbox" :checked="config.models.includes(m.id)" @change="toggleModel(m.id)" />
          <div>
            <strong>{{ m.id }}</strong>
            <span class="model-meta">{{ m.tier }} · {{ m.region }} · {{ m.maxForecastHours }}h</span>
            <span v-if="modelMaxDays(m.id)" class="model-cap">{{ modelMaxDays(m.id) }}</span>
          </div>
        </label>
      </div>
    </div>

    <!-- Horizons -->
    <div class="section">
      <h3>Horizons</h3>
      <div class="preset-row">
        <button v-for="(_, name) in HORIZON_PRESETS" :key="name" :class="{ active: horizonPreset.active === name }" @click="setHorizonPreset(name)">{{ name }}</button>
        <button :class="{ active: horizonPreset.active === 'custom' }" @click="horizonPreset.active = 'custom'">custom</button>
      </div>
      <div v-if="horizonPreset.active === 'custom'" class="custom-horizons">
        <input v-model="horizonPreset.custom" placeholder="e.g. 1, 3, 6, 12, 24, 48" @keyup.enter="applyCustomHorizons" />
        <button @click="applyCustomHorizons">Apply</button>
      </div>
      <code class="horizons-display">{{ config.horizons.join(', ') }}</code>
    </div>

    <!-- Parameters -->
    <div class="section">
      <h3>Parameters</h3>
      <div class="param-row">
        <label v-for="g in ['Weather', 'Solar', 'Soil']" :key="g" class="param-card" :class="{ selected: config.parameters.groups.includes(g) }">
          <input type="checkbox" :checked="config.parameters.groups.includes(g)" @change="toggleGroup(g)" />
          {{ g }}
          <span class="param-count">{{ { Weather: 31, Solar: 9, Soil: 11 }[g] }} hourly vars</span>
        </label>
      </div>
      <div class="weight-display">API call weight: <strong>{{ Math.ceil(config.parameters.groups.reduce((s, g) => s + ({ Weather: 31, Solar: 9, Soil: 11 }[g] ?? 0), 0) / 10) * Math.ceil(config.forecastDays / 14) }}</strong></div>
    </div>

    <!-- Enrichment -->
    <div class="section">
      <h3>Enrichment</h3>
      <div class="enrichment-grid">
        <div v-for="f in enrichmentFeatures" :key="f.name" class="enrichment-card" :class="{ selected: isEnrichmentEnabled(f.name) }">
          <label class="enrichment-header">
            <input type="checkbox" :checked="isEnrichmentEnabled(f.name)" @change="toggleEnrichment(f.name)" />
            <strong>{{ f.name }}</strong>
            <span v-if="f.enabledByDefault" class="enrichment-badge">default on</span>
          </label>
          <div v-if="isEnrichmentEnabled(f.name) && f.options.length" class="enrichment-options">
            <label v-for="opt in f.options" :key="opt.name" class="enrichment-option">
              <span class="option-name">{{ opt.name }}</span>
              <input
                v-if="opt.type === 'number'"
                type="number"
                step="any"
                :placeholder="String(opt.default)"
                :value="getEnrichmentOption(f.name, opt.name, opt.default)"
                @input="setEnrichmentOption(f.name, opt.name, ($event.target as HTMLInputElement).value ? Number(($event.target as HTMLInputElement).value) : undefined, opt.default)"
                class="option-input"
              />
              <input
                v-else
                type="text"
                :placeholder="String(opt.default)"
                :value="getEnrichmentOption(f.name, opt.name, opt.default)"
                @input="setEnrichmentOption(f.name, opt.name, ($event.target as HTMLInputElement).value || undefined, opt.default)"
                class="option-input"
              />
            </label>
          </div>
        </div>
      </div>
    </div>

    <!-- MQTT -->
    <div class="section">
      <h3>MQTT</h3>
      <div class="general-row">
        <label>Host <input v-model="config.mqtt.host" placeholder="192.168.1.x" /></label>
        <label>Port <input v-model.number="config.mqtt.port" type="number" /></label>
        <label>Username <input v-model="config.mqtt.username" placeholder="optional" /></label>
        <label>Password <input v-model="config.mqtt.password" type="password" placeholder="optional" /></label>
      </div>
    </div>

    <!-- Persistence -->
    <div class="section">
      <h3>Persistence</h3>
      <div class="persistence-row">
        <label class="persistence-option" :class="{ selected: config.persistence.provider === 'Sqlite' }">
          <input type="radio" value="Sqlite" v-model="config.persistence.provider" /> SQLite <span class="enrichment-badge">default</span>
        </label>
        <label class="persistence-option" :class="{ selected: config.persistence.provider === 'PostgreSql' }">
          <input type="radio" value="PostgreSql" v-model="config.persistence.provider" /> PostgreSQL
        </label>
      </div>
      <div v-if="config.persistence.provider === 'PostgreSql'" class="persistence-connstr">
        <label>Connection String <input v-model="config.persistence.connectionString" placeholder="Host=localhost;Database=njord;..." class="input-wide" /></label>
      </div>
    </div>

    <!-- General -->
    <div class="section">
      <h3>General</h3>
      <div class="general-row">
        <label>Poll Interval <input v-model="config.pollInterval" placeholder="01:00:00" /></label>
        <label>Forecast Days <input v-model.number="config.forecastDays" type="number" min="1" max="16" /></label>
        <label>Discovery Interval <input v-model="config.discoveryInterval" placeholder="00:20:00" /></label>
      </div>
    </div>

    <!-- Budget -->
    <div class="section budget-section">
      <h3>Budget</h3>
      <div class="budget-bar-container">
        <div class="budget-bar" :style="{ width: Math.min(budget.percentUsed, 100) + '%' }" :class="{ over: budget.overBudget }" />
      </div>
      <div class="budget-stats">
        <span>{{ budget.projected.toLocaleString() }} / {{ budget.limit.toLocaleString() }} req/month</span>
        <span :class="{ over: budget.overBudget }">{{ budget.percentUsed.toFixed(1) }}%</span>
      </div>
      <div class="budget-detail">
        {{ budget.totalModelsPerCycle }} model-location pairs · {{ budget.cyclesPerMonth }} cycles/month · weight {{ budget.apiCallWeight }}
      </div>
      <p v-if="budget.overBudget" class="error">Exceeds 80% budget guard ({{ budget.guard.toLocaleString() }}). Reduce models, increase poll interval, or exclude parameters.</p>
    </div>

    <!-- Export -->
    <div class="section">
      <h3>Export</h3>
      <div class="export-tabs">
        <button :class="{ active: exportMode.format === 'json' }" @click="exportMode.format = 'json'">appsettings.json</button>
        <button :class="{ active: exportMode.format === 'env' }" @click="exportMode.format = 'env'">Environment Variables</button>
        <button :class="{ active: exportMode.format === 'compose' }" @click="exportMode.format = 'compose'">docker-compose.yml</button>
        <button @click="copyExport" class="btn-copy">{{ copied.show ? 'Copied!' : 'Copy' }}</button>
      </div>
      <pre class="export-output">{{ exportOutput }}</pre>
    </div>

  </div>
</template>

<style scoped>
.builder { max-width: 800px; }
.section { margin-bottom: 24px; padding: 16px; border: 1px solid var(--vp-c-divider); border-radius: 8px; }
.section h3 { margin: 0 0 12px; font-size: 16px; }

.location-card { padding: 8px; margin-bottom: 8px; background: var(--vp-c-bg-soft); border-radius: 6px; }
.location-row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.input-name { width: 140px; }
.input-coord { width: 90px; }
.location-models { margin-top: 6px; display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
.location-models .label { font-size: 12px; color: var(--vp-c-text-2); margin-right: 4px; }

input, textarea { padding: 4px 8px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg); color: var(--vp-c-text-1); font-size: 13px; }
textarea { width: 100%; font-family: var(--vp-font-family-mono); }
button { padding: 4px 12px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-1); cursor: pointer; font-size: 13px; }
button:hover { background: var(--vp-c-bg-mute); }
button.active { background: var(--vp-c-brand-1); color: white; border-color: var(--vp-c-brand-1); }
.geo-input-row .btn-add { margin-top: 0; }
.btn-remove { color: var(--vp-c-danger-1); border-color: var(--vp-c-danger-1); }
.btn-copy { margin-left: auto; }

.model-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 8px; }
.model-card { display: flex; gap: 8px; padding: 8px; border: 1px solid var(--vp-c-divider); border-radius: 6px; cursor: pointer; font-size: 13px; }
.model-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.model-meta { display: block; font-size: 11px; color: var(--vp-c-text-3); }
.model-cap { display: block; font-size: 11px; color: var(--vp-c-warning-1); }
.model-chip { font-size: 12px; padding: 2px 6px; border: 1px solid var(--vp-c-divider); border-radius: 4px; cursor: pointer; display: inline-flex; align-items: center; gap: 2px; }
.model-chip.warn { border-color: var(--vp-c-warning-1); }
.warn-icon { color: var(--vp-c-warning-1); }

.enrichment-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 8px; }
.enrichment-card { padding: 10px; border: 1px solid var(--vp-c-divider); border-radius: 6px; }
.enrichment-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.enrichment-header { display: flex; gap: 6px; align-items: center; cursor: pointer; font-size: 13px; }
.enrichment-badge { font-size: 10px; color: var(--vp-c-text-3); background: var(--vp-c-bg-mute); padding: 1px 6px; border-radius: 3px; }
.enrichment-options { margin-top: 8px; display: flex; flex-direction: column; gap: 4px; }
.enrichment-option { display: flex; justify-content: space-between; align-items: center; font-size: 12px; color: var(--vp-c-text-2); }
.option-name { min-width: 100px; }
.option-input { width: 100px; font-size: 12px; }

.preset-row { display: flex; gap: 4px; margin-bottom: 8px; }
.custom-horizons { display: flex; gap: 8px; margin-bottom: 8px; }
.custom-horizons input { flex: 1; }
.horizons-display { display: block; font-size: 13px; color: var(--vp-c-text-2); }

.param-row { display: flex; gap: 8px; flex-wrap: wrap; }
.param-card { padding: 8px 12px; border: 1px solid var(--vp-c-divider); border-radius: 6px; cursor: pointer; display: flex; align-items: center; gap: 6px; }
.param-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.param-count { font-size: 11px; color: var(--vp-c-text-3); }
.weight-display { margin-top: 8px; font-size: 13px; color: var(--vp-c-text-2); }

.general-row { display: flex; gap: 12px; flex-wrap: wrap; }
.general-row label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; color: var(--vp-c-text-2); }
.general-row input { width: 150px; }

.persistence-row { display: flex; gap: 8px; margin-bottom: 8px; }
.persistence-option { display: flex; gap: 6px; align-items: center; padding: 6px 12px; border: 1px solid var(--vp-c-divider); border-radius: 6px; cursor: pointer; font-size: 13px; }
.persistence-option.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.persistence-connstr { margin-top: 4px; }
.persistence-connstr label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; color: var(--vp-c-text-2); }
.input-wide { width: 100%; }
.budget-section { }
.budget-bar-container { height: 8px; background: var(--vp-c-divider); border-radius: 4px; overflow: hidden; margin-bottom: 6px; }
.budget-bar { height: 100%; background: var(--vp-c-brand-1); border-radius: 4px; transition: width 0.3s; }
.budget-bar.over { background: var(--vp-c-danger-1); }
.budget-stats { display: flex; justify-content: space-between; font-size: 13px; font-variant-numeric: tabular-nums; }
.budget-stats .over { color: var(--vp-c-danger-1); font-weight: 600; }
.budget-detail { font-size: 12px; color: var(--vp-c-text-3); margin-top: 4px; }

.export-tabs { display: flex; gap: 4px; margin-bottom: 8px; }
.export-output { padding: 12px; background: var(--vp-c-bg-soft); border: 1px solid var(--vp-c-divider); border-radius: 6px; font-size: 12px; overflow-x: auto; max-height: 400px; white-space: pre; }

.import-box { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
.import-box button { align-self: flex-start; }
.error { color: var(--vp-c-danger-1); font-size: 13px; margin-top: 4px; }

details summary { cursor: pointer; color: var(--vp-c-text-2); font-size: 14px; }

.geo-search { margin-top: 8px; position: relative; }
.geo-input-row { display: flex; gap: 8px; align-items: stretch; }
.geo-input { flex: 1; }
.geo-results { position: absolute; z-index: 10; top: 100%; left: 0; right: 0; margin-top: 4px; border: 1px solid var(--vp-c-divider); border-radius: 6px; background: var(--vp-c-bg); box-shadow: 0 4px 12px rgba(0,0,0,0.15); max-height: 240px; overflow-y: auto; }
.geo-result { padding: 8px 12px; cursor: pointer; display: flex; align-items: baseline; gap: 8px; border-bottom: 1px solid var(--vp-c-divider); }
.geo-result:last-child { border-bottom: none; }
.geo-result:hover { background: var(--vp-c-bg-soft); }
.geo-name { font-weight: 600; font-size: 13px; }
.geo-detail { font-size: 12px; color: var(--vp-c-text-2); }
.geo-coords { font-size: 11px; color: var(--vp-c-text-3); font-family: var(--vp-font-family-mono); margin-left: auto; }
.geo-loading { font-size: 12px; color: var(--vp-c-text-3); margin-top: 4px; }
</style>
