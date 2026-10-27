<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { defaultConfig, FEATURES, featureLabel, requiredDomains, userFeatureGroups, recommendedFeatures, entityCount, type NjordConfig, type FeatureEntry, type FeatureOption, type OptionValue } from './types'
import weatherModels from '../../../data/weather-models.json'
import { computeBudget, featureCostPerDay, budgetLevel } from './budget'
import { exportAsJson, exportAsEnvVars, exportAsCompose, autoImport, encodeHash, decodeHash, mergeWithDefaults } from './serializer'

const config = reactive<NjordConfig>(defaultConfig())

function toggleModel(id: string, on: boolean) {
  const models = config.weather.models.filter(m => m !== id)
  if (on) models.push(id)
  config.weather.models = models.length ? models : [id]
}

const featureGroups = userFeatureGroups()
const expandedFeature = ref<string | null>(null)

function isFeatureOn(key: string): boolean {
  return !!config.features[key]
}

function toggleFeature(key: string) {
  config.features[key] = !config.features[key]
}

function toggleInfo(key: string) {
  expandedFeature.value = expandedFeature.value === key ? null : key
}

function costLabel(key: string): string {
  const cost = featureCostPerDay(config, key)
  return cost > 0 ? `+${cost}/day` : 'free'
}

function applyRecommended() {
  Object.assign(config.features, recommendedFeatures())
}

const totalEntities = computed(() => entityCount(config.features, config.locations.length))

function getOption(key: string, option: FeatureOption): OptionValue {
  const value = config.featureOptions[key]?.[option.name]
  return value ?? option.default
}

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

function enabledCount(): number {
  return FEATURES.filter(f => config.features[f.key]).length
}

