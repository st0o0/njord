<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { defaultConfig, ENRICHMENT_TOGGLES, requiredDomains, type NjordConfig, type EnrichmentFeature } from './types'
import { computeBudget, variableCount } from './budget'
import { exportAsJson, exportAsEnvVars, exportAsCompose, autoImport, findOutdatedKeys, encodeHash, decodeHash } from './serializer'
import enrichmentData from '../../../data/enrichment.json'

const enrichmentFeatures = enrichmentData as EnrichmentFeature[]
const config = reactive<NjordConfig>(defaultConfig())

function isEnrichmentToggled(key: string): boolean {
  return !!config.enrichments[key]
}

function toggleEnrichmentKey(key: string) {
  config.enrichments[key] = !config.enrichments[key]
}

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

const exportMode = reactive({ format: 'json' as 'json' | 'env' | 'compose' })
const importText = reactive({ value: '', error: '', outdated: [] as string[] })
const copied = reactive({ show: false })
const newLocation = ref('')

const budget = computed(() => computeBudget(config))
const memoryTip = computed(() => {
  if (requiredDomains(config.enrichments).length * config.locations.length > 12) return 'With many domain calls per cycle, consider 256M.'
  return ''
})
const exportOutput = computed(() => {
  if (exportMode.format === 'json') return exportAsJson(config)
  if (exportMode.format === 'compose') return exportAsCompose(config)
  return exportAsEnvVars(config)
})

function addLocation() {
  const name = newLocation.value.trim()
  if (!name) return
  config.locations.push({ name })
  newLocation.value = ''
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
  importText.outdated = []
  try {
    importText.outdated = findOutdatedKeys(importText.value)
    const partial = autoImport(importText.value)
    Object.assign(config, { ...defaultConfig(), ...partial })
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
        <textarea v-model="importText.value" placeholder="Paste appsettings.json, env vars, or docker-compose environment block here..." rows="4" />
        <button @click="doImport" :disabled="!importText.value.trim()">Import</button>
        <p v-if="importText.error" class="error">{{ importText.error }}</p>
        <p v-for="key in importText.outdated" :key="key" class="error">{{ key }}</p>
      </div>
    </details>

    <!-- Locations -->
    <div class="section">
      <h3>Locations</h3>
      <div v-for="(loc, i) in config.locations" :key="i" class="location-card">
        <div class="location-row">
          <input v-model="loc.name" placeholder="Name" class="location-name" />
          <span class="location-hint">coordinates resolved by geocoding</span>
          <button @click="removeLocation(i)" class="btn-remove">Remove</button>
        </div>
      </div>
      <div class="add-location">
        <input v-model="newLocation" placeholder="City name, e.g. Lucerne" @keyup.enter="addLocation" />
        <button @click="addLocation" :disabled="!newLocation.trim()" class="btn-add">Add</button>
      </div>
    </div>

    <!-- Enrichment toggles -->
    <div class="section">
      <h3>Enrichments</h3>
      <div class="domain-grid">
        <label v-for="t in ENRICHMENT_TOGGLES" :key="t.key" class="domain-card" :class="{ selected: isEnrichmentToggled(t.key) }">
          <input type="checkbox" :checked="isEnrichmentToggled(t.key)" @change="toggleEnrichmentKey(t.key)" />
          <div>
            <strong>{{ t.label }}</strong>
            <span v-if="t.enabledByDefault" class="enrichment-badge">default on</span>
            <span class="domain-meta">{{ t.description }} (fetches {{ t.domains.join(', ') }})</span>
          </div>
        </label>
      </div>
      <p class="section-hint">The domains that are fetched follow from the enabled enrichments: {{ requiredDomains(config.enrichments).join(', ') || 'none' }}. Each domain costs one request per location and poll cycle.</p>
    </div>

    <!-- Weather -->
    <div class="section">
      <h3>Weather</h3>
      <label class="mqtt-enabled">
        <input type="checkbox" v-model="config.weather.includeSolar" />
        <strong>Include solar variables</strong>
        <span class="enrichment-badge">disabled by default</span>
      </label>
      <div class="general-row">
        <label>Forecast Days <input v-model.number="config.weather.forecastDays" type="number" min="1" max="16" /></label>
      </div>
      <div class="weight-display">{{ variableCount(config.weather.includeSolar) }} hourly vars → API call weight: <strong>{{ budget.apiCallWeight }}</strong></div>
      <p class="section-hint">Solar adds shortwave, direct, diffuse and direct normal radiation plus sunshine duration.</p>
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
            <label v-for="opt in f.options" :key="opt.name" class="enrichment-option" :title="opt.description || ''">
              <span class="option-name">{{ opt.name.includes(':') ? opt.name.split(':')[1] : opt.name }}</span>
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
              <span v-if="opt.description" class="option-description">{{ opt.description }}</span>
            </label>
          </div>
        </div>
      </div>
    </div>

    <!-- MQTT -->
    <div class="section">
      <h3>MQTT</h3>
      <label class="mqtt-enabled">
        <input type="checkbox" v-model="config.mqtt.enabled" />
        <strong>Enabled</strong>
        <span class="enrichment-badge">disabled by default</span>
      </label>
      <div v-if="config.mqtt.enabled" class="general-row mqtt-fields">
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
        {{ requiredDomains(config.enrichments).length }} domains ({{ requiredDomains(config.enrichments).join(', ') }}) × {{ config.locations.length }} locations = {{ budget.domainCallsPerCycle }} calls/cycle · {{ budget.cyclesPerDay }} cycles/day · weight {{ budget.apiCallWeight }} = {{ budget.callsPerDay.toLocaleString() }} req/day · {{ budget.cyclesPerMonth }} cycles/month
      </div>
      <p v-if="budget.overBudget" class="error">Exceeds 80% budget guard ({{ budget.guard.toLocaleString() }}). Disable enrichments, reduce locations, increase poll interval, or enable fewer enrichments.</p>
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
.location-hint { font-size: 11px; color: var(--vp-c-text-3); margin-left: auto; }
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
.option-description { width: 100%; font-size: 10px; color: var(--vp-c-text-3); margin-top: -2px; }


.weight-display { margin-top: 8px; font-size: 13px; color: var(--vp-c-text-2); }

.general-row { display: flex; gap: 12px; flex-wrap: wrap; }
.general-row label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; color: var(--vp-c-text-2); }
.general-row input { width: 150px; }

.mqtt-enabled { display: flex; gap: 6px; align-items: center; cursor: pointer; font-size: 13px; margin-bottom: 8px; }
.mqtt-fields { margin-top: 4px; }
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
.tip { color: var(--vp-c-warning-1); font-size: 12px; margin-top: 6px; }
.section-hint { font-size: 12px; color: var(--vp-c-text-3); margin: -4px 0 8px; }

details summary { cursor: pointer; color: var(--vp-c-text-2); font-size: 14px; }

</style>
