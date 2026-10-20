<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { defaultConfig, CATEGORIES, FEATURES, featureLabel, requiredDomains, type NjordConfig, type FeatureOption, type OptionValue } from './types'
import weatherModels from '../../../data/weather-models.json'
import { computeBudget, featureCostPerDay, budgetLevel } from './budget'
import { exportAsJson, exportAsEnvVars, exportAsCompose, autoImport, encodeHash, decodeHash, mergeWithDefaults } from './serializer'

const config = reactive<NjordConfig>(defaultConfig())

function toggleModel(id: string, on: boolean) {
  const models = config.weather.models.filter(m => m !== id)
  if (on) models.push(id)
  config.weather.models = models.length ? models : [id]
}

const featureGroups = CATEGORIES
  .map(c => ({ ...c, features: FEATURES.filter(f => f.category === c.key) }))
  .filter(g => g.features.length > 0)

function isFeatureOn(key: string): boolean {
  return !!config.features[key]
}

function toggleFeature(key: string) {
  config.features[key] = !config.features[key]
}

function costLabel(key: string): string {
  return `+${featureCostPerDay(config, key)} calls/day`
}

const featuresWithOptions = computed(() => FEATURES.filter(f => f.options?.length && isFeatureOn(f.key)))

function getOption(key: string, option: FeatureOption): OptionValue {
  const value = config.featureOptions[key]?.[option.name]
  return value ?? option.default
}

// A value equal to the default is dropped, so only real overrides reach the export.
function storeOption(key: string, option: FeatureOption, value: OptionValue | undefined) {
  const same = value !== undefined && JSON.stringify(value) === JSON.stringify(option.default)
  if (value === undefined || same) {
    if (config.featureOptions[key]) {
      delete config.featureOptions[key][option.name]
      if (!Object.keys(config.featureOptions[key]).length) delete config.featureOptions[key]
    }
    return
  }
  if (!config.featureOptions[key]) config.featureOptions[key] = {}
  config.featureOptions[key][option.name] = value
}

function getArrayOption(key: string, option: FeatureOption): number[] {
  return [...(getOption(key, option) as number[])]
}

function setArrayOption(key: string, option: FeatureOption, index: number, raw: string) {
  const parsed = Number(raw)
  if (raw.trim() === '' || Number.isNaN(parsed)) return
  const current = getArrayOption(key, option)
  current[index] = parsed
  storeOption(key, option, current)
}

function setNumberOption(key: string, option: FeatureOption, raw: string) {
  const parsed = Number(raw)
  storeOption(key, option, raw.trim() === '' || Number.isNaN(parsed) ? undefined : parsed)
}

const exportMode = reactive({ format: 'json' as 'json' | 'env' | 'compose' })
const importText = reactive({ value: '', error: '' })
const copied = reactive({ show: false })
const newLocation = ref('')
const newLatitude = ref<number | ''>('')
const newLongitude = ref<number | ''>('')
const canAddLocation = computed(() =>
  newLocation.value.trim() !== ''
  && typeof newLatitude.value === 'number' && newLatitude.value >= -90 && newLatitude.value <= 90
  && typeof newLongitude.value === 'number' && newLongitude.value >= -180 && newLongitude.value <= 180)

const budget = computed(() => computeBudget(config))
const memoryTip = computed(() => {
  if (requiredDomains(config.features).length * config.locations.length > 12) return 'With many domain calls per cycle, consider 256M.'
  return ''
})
const exportOutput = computed(() => {
  if (exportMode.format === 'json') return exportAsJson(config)
  if (exportMode.format === 'compose') return exportAsCompose(config)
  return exportAsEnvVars(config)
})

function addLocation() {
  if (!canAddLocation.value) return
  config.locations.push({
    name: newLocation.value.trim(),
    latitude: newLatitude.value as number,
    longitude: newLongitude.value as number,
  })
  newLocation.value = ''
  newLatitude.value = ''
  newLongitude.value = ''
}

function removeLocation(i: number) {
  config.locations.splice(i, 1)
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
    Object.assign(config, mergeWithDefaults(partial))
    importText.value = ''
  } catch (e) {
    importText.error = e instanceof Error ? e.message : 'Invalid input'
  }
}

watch(config, () => {
  if (typeof window !== 'undefined')
    window.location.hash = 'config=' + encodeHash(config)
}, { deep: true })