function outputBadge(output: string): string {
  const map: Record<string, string> = { Alert: 'alert', Score: 'score', Condition: 'condition', Trend: 'trend', Forecast: 'forecast' }
  return map[output] ?? output.toLowerCase()
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
      <p class="section-hint">Add each location you want to monitor. Use coordinates from Google Maps (right-click a spot).</p>
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
      <div class="features-header">
        <h3>Features</h3>
        <div class="features-meta">
          <span class="meta-pill">{{ enabledCount() }} / {{ FEATURES.length }} enabled</span>
          <span class="meta-pill">~{{ totalEntities }} entities</span>
          <button @click="applyRecommended" class="btn-recommended">Recommended</button>
        </div>
      </div>

      <div v-for="g in featureGroups" :key="g.key" class="feature-group">
        <h4>{{ g.icon }} {{ g.label }}</h4>
        <div class="domain-grid">
          <div v-for="f in g.features" :key="f.key" class="feature-card" :class="{ selected: isFeatureOn(f.key) }">
            <div class="feature-top">
              <label class="feature-toggle">
                <input type="checkbox" :checked="isFeatureOn(f.key)" @change="toggleFeature(f.key)" />
                <strong>{{ featureLabel(f.key) }}</strong>
              </label>
              <div class="feature-badges">
                <span class="output-badge" :class="outputBadge(f.output)">{{ f.output }}</span>
                <span v-if="f.enabledByDefault" class="enrichment-badge">default</span>
                <span class="cost-badge" :class="{ free: costLabel(f.key) === 'free' }">{{ costLabel(f.key) }}</span>
              </div>
            </div>
            <p class="feature-desc">{{ f.description }}</p>
            <button v-if="f.longDescription || (f.options?.length && isFeatureOn(f.key))" class="btn-info" @click.prevent="toggleInfo(f.key)">{{ expandedFeature === f.key ? 'Less' : f.options?.length && isFeatureOn(f.key) ? 'Settings' : 'More info' }}</button>
            <div v-if="expandedFeature === f.key" class="feature-detail">
              <p v-if="f.longDescription">{{ f.longDescription }}</p>
              <div v-if="f.options?.length && isFeatureOn(f.key)" class="inline-options">
                <label v-for="opt in f.options" :key="opt.name" class="inline-option">
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
              <div v-if="f.entities?.length" class="entity-list">
                <span class="entity-label">HA entities:</span>
                <code v-for="e in f.entities" :key="e">{{ e }}</code>
              </div>
              <div class="domain-tags">
                <span v-for="d in f.domains" :key="d" class="domain-tag">{{ d }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      <p class="section-hint">Fetched domains: {{ requiredDomains(config.features).join(', ') || 'none' }}. Cost shows the additional API calls per day per location.</p>
    </div>

    <!-- Weather -->
    <div class="section">
      <h3>Weather</h3>
      <div class="general-row">
        <label>Forecast Days <input v-model.number="config.weather.forecastDays" type="number" min="1" max="16" /></label>
      </div>
      <details class="advanced">
        <summary>Ensemble comparison models</summary>
        <p class="section-hint">Select ensemble models for comparison. Only used when Model Comparison is enabled. The primary forecast uses the deterministic Forecast API (best_match) automatically.</p>
        <div v-for="m in weatherModels" :key="m.id" class="general-row">
          <label>
            <input type="checkbox" :checked="config.weather.models.includes(m.id)" @change="toggleModel(m.id, ($event.target as HTMLInputElement).checked)" />
            {{ m.displayName }} ({{ m.resolutionKm }} km, {{ m.forecastDays }} days)
          </label>
        </div>
      </details>
    </div>

    <!-- Solar yield -->
    <div v-if="config.features['Energy:SolarYield']" class="section">
      <h3>Solar yield</h3>
      <p class="section-hint">Configure your solar panel setup. The forecast uses these values to calculate expected energy output.</p>
      <div class="general-row">
        <label>Peak kW <input v-model.number="config.solarYield.peakKw" type="number" step="any" min="0.1" /></label>
        <label>Azimuth <input v-model.number="config.solarYield.azimuth" type="number" min="0" max="360" /></label>
        <label>Tilt <input v-model.number="config.solarYield.tilt" type="number" min="0" max="90" /></label>
      </div>
      <p class="section-hint">Defaults: 5.0 kW, azimuth 180 (south-facing), tilt 30 degrees.</p>
    </div>

    <!-- Per-domain forecast days -->
    <details class="section">
      <summary>Domain settings</summary>
      <div class="general-row">
        <label>Air quality days <input v-model.number="config.airQuality.forecastDays" type="number" min="1" max="7" /></label>
        <label>Region <select v-model="config.airQuality.region"><option value="Global">Global</option><option value="Europe">Europe</option><option value="UnitedStates">United States</option></select></label>
        <label>Marine days <input v-model.number="config.marine.forecastDays" type="number" min="1" max="8" /></label>
        <label>Flood days <input v-model.number="config.flood.forecastDays" type="number" min="1" max="210" /></label>
        <label><input v-model="config.flood.includeEnsemble" type="checkbox" /> Flood ensemble (50 members, raises cost)</label>
      </div>
      <p class="section-hint">Only used for domains an enabled feature requires. Defaults: air quality 5, marine 8, flood 92.</p>
    </details>

    <!-- Persistence -->
    <details class="section">
      <summary>Storage &amp; advanced</summary>
      <h4>Persistence</h4>
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

      <h4>Polling</h4>
      <div class="general-row">
        <label>Poll Interval <input v-model="config.pollInterval" placeholder="01:00:00" /></label>
        <label title="Interval while learning a domain's update cycle">Discovery Interval <input v-model="config.discoveryInterval" placeholder="00:20:00" /></label>
      </div>

      <h4>Container Resources</h4>
      <div class="general-row">
        <label>CPU Limit <input v-model="config.deploy.limits.cpus" placeholder="e.g. 1.0" /></label>
        <label>Memory Limit <input v-model="config.deploy.limits.memory" placeholder="e.g. 256M" /></label>
      </div>
      <p v-if="memoryTip" class="tip">{{ memoryTip }}</p>
    </details>

    <!-- Budget -->
    <div class="section budget-section" :class="'level-' + budgetLevel(budget.percentUsed)">
      <h3>API Budget</h3>
      <div class="budget-bar-container">
        <div class="budget-bar" :style="{ width: Math.min(budget.percentUsed, 100) + '%' }" :class="'level-' + budgetLevel(budget.percentUsed)" />
      </div>
      <div class="budget-stats">
        <span>{{ budget.projected.toLocaleString() }} / {{ budget.limit.toLocaleString() }} requests/month</span>
        <span :class="'level-' + budgetLevel(budget.percentUsed)">{{ budget.percentUsed.toFixed(1) }}%</span>
      </div>
      <div class="budget-detail">
        {{ config.locations.length || 1 }} location{{ config.locations.length !== 1 ? 's' : '' }} &middot;
        {{ requiredDomains(config.features).length }} domains &middot;
        {{ budget.cyclesPerDay }} polls/day &middot;
        {{ budget.callsPerDay.toLocaleString() }} calls/day
      </div>
      <p v-if="budget.overBudget" class="error">Exceeds 80% budget guard ({{ budget.guard.toLocaleString() }}). Disable features, reduce locations or increase the poll interval.</p>
      <p v-else class="section-hint">Free tier: 300,000 requests/month. njord stays well within this for typical setups.</p>
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
.builder { max-width: 860px; }
.section { margin-bottom: 24px; padding: 16px; border: 1px solid var(--vp-c-divider); border-radius: 8px; }
.section h3 { margin: 0 0 12px; font-size: 16px; }
.section h4 { margin: 16px 0 8px; font-size: 14px; color: var(--vp-c-text-2); }

.features-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; }
.features-header h3 { margin: 0; }
.features-meta { display: flex; gap: 8px; align-items: center; }
.meta-pill { font-size: 12px; padding: 2px 8px; border-radius: 10px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-2); }
.btn-recommended { padding: 4px 12px; font-size: 12px; border: 1px solid var(--vp-c-brand-1); border-radius: 4px; background: var(--vp-c-brand-soft); color: var(--vp-c-brand-1); cursor: pointer; font-weight: 500; }
.btn-recommended:hover { background: var(--vp-c-brand-1); color: white; }

.location-card { padding: 8px; margin-bottom: 8px; background: var(--vp-c-bg-soft); border-radius: 6px; }
.location-row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.location-name { flex: 1; min-width: 160px; }
.location-coord { width: 110px; }
.add-location { display: flex; gap: 8px; align-items: stretch; margin-top: 8px; }
.add-location input { flex: 1; }

.feature-group h4 { margin: 16px 0 8px; font-size: 14px; color: var(--vp-c-text-2); }
.domain-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 8px; }

.feature-card { padding: 10px; border: 1px solid var(--vp-c-divider); border-radius: 6px; font-size: 13px; }
.feature-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.feature-top { display: flex; justify-content: space-between; align-items: flex-start; gap: 6px; }
.feature-toggle { display: flex; gap: 6px; align-items: center; cursor: pointer; }
.feature-badges { display: flex; gap: 4px; flex-shrink: 0; }
.feature-desc { margin: 4px 0 2px; font-size: 12px; color: var(--vp-c-text-3); }
.btn-info { border: none; background: none; color: var(--vp-c-brand-1); cursor: pointer; font-size: 11px; padding: 0; text-decoration: underline; }
.feature-detail { margin-top: 8px; padding-top: 8px; border-top: 1px solid var(--vp-c-divider); }
.feature-detail p { font-size: 12px; color: var(--vp-c-text-2); margin: 0 0 6px; line-height: 1.5; }
.entity-list { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; margin-bottom: 6px; }
.entity-label { font-size: 11px; color: var(--vp-c-text-3); }
.entity-list code { font-size: 10px; padding: 1px 5px; border-radius: 3px; background: var(--vp-c-bg-mute); color: var(--vp-c-text-2); }
.domain-tags { display: flex; gap: 4px; }
.domain-tag { font-size: 10px; padding: 1px 6px; border-radius: 3px; background: var(--vp-c-bg-mute); color: var(--vp-c-text-3); }

.output-badge { font-size: 10px; padding: 1px 6px; border-radius: 3px; font-weight: 500; }
.output-badge.alert { background: #fef3cd; color: #856404; }
.output-badge.score { background: #d1ecf1; color: #0c5460; }
.output-badge.condition { background: #d4edda; color: #155724; }
.output-badge.trend { background: #e2d5f1; color: #4a235a; }
.output-badge.forecast { background: #cce5ff; color: #004085; }
@media (prefers-color-scheme: dark) {
  .output-badge.alert { background: #5a4b1e; color: #ffc107; }
  .output-badge.score { background: #1a3a42; color: #7dd3e0; }
  .output-badge.condition { background: #1a3a24; color: #6fcf97; }
  .output-badge.trend { background: #2d1a4a; color: #bb9fed; }
  .output-badge.forecast { background: #1a2d4a; color: #7db8f0; }
}
.enrichment-badge { font-size: 10px; color: var(--vp-c-text-3); background: var(--vp-c-bg-mute); padding: 1px 6px; border-radius: 3px; }
.cost-badge { font-size: 10px; padding: 1px 6px; border-radius: 3px; color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.cost-badge.free { color: var(--vp-c-text-3); background: var(--vp-c-bg-mute); }

input, textarea { padding: 4px 8px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg); color: var(--vp-c-text-1); font-size: 13px; }
textarea { width: 100%; font-family: var(--vp-font-family-mono); }
button { padding: 4px 12px; border: 1px solid var(--vp-c-divider); border-radius: 4px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-1); cursor: pointer; font-size: 13px; }
button:hover { background: var(--vp-c-bg-mute); }
button.active { background: var(--vp-c-brand-1); color: white; border-color: var(--vp-c-brand-1); }
.btn-remove { color: var(--vp-c-danger-1); border-color: var(--vp-c-danger-1); }
.btn-copy { margin-left: auto; }

.inline-options { margin-top: 8px; padding: 8px; background: var(--vp-c-bg-soft); border-radius: 4px; display: flex; flex-direction: column; gap: 4px; }
.inline-option { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; font-size: 12px; color: var(--vp-c-text-2); gap: 2px; }
.option-name { min-width: 100px; }
.option-input { width: 100px; font-size: 12px; }
.option-input-small { width: 64px; }
.array-inputs { display: flex; gap: 4px; }
.advanced { margin-top: 8px; }

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