onMounted(() => {
  const hash = window.location.hash.replace('#config=', '')
  if (hash) {
    const restored = decodeHash(hash)
    if (restored) Object.assign(config, restored)
  }
})
</script>

<template>
  <div class="builder">

    <!-- Import -->
    <details class="section">
      <summary>Import existing config</summary>
      <div class="import-box">
        <textarea v-model="importText.value" placeholder="Paste appsettings.json, env vars, or docker-compose environment block here..." rows="4" />
        <button @click="doImport" :disabled="!importText.value.trim()">Import</button>
        <p v-if="importText.error" class="error">{{ importText.error }}</p>
      </div>
    </details>

    <!-- Locations -->
    <div class="section">
      <h3>Locations</h3>
      <div v-for="(loc, i) in config.locations" :key="i" class="location-card">
        <div class="location-row">
          <input v-model="loc.name" placeholder="Name" class="location-name" />
          <input v-model.number="loc.latitude" type="number" step="any" min="-90" max="90" placeholder="Latitude" class="location-coord" />
          <input v-model.number="loc.longitude" type="number" step="any" min="-180" max="180" placeholder="Longitude" class="location-coord" />
          <button @click="removeLocation(i)" class="btn-remove">Remove</button>
        </div>
      </div>
      <div class="add-location">
        <input v-model="newLocation" placeholder="Name, e.g. Lucerne" @keyup.enter="addLocation" />
        <input v-model.number="newLatitude" type="number" step="any" min="-90" max="90" placeholder="Latitude" class="location-coord" @keyup.enter="addLocation" />
        <input v-model.number="newLongitude" type="number" step="any" min="-180" max="180" placeholder="Longitude" class="location-coord" @keyup.enter="addLocation" />
        <button @click="addLocation" :disabled="!canAddLocation" class="btn-add">Add</button>
      </div>
    </div>

    <!-- Features -->
    <div class="section">
      <h3>Features</h3>
      <div v-for="g in featureGroups" :key="g.key" class="feature-group">
        <h4>{{ g.label }}</h4>
        <div class="domain-grid">
          <label v-for="f in g.features" :key="f.key" class="domain-card" :class="{ selected: isFeatureOn(f.key) }">
            <input type="checkbox" :checked="isFeatureOn(f.key)" @change="toggleFeature(f.key)" />
            <div>
              <strong>{{ featureLabel(f.key) }}</strong>
              <span v-if="f.enabledByDefault" class="enrichment-badge">default on</span>
              <span class="domain-meta">{{ f.description }}</span>
              <span class="domain-meta cost">{{ costLabel(f.key) }} per location ({{ f.domains.join(', ') }})</span>
            </div>
          </label>
        </div>
      </div>
      <p class="section-hint">The domains that are fetched follow from the enabled features: {{ requiredDomains(config.features).join(', ') || 'none' }}. The cost shows the calls per day and location the feature adds on top of the other enabled features; 0 means everything it needs is already fetched.</p>
    </div>

    <!-- Weather -->
    <div class="section">
      <h3>Weather</h3>
      <div class="general-row">
        <label>Forecast Days <input v-model.number="config.weather.forecastDays" type="number" min="1" max="16" /></label>
      </div>
      <details class="advanced">
        <summary>Advanced</summary>
        <div v-for="m in weatherModels" :key="m.id" class="general-row">
          <label>
            <input type="checkbox" :checked="config.weather.models.includes(m.id)" @change="toggleModel(m.id, ($event.target as HTMLInputElement).checked)" />
            {{ m.displayName }} ({{ m.resolutionKm }} km, {{ m.forecastDays }} days)
          </label>
        </div>
        <p class="section-hint">The first selected model is the primary one. More than one only takes effect with Weather:ModelComparison enabled, and multiplies the weather API weight by the model count. Only written to the config when it differs from the default.</p>
      </details>
      <div class="weight-display">Weather API call weight: <strong>{{ budget.apiCallWeight }}</strong></div>
      <p class="section-hint">The hourly variables follow from the enabled features; Open-Meteo counts ceil(variables / 10) calls, doubled beyond 14 forecast days.</p>
    </div>

    <!-- Solar yield -->
    <div v-if="config.features['Energy:SolarYield']" class="section">
      <h3>Solar yield</h3>
      <div class="general-row">
        <label>Peak kW <input v-model.number="config.solarYield.peakKw" type="number" step="any" min="0.1" /></label>
        <label>Azimuth <input v-model.number="config.solarYield.azimuth" type="number" min="0" max="360" /></label>
        <label>Tilt <input v-model.number="config.solarYield.tilt" type="number" min="0" max="90" /></label>
      </div>
      <p class="section-hint">Defaults: 5.0 kW, azimuth 180 (south), tilt 30. Peak power must be above 0, azimuth 0..360, tilt 0..90.</p>
    </div>

    <!-- Per-domain forecast days -->
    <div class="section">
      <h3>Forecast days per domain</h3>
      <div class="general-row">
        <label>Air quality <input v-model.number="config.airQuality.forecastDays" type="number" min="1" max="7" /></label>
        <label>Region <select v-model="config.airQuality.region"><option value="Global">Global</option><option value="Europe">Europe</option><option value="UnitedStates">United States</option></select></label>
        <label>Marine <input v-model.number="config.marine.forecastDays" type="number" min="1" max="8" /></label>
        <label>Flood <input v-model.number="config.flood.forecastDays" type="number" min="1" max="210" /></label>
        <label><input v-model="config.flood.includeEnsemble" type="checkbox" /> Flood ensemble (50 members, raises API cost)</label>
      </div>
      <p class="section-hint">Used only for domains an enabled feature requires. Defaults: air quality 5, marine 8, flood 92.</p>
    </div>

    <!-- Feature options -->
    <div v-if="featuresWithOptions.length" class="section">
      <h3>Feature settings</h3>
      <div class="enrichment-grid">
        <div v-for="f in featuresWithOptions" :key="f.key" class="enrichment-card selected">
          <div class="enrichment-header"><strong>{{ featureLabel(f.key) }}</strong></div>
          <div class="enrichment-options">
            <label v-for="opt in f.options" :key="opt.name" class="enrichment-option">
              <span class="option-name">{{ opt.name }}</span>
              <span v-if="opt.type === 'numberArray'" class="array-inputs">
                <input
                  v-for="(v, idx) in getArrayOption(f.key, opt)"
                  :key="idx"
                  type="number"
                  step="any"
                  :value="v"
                  @input="setArrayOption(f.key, opt, idx, ($event.target as HTMLInputElement).value)"
                  class="option-input option-input-small"
                />
              </span>
              <input
                v-else
                type="number"
                step="any"
                :placeholder="String(opt.default)"
                :value="getOption(f.key, opt)"
                @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)"
                class="option-input"
              />
            </label>
          </div>
        </div>
      </div>
      <p class="section-hint">Only values that differ from the default are written to the config. Threshold lists are three severity steps.</p>
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
        <label title="Poll interval for a model whose update cycle is not learned yet">Discovery Poll Interval <input v-model="config.discoveryInterval" placeholder="00:20:00" /></label>
      </div>
    </div>

    <!-- Deploy Limits -->
    <div class="section">
      <h3>Container Resources</h3>
      <p class="section-hint">Limits CPU and memory for the Docker container. Prevents runaway resource usage on low-power hosts.</p>
      <div class="general-row">
        <label>CPU Limit <input v-model="config.deploy.limits.cpus" placeholder="e.g. 1.0" /></label>
        <label>Memory Limit <input v-model="config.deploy.limits.memory" placeholder="e.g. 256M" /></label>
      </div>
      <p v-if="memoryTip" class="tip">{{ memoryTip }}</p>
    </div>

    <!-- Budget -->
    <div class="section budget-section" :class="'level-' + budgetLevel(budget.percentUsed)">
      <h3>Budget</h3>
      <div class="budget-bar-container">
        <div class="budget-bar" :style="{ width: Math.min(budget.percentUsed, 100) + '%' }" :class="'level-' + budgetLevel(budget.percentUsed)" />
      </div>
      <div class="budget-stats">
        <span>{{ budget.projected.toLocaleString() }} / {{ budget.limit.toLocaleString() }} req/month</span>
        <span :class="'level-' + budgetLevel(budget.percentUsed)">{{ budget.percentUsed.toFixed(1) }}%</span>
      </div>
      <div class="budget-detail">
        {{ requiredDomains(config.features).length }} domains ({{ requiredDomains(config.features).join(', ') }}) × {{ config.locations.length }} locations = {{ budget.domainCallsPerCycle }} calls/cycle · {{ budget.cyclesPerDay }} cycles/day · weight {{ budget.apiCallWeight }} = {{ budget.callsPerDay.toLocaleString() }} req/day · {{ budget.cyclesPerMonth }} cycles/month
      </div>
      <p v-if="budget.overBudget" class="error">Exceeds 80% budget guard ({{ budget.guard.toLocaleString() }}). Disable features, reduce locations or increase the poll interval.</p>
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
.location-name { flex: 1; min-width: 160px; }
.location-coord { width: 110px; }
.add-location { display: flex; gap: 8px; align-items: stretch; margin-top: 8px; }
.add-location input { flex: 1; }

.domain-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 8px; }
.domain-card { display: flex; gap: 8px; padding: 8px; border: 1px solid var(--vp-c-divider); border-radius: 6px; cursor: pointer; font-size: 13px; }
.domain-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.domain-card.locked { cursor: default; }
.domain-meta { display: block; font-size: 11px; color: var(--vp-c-text-3); }

input, textarea { padding: 4px 8px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg); color: var(--vp-c-text-1); font-size: 13px; }
textarea { width: 100%; font-family: var(--vp-font-family-mono); }
button { padding: 4px 12px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-1); cursor: pointer; font-size: 13px; }
button:hover { background: var(--vp-c-bg-mute); }
button.active { background: var(--vp-c-brand-1); color: white; border-color: var(--vp-c-brand-1); }
.btn-remove { color: var(--vp-c-danger-1); border-color: var(--vp-c-danger-1); }
.btn-copy { margin-left: auto; }


.enrichment-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 8px; }
.enrichment-card { padding: 10px; border: 1px solid var(--vp-c-divider); border-radius: 6px; }
.enrichment-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.enrichment-header { display: flex; gap: 6px; align-items: center; cursor: pointer; font-size: 13px; }
.enrichment-badge { font-size: 10px; color: var(--vp-c-text-3); background: var(--vp-c-bg-mute); padding: 1px 6px; border-radius: 3px; }
.enrichment-options { margin-top: 8px; display: flex; flex-direction: column; gap: 4px; }
.enrichment-option { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; font-size: 12px; color: var(--vp-c-text-2); gap: 2px; }
.option-name { min-width: 100px; }
.option-input { width: 100px; font-size: 12px; }
.option-input-small { width: 64px; }
.array-inputs { display: flex; gap: 4px; }
.input-wide-model { width: 260px; }
.advanced { margin-top: 8px; }
.option-description { width: 100%; font-size: 10px; color: var(--vp-c-text-3); margin-top: -2px; }


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
.budget-bar.level-warn { background: var(--vp-c-warning-1); }
.budget-bar.level-over { background: var(--vp-c-danger-1); }
.budget-stats { display: flex; justify-content: space-between; font-size: 13px; font-variant-numeric: tabular-nums; }
.budget-stats .level-warn { color: var(--vp-c-warning-1); font-weight: 600; }
.budget-stats .level-over { color: var(--vp-c-danger-1); font-weight: 600; }
.feature-group h4 { margin: 12px 0 6px; font-size: 14px; color: var(--vp-c-text-2); }
.cost { color: var(--vp-c-brand-1); }
.budget-detail { font-size: 12px; color: var(--vp-c-text-3); margin-top: 4px; }

.export-tabs { display: flex; gap: 4px; margin-bottom: 8px; }
.export-output { padding: 12px; background: var(--vp-c-bg-soft); border: 1px solid var(--vp-c-divider); border-radius: 6px; font-size: 12px; overflow-x: auto; max-height: 400px; white-space: pre; }

.import-box { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
.import-box button { align-self: flex-start; }
.error { color: var(--vp-c-danger-1); font-size: 13px; margin-top: 4px; }
.tip { color: var(--vp-c-warning-1); font-size: 12px; margin-top: 6px; }
.section-hint { font-size: 12px; color: var(--vp-c-text-3); margin: -4px 0 8px; }

details summary { cursor: pointer; color: var(--vp-c-text-2); font-size: 14px; }

</style>
