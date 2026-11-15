<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { defaultConfig, FEATURES, featureLabel, requiredDomains, userFeatureGroups, recommendedFeatures, entityCount, type NjordConfig, type FeatureEntry, type FeatureOption, type OptionValue } from './types'
import weatherModels from '../../../data/weather-models.json'
import { computeBudget, featureCostPerDay, budgetLevel } from './budget'
import { exportAsJson, exportAsEnvVars, exportAsCompose, autoImport, encodeHash, decodeHash, mergeWithDefaults } from './serializer'

const config = reactive<NjordConfig>(defaultConfig())

function toggleModel(id: string, on: boolean) {
  const models = config.ensemble.models.filter(m => m !== id)
  if (on) models.push(id)
  config.ensemble.models = models.length ? models : [id]
}

const featureGroups = userFeatureGroups()
const expandedFeature = ref<string | null>(null)
const collapsedGroups = reactive<Record<string, boolean>>({})

function isGroupOpen(key: string): boolean {
  return collapsedGroups[key] !== true
}

function toggleGroup(key: string) {
  collapsedGroups[key] = !collapsedGroups[key]
}

function subGroups(features: FeatureEntry[]): { output: string; features: FeatureEntry[] }[] {
  const order = ['Alert', 'Score', 'Condition', 'Observation', 'Trend', 'Forecast', 'Weather']
  const map = new Map<string, FeatureEntry[]>()
  for (const f of features) {
    const list = map.get(f.output) ?? []
    list.push(f)
    map.set(f.output, list)
  }
  return order.filter(o => map.has(o)).map(o => ({ output: o, features: map.get(o)! }))
}

function needsSubGroups(features: FeatureEntry[]): boolean {
  const outputs = new Set(features.map(f => f.output))
  return outputs.size > 1
}

interface GeoResult { name: string; latitude: number; longitude: number; country: string; admin1?: string }
const geoQuery = ref('')
const geoResults = ref<GeoResult[]>([])
const geoLoading = ref(false)
const geoOpen = ref(false)
let geoTimer: ReturnType<typeof setTimeout> | null = null

function onGeoInput(value: string) {
  geoQuery.value = value
  if (geoTimer) clearTimeout(geoTimer)
  if (value.trim().length < 2) { geoResults.value = []; geoOpen.value = false; return }
  geoLoading.value = true
  geoTimer = setTimeout(async () => {
    try {
      const res = await fetch(`https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(value.trim())}&count=6&language=en&format=json`)
      const data = await res.json()
      geoResults.value = (data.results ?? []).map((r: any) => ({ name: r.name, latitude: r.latitude, longitude: r.longitude, country: r.country ?? '', admin1: r.admin1 }))
      geoOpen.value = geoResults.value.length > 0
    } catch { geoResults.value = []; geoOpen.value = false }
    geoLoading.value = false
  }, 300)
}

function pickGeo(r: GeoResult) {
  const lat = Math.round(r.latitude * 100) / 100
  const lon = Math.round(r.longitude * 100) / 100
  config.locations.push({ name: r.name, latitude: lat, longitude: lon })
  geoQuery.value = ''
  geoResults.value = []
  geoOpen.value = false
  onLocationAdded(lat, lon)
}

function closeGeo() { setTimeout(() => { geoOpen.value = false }, 200) }

function isFeatureOn(key: string): boolean {
  return !!config.features[key]
}

function toggleFeature(key: string) {
  config.features[key] = !config.features[key]
  activeTier.value = 'custom'
}

function toggleInfo(key: string) {
  expandedFeature.value = expandedFeature.value === key ? null : key
}

function costLabel(key: string): string {
  const cost = featureCostPerDay(config, key)
  return cost > 0 ? `+${cost}/day` : 'free'
}

// ── Tiers ──

type Tier = 'essential' | 'extended' | 'full' | 'custom'
const activeTier = ref<Tier>('essential')
const showCustomize = ref(false)
const featureSearch = ref('')

const EXTENDED_KEYS = new Set([
  ...Object.entries(recommendedFeatures()).filter(([, v]) => v).map(([k]) => k),
  'WetBulbDanger', 'CompoundHeatStress', 'MigraineRisk', 'Shower', 'ClearSkyUv', 'FireRisk',
  'Dust', 'SmogRisk', 'PollenBlast', 'AllergySeverity', 'AsthmaRisk', 'PollenTrend', 'GranularAqi', 'RagweedPollen',
  'SleepQuality', 'Bbq', 'Laundry', 'Outdoor', 'SprayWindow', 'Gardening', 'NightVentilation', 'VentilationWindow', 'ExerciseAdvisory',
  'ConfidenceTrend', 'ApproachingFront',
  'DewPoint', 'UvIndex', 'WindGusts', 'EuropeanAqi',
])

function tierFeatures(tier: Tier): Record<string, boolean> {
  if (tier === 'full') return Object.fromEntries(FEATURES.map(f => [f.key, true]))
  if (tier === 'extended') return Object.fromEntries(FEATURES.map(f => [f.key, EXTENDED_KEYS.has(f.key)]))
  return recommendedFeatures()
}

function applyTier(tier: Tier) {
  activeTier.value = tier
  Object.assign(config.features, tierFeatures(tier))
}

function applyRecommended() {
  applyTier('essential')
}

const tierInfo = computed(() => {
  const tiers: { key: Tier; label: string; desc: string; count: number; domains: number }[] = [
    { key: 'essential', label: 'Essential', desc: 'Weather safety for everyday life', count: 0, domains: 0 },
    { key: 'extended', label: 'Extended', desc: 'Outdoor, health and air quality', count: 0, domains: 0 },
    { key: 'full', label: 'Full', desc: 'Maximum coverage with all domains', count: 0, domains: 0 },
  ]
  for (const t of tiers) {
    const feats = tierFeatures(t.key)
    t.count = Object.values(feats).filter(Boolean).length
    t.domains = requiredDomains(feats).length
  }
  return tiers
})

const filteredGroups = computed(() => {
  const q = featureSearch.value.trim().toLowerCase()
  if (!q) return featureGroups
  return featureGroups.map(g => ({
    ...g,
    features: g.features.filter(f =>
      f.key.toLowerCase().includes(q) || f.name.toLowerCase().includes(q)
      || f.description.toLowerCase().includes(q) || f.output.toLowerCase().includes(q)),
  })).filter(g => g.features.length > 0)
})

// ── Location-aware recommendations ──

interface LocationProfile { climate: 'cold' | 'temperate' | 'hot'; coastal: boolean; floodRisk: boolean }
const locationSuggestion = ref<{ profile: LocationProfile; extras: string[] } | null>(null)
const suggestionDismissed = ref(false)

function deriveProfile(lat: number): LocationProfile {
  const absLat = Math.abs(lat)
  const climate = absLat > 55 ? 'cold' : absLat > 35 ? 'temperate' : absLat > 23 ? 'hot' : 'temperate'
  return { climate, coastal: false, floodRisk: false }
}

function suggestExtras(profile: LocationProfile): string[] {
  const extras: string[] = []
  if (profile.climate === 'cold') extras.push('FrostDepth', 'SnowDepthWatch')
  if (profile.climate === 'hot') extras.push('WetBulbDanger', 'CompoundHeatStress', 'FireRisk')
  if (profile.coastal) extras.push('SwimmingConditions', 'SurfConditions', 'CoastalFlood')
  if (profile.floodRisk) extras.push('FloodRisk', 'FloodRain', 'FloodTimeline')
  return extras.filter(k => !config.features[k])
}

async function checkCoastal(lat: number, lon: number): Promise<boolean> {
  try {
    const res = await fetch(`https://marine-api.open-meteo.com/v1/marine?latitude=${lat}&longitude=${lon}&hourly=wave_height&forecast_days=1&timeformat=unixtime`)
    const data = await res.json()
    return !data.error && data.hourly?.wave_height?.some((v: number | null) => v !== null)
  } catch { return false }
}

async function onLocationAdded(lat: number, lon: number) {
  suggestionDismissed.value = false
  const profile = deriveProfile(lat)
  profile.coastal = await checkCoastal(lat, lon)
  const extras = suggestExtras(profile)
  if (extras.length > 0) locationSuggestion.value = { profile, extras }
  else locationSuggestion.value = null
}

function applySuggestion() {
  if (!locationSuggestion.value) return
  for (const k of locationSuggestion.value.extras) config.features[k] = true
  activeTier.value = 'custom'
  locationSuggestion.value = null
}

function dismissSuggestion() {
  locationSuggestion.value = null
  suggestionDismissed.value = true
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

const SEVERITY_LABELS = ['Low', 'Medium', 'High', 'Extreme']
const SEVERITY_COLORS = ['#e6a817', '#e67317', '#dc2626', '#7c1d7c']
const SEVERITY_ICONS = ['○', '◑', '●', '✖']

function optionUnit(featureKey: string, optName: string): string {
  const n = optName.toLowerCase()
  if (n.includes('hour') || n === 'windowhours' || n === 'persistenthours') return 'h'
  if (n.includes('threshold') || n === 'thresholds') {
    const f = FEATURES.find(f => f.key === featureKey)
    if (!f) return ''
    const p = (f.weatherParams ?? [])[0] ?? ''
    if (/temperature|wetbulb|dewpoint|tropicalnight/i.test(p) || /temperature|frost|heat|windchill|tropicalnight/i.test(featureKey)) return '°C'
    if (/wind|gust/i.test(p) || /storm|gust/i.test(featureKey)) return 'm/s'
    if (/precipitation|rain|snow/i.test(p) || /rain|snow|shower/i.test(featureKey)) return 'mm'
    if (/pressure/i.test(p) || /pressure/i.test(featureKey)) return 'hPa'
    if (/uv/i.test(featureKey)) return ''
    if (/visibility/i.test(featureKey)) return 'm'
    if (/humidity/i.test(featureKey)) return 'g/kg'
    if (/cape/i.test(n)) return 'J/kg'
  }
  return ''
}

function optionLabel(name: string): string {
  if (name === 'WindowHours') return 'Lookahead window'
  if (name === 'PersistentHours') return 'Must persist for'
  if (name === 'HourlyThreshold') return 'Hourly threshold'
  if (name === 'DailyThreshold') return 'Daily threshold'
  return name.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

function isThresholdArray(opt: FeatureOption): boolean {
  return opt.type === 'numberArray' && /threshold/i.test(opt.name)
}

function isWindowHours(opt: FeatureOption): boolean {
  return opt.name === 'WindowHours' || opt.name === 'PersistentHours'
}

function optionGroups(f: FeatureEntry): { group: string | null; options: FeatureOption[] }[] {
  if (!f.options?.length) return []
  const groups: { group: string | null; options: FeatureOption[] }[] = []
  const seen = new Set<string | null>()
  for (const opt of f.options) {
    const g = opt.group ?? null
    if (!seen.has(g)) {
      seen.add(g)
      groups.push({ group: g, options: f.options.filter(o => (o.group ?? null) === g) })
    }
  }
  return groups
}

function weightSum(key: string, options: FeatureOption[]): number | null {
  const weights = options.filter(o => o.group === 'Weights' && o.type === 'number')
  if (weights.length === 0) return null
  return weights.reduce((sum, w) => sum + (getOption(key, w) as number), 0)
}

function weightSumError(key: string, options: FeatureOption[]): string | null {
  const sum = weightSum(key, options)
  if (sum === null) return null
  return Math.abs(sum - 1.0) < 0.001 ? null : `Weights sum to ${sum.toFixed(3)}, must be 1.0`
}

const exportMode = reactive({ format: 'compose' as 'json' | 'env' | 'compose' })
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
  const lat = newLatitude.value as number
  const lon = newLongitude.value as number
  config.locations.push({ name: newLocation.value.trim(), latitude: lat, longitude: lon })
  newLocation.value = ''
  newLatitude.value = ''
  newLongitude.value = ''
  onLocationAdded(lat, lon)
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

    <!-- STEP 1: Where are you? -->
    <section class="card card-hero">
      <h3>Where are you?</h3>
      <p class="hint">Search for your city. njord monitors weather for each location you add.</p>

      <div v-for="(loc, i) in config.locations" :key="i" class="loc-chip">
        <span class="loc-chip-name">{{ loc.name }}</span>
        <span class="loc-chip-coords">{{ loc.latitude }}, {{ loc.longitude }}</span>
        <button @click="removeLocation(i)" class="loc-chip-x" :aria-label="'Remove ' + loc.name">&times;</button>
      </div>

      <div class="geo-search">
        <div class="geo-input-wrap">
          <input :value="geoQuery" @input="onGeoInput(($event.target as HTMLInputElement).value)" @blur="closeGeo" placeholder="Search for a city or place..." class="geo-input" role="combobox" aria-label="Search for a city or place" :aria-expanded="geoOpen" aria-autocomplete="list" />
          <span v-if="geoLoading" class="geo-spinner" />
        </div>
        <div v-if="geoOpen" class="geo-dropdown" role="listbox" aria-label="Location search results">
          <button v-for="r in geoResults" :key="r.name + r.latitude" class="geo-option" role="option" @mousedown.prevent="pickGeo(r)">
            <span class="geo-name">{{ r.name }}</span>
            <span class="geo-meta">{{ [r.admin1, r.country].filter(Boolean).join(', ') }}</span>
            <span class="geo-coords">{{ r.latitude.toFixed(2) }}, {{ r.longitude.toFixed(2) }}</span>
          </button>
        </div>
      </div>
    </section>

    <!-- Location suggestion -->
    <div v-if="locationSuggestion && !suggestionDismissed" class="suggestion-banner">
      <div class="suggestion-text">
        Based on your location, we suggest enabling
        <strong>{{ locationSuggestion.extras.map(k => featureLabel(k)).join(', ') }}</strong>.
      </div>
      <div class="suggestion-actions">
        <button @click="applySuggestion" class="btn-primary">Enable</button>
        <button @click="dismissSuggestion" class="btn-ghost">Dismiss</button>
      </div>
    </div>

    <!-- STEP 2: What do you need? -->
    <section class="card">
      <h3>What do you need?</h3>
      <p class="hint">Pick a preset. You can customize individual features later.</p>

      <div class="tier-row" role="radiogroup" aria-label="Feature preset">
        <button v-for="t in tierInfo" :key="t.key" role="radio" :aria-checked="String(activeTier === t.key)" class="tier-card" :class="{ active: activeTier === t.key }" @click="applyTier(t.key)">
          <span class="tier-label">{{ t.label }}</span>
          <span class="tier-count">{{ t.count }} features</span>
          <span class="tier-desc">{{ t.desc }}</span>
        </button>
      </div>

      <div class="tier-summary">
        <span>{{ enabledCount() }} features enabled</span>
        <span class="tier-summary-sep">&middot;</span>
        <span>~{{ totalEntities }} HA entities{{ config.locations.length > 1 ? ' total' : '' }}</span>
        <span class="tier-summary-sep">&middot;</span>
        <span :class="'level-' + budgetLevel(budget.percentUsed)">{{ budget.percentUsed.toFixed(1) }}% of free tier</span>
      </div>
    </section>

    <!-- STEP 3: Your config -->
    <section class="card">
      <h3>Your config</h3>
      <p class="hint">Copy this into your Docker setup. That's it.</p>

      <div class="tab-bar">
        <button :class="{ active: exportMode.format === 'compose' }" @click="exportMode.format = 'compose'">docker-compose.yml</button>
        <button :class="{ active: exportMode.format === 'env' }" @click="exportMode.format = 'env'">Environment Variables</button>
        <button :class="{ active: exportMode.format === 'json' }" @click="exportMode.format = 'json'">appsettings.json</button>
      </div>
      <div class="export-box">
        <pre class="export-output">{{ exportOutput }}</pre>
        <button @click="copyExport" class="btn-copy-float">{{ copied.show ? 'Copied!' : 'Copy' }}</button>
      </div>
    </section>

    <!-- ADVANCED (everything else, collapsed) -->
    <details class="advanced-section">
      <summary class="advanced-toggle">Advanced settings</summary>

      <div class="advanced-body">

        <!-- Customize features -->
        <section class="card">
          <h3>Customize features</h3>
          <input v-model="featureSearch" placeholder="Filter features..." class="feature-search" />

          <div v-for="g in filteredGroups" :key="g.key" class="feature-group">
            <div class="group-header" role="button" tabindex="0" :aria-expanded="String(isGroupOpen(g.key))" @click="toggleGroup(g.key)" @keydown.enter.prevent="toggleGroup(g.key)" @keydown.space.prevent="toggleGroup(g.key)">
              <span class="group-chevron" :class="{ open: isGroupOpen(g.key) }" aria-hidden="true">&#9656;</span>
              <span class="group-icon">{{ g.icon }}</span>
              <span class="group-label">{{ g.label }}</span>
              <span class="group-count">{{ g.features.filter(f => isFeatureOn(f.key)).length }}/{{ g.features.length }}</span>
            </div>
            <div v-show="isGroupOpen(g.key)" class="group-body">
              <template v-if="needsSubGroups(g.features)">
                <div v-for="sg in subGroups(g.features)" :key="sg.output" class="sub-group">
                  <div class="sub-group-header">
                    <span class="badge" :class="'badge-' + outputBadge(sg.output)">{{ sg.output }}s</span>
                    <span class="sub-group-count">{{ sg.features.filter(f => isFeatureOn(f.key)).length }}/{{ sg.features.length }}</span>
                  </div>
                  <div class="feature-list">
                    <div v-for="f in sg.features" :key="f.key" class="feature-card" :class="{ on: isFeatureOn(f.key) }">
                      <div class="feature-row">
                        <label class="toggle-wrap" @click.prevent="toggleFeature(f.key)">
                          <span role="switch" :aria-checked="String(isFeatureOn(f.key))" :aria-label="featureLabel(f.key)" tabindex="0" class="toggle-switch" :class="{ active: isFeatureOn(f.key) }" @keydown.enter.prevent="toggleFeature(f.key)" @keydown.space.prevent="toggleFeature(f.key)"><span class="toggle-knob" /></span>
                          <span class="feature-name">{{ featureLabel(f.key) }}</span>
                        </label>
                        <div class="feature-badges">
                          <span v-if="f.enabledByDefault" class="badge badge-default">default</span>
                          <span class="badge badge-cost" :class="{ 'badge-free': costLabel(f.key) === 'free' }">{{ costLabel(f.key) }}</span>
                        </div>
                      </div>
                      <p class="feature-desc">{{ f.description }}</p>
                      <button v-if="f.longDescription || (f.options?.length && isFeatureOn(f.key))" class="btn-link" @click.prevent="toggleInfo(f.key)">{{ expandedFeature === f.key ? 'Less' : f.options?.length && isFeatureOn(f.key) ? 'Settings' : 'More info' }}</button>
                      <div v-if="expandedFeature === f.key" class="feature-detail">
                        <p v-if="f.longDescription" class="detail-desc">{{ f.longDescription }}</p>
                        <div v-if="f.options?.length && isFeatureOn(f.key)" class="options-panel">
                          <template v-for="grp in optionGroups(f)" :key="grp.group ?? '__ungrouped'">
                            <details v-if="grp.group" class="option-group" open>
                              <summary class="option-group-label">{{ grp.group }} <span v-if="grp.group === 'Weights' && weightSumError(f.key, f.options!)" class="weight-error">{{ weightSumError(f.key, f.options!) }}</span></summary>
                              <label v-for="opt in grp.options" :key="opt.name" class="option-row" :title="opt.description">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <input type="number" step="any" :placeholder="String(opt.default)" :value="getOption(f.key, opt)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="option-input" />
                              </label>
                            </details>
                            <template v-else>
                              <template v-for="opt in grp.options" :key="opt.name">
                                <div v-if="isWindowHours(opt)" class="option-slider-row">
                                  <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                  <div class="option-slider-wrap">
                                    <input type="range" min="1" max="48" :value="getOption(f.key, opt)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="slider slider-sm" />
                                    <span class="option-slider-val">{{ getOption(f.key, opt) }}h</span>
                                  </div>
                                </div>
                                <div v-else-if="isThresholdArray(opt)" class="threshold-group">
                                  <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                  <div class="threshold-steps">
                                    <div v-for="(v, idx) in getArrayOption(f.key, opt)" :key="idx" class="threshold-step" :aria-label="(SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1)) + ' threshold'">
                                      <span class="threshold-icon" :style="{ color: SEVERITY_COLORS[idx] ?? SEVERITY_COLORS[2] }" aria-hidden="true">{{ SEVERITY_ICONS[idx] ?? SEVERITY_ICONS[2] }}</span>
                                      <span class="threshold-level">{{ SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1) }}</span>
                                      <input type="number" step="any" :value="v" :aria-label="(SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1)) + ' threshold value'" @input="setArrayOption(f.key, opt, idx, ($event.target as HTMLInputElement).value)" class="option-input option-input-sm" />
                                      <span class="threshold-unit" aria-hidden="true">{{ optionUnit(f.key, opt.name) }}</span>
                                    </div>
                                  </div>
                                </div>
                                <label v-else-if="opt.type === 'boolean'" class="option-row">
                                  <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                  <span role="switch" :aria-checked="String(getOption(f.key, opt))" :aria-label="optionLabel(opt.name)" tabindex="0" class="toggle-switch toggle-switch-sm" :class="{ active: getOption(f.key, opt) as boolean }" @click.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))" @keydown.enter.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))" @keydown.space.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))"><span class="toggle-knob" /></span>
                                </label>
                                <div v-else-if="opt.type === 'numberArray'" class="option-row">
                                  <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                  <span class="array-inputs">
                                    <input v-for="(v, idx) in getArrayOption(f.key, opt)" :key="idx" type="number" step="any" :value="v" :aria-label="optionLabel(opt.name) + ' value ' + (idx+1)" @input="setArrayOption(f.key, opt, idx, ($event.target as HTMLInputElement).value)" class="option-input option-input-sm" />
                                  </span>
                                </div>
                                <div v-else class="option-slider-row">
                                  <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                  <div class="option-slider-wrap">
                                    <input type="number" step="any" :placeholder="String(opt.default)" :value="getOption(f.key, opt)" :aria-label="optionLabel(opt.name)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="option-input" />
                                    <span v-if="optionUnit(f.key, opt.name)" class="threshold-unit" aria-hidden="true">{{ optionUnit(f.key, opt.name) }}</span>
                                  </div>
                                </div>
                              </template>
                            </template>
                          </template>
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
              </template>
              <template v-else>
                <div class="feature-list">
                  <div v-for="f in g.features" :key="f.key" class="feature-card" :class="{ on: isFeatureOn(f.key) }">
                    <div class="feature-row">
                      <label class="toggle-wrap" @click.prevent="toggleFeature(f.key)">
                        <span role="switch" :aria-checked="String(isFeatureOn(f.key))" :aria-label="featureLabel(f.key)" tabindex="0" class="toggle-switch" :class="{ active: isFeatureOn(f.key) }" @keydown.enter.prevent="toggleFeature(f.key)" @keydown.space.prevent="toggleFeature(f.key)"><span class="toggle-knob" /></span>
                        <span class="feature-name">{{ featureLabel(f.key) }}</span>
                      </label>
                      <div class="feature-badges">
                        <span class="badge" :class="'badge-' + outputBadge(f.output)">{{ f.output }}</span>
                        <span v-if="f.enabledByDefault" class="badge badge-default">default</span>
                        <span class="badge badge-cost" :class="{ 'badge-free': costLabel(f.key) === 'free' }">{{ costLabel(f.key) }}</span>
                      </div>
                    </div>
                    <p class="feature-desc">{{ f.description }}</p>
                    <button v-if="f.longDescription || (f.options?.length && isFeatureOn(f.key))" class="btn-link" @click.prevent="toggleInfo(f.key)">{{ expandedFeature === f.key ? 'Less' : f.options?.length && isFeatureOn(f.key) ? 'Settings' : 'More info' }}</button>
                    <div v-if="expandedFeature === f.key" class="feature-detail">
                      <p v-if="f.longDescription" class="detail-desc">{{ f.longDescription }}</p>
                      <div v-if="f.options?.length && isFeatureOn(f.key)" class="options-panel">
                        <template v-for="grp in optionGroups(f)" :key="grp.group ?? '__ungrouped'">
                          <details v-if="grp.group" class="option-group" open>
                            <summary class="option-group-label">{{ grp.group }} <span v-if="grp.group === 'Weights' && weightSumError(f.key, f.options!)" class="weight-error">{{ weightSumError(f.key, f.options!) }}</span></summary>
                            <label v-for="opt in grp.options" :key="opt.name" class="option-row" :title="opt.description">
                              <span class="option-label">{{ optionLabel(opt.name) }}</span>
                              <input type="number" step="any" :placeholder="String(opt.default)" :value="getOption(f.key, opt)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="option-input" />
                            </label>
                          </details>
                          <template v-else>
                            <template v-for="opt in grp.options" :key="opt.name">
                              <div v-if="isWindowHours(opt)" class="option-slider-row">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <div class="option-slider-wrap">
                                  <input type="range" min="1" max="48" :value="getOption(f.key, opt)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="slider slider-sm" />
                                  <span class="option-slider-val">{{ getOption(f.key, opt) }}h</span>
                                </div>
                              </div>
                              <div v-else-if="isThresholdArray(opt)" class="threshold-group">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <div class="threshold-steps">
                                  <div v-for="(v, idx) in getArrayOption(f.key, opt)" :key="idx" class="threshold-step" :aria-label="(SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1)) + ' threshold'">
                                    <span class="threshold-icon" :style="{ color: SEVERITY_COLORS[idx] ?? SEVERITY_COLORS[2] }" aria-hidden="true">{{ SEVERITY_ICONS[idx] ?? SEVERITY_ICONS[2] }}</span>
                                    <span class="threshold-level">{{ SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1) }}</span>
                                    <input type="number" step="any" :value="v" :aria-label="(SEVERITY_LABELS[idx] ?? 'Level ' + (idx+1)) + ' threshold value'" @input="setArrayOption(f.key, opt, idx, ($event.target as HTMLInputElement).value)" class="option-input option-input-sm" />
                                    <span class="threshold-unit" aria-hidden="true">{{ optionUnit(f.key, opt.name) }}</span>
                                  </div>
                                </div>
                              </div>
                              <label v-else-if="opt.type === 'boolean'" class="option-row">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <span role="switch" :aria-checked="String(getOption(f.key, opt))" :aria-label="optionLabel(opt.name)" tabindex="0" class="toggle-switch toggle-switch-sm" :class="{ active: getOption(f.key, opt) as boolean }" @click.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))" @keydown.enter.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))" @keydown.space.prevent="storeOption(f.key, opt, !(getOption(f.key, opt) as boolean))"><span class="toggle-knob" /></span>
                              </label>
                              <div v-else-if="opt.type === 'numberArray'" class="option-row">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <span class="array-inputs">
                                  <input v-for="(v, idx) in getArrayOption(f.key, opt)" :key="idx" type="number" step="any" :value="v" :aria-label="optionLabel(opt.name) + ' value ' + (idx+1)" @input="setArrayOption(f.key, opt, idx, ($event.target as HTMLInputElement).value)" class="option-input option-input-sm" />
                                </span>
                              </div>
                              <div v-else class="option-slider-row">
                                <span class="option-label">{{ optionLabel(opt.name) }}</span>
                                <div class="option-slider-wrap">
                                  <input type="number" step="any" :placeholder="String(opt.default)" :value="getOption(f.key, opt)" :aria-label="optionLabel(opt.name)" @input="setNumberOption(f.key, opt, ($event.target as HTMLInputElement).value)" class="option-input" />
                                  <span v-if="optionUnit(f.key, opt.name)" class="threshold-unit" aria-hidden="true">{{ optionUnit(f.key, opt.name) }}</span>
                                </div>
                              </div>
                            </template>
                          </template>
                        </template>
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
              </template>
            </div>
          </div>
        </section>

        <!-- API budget -->
        <section class="card">
          <h3>API Budget</h3>
          <div class="budget-card" :class="'level-' + budgetLevel(budget.percentUsed)">
            <div class="budget-header">
              <span>{{ budget.projected.toLocaleString() }} / {{ budget.limit.toLocaleString() }} requests/month</span>
              <span class="budget-pct-lg" :class="'level-' + budgetLevel(budget.percentUsed)">{{ budget.percentUsed.toFixed(1) }}%</span>
            </div>
            <div class="budget-bar-lg" role="progressbar" :aria-valuenow="Math.round(budget.percentUsed)" aria-valuemin="0" aria-valuemax="100" :aria-label="'API budget usage: ' + budget.percentUsed.toFixed(1) + '%'">
              <div class="budget-bar-lg-fill" :style="{ width: Math.min(budget.percentUsed, 100) + '%' }" :class="'level-' + budgetLevel(budget.percentUsed)" />
            </div>
            <div class="budget-detail">
              {{ config.locations.length || 1 }} location{{ config.locations.length !== 1 ? 's' : '' }} &middot;
              {{ requiredDomains(config.features).length }} domains &middot;
              {{ budget.cyclesPerDay }} polls/day &middot;
              {{ budget.callsPerDay.toLocaleString() }} calls/day
            </div>
            <p v-if="budget.overBudget" class="error">Exceeds 80% budget guard. Disable features, reduce locations or increase the poll interval.</p>
          </div>
        </section>

        <!-- Domain & polling settings -->
        <section class="card">
          <h3>Domain settings</h3>
          <div class="slider-grid">
            <div class="slider-field">
              <label class="field-label">Forecast Days</label>
              <div class="slider-row"><input type="range" min="1" max="16" :value="config.forecast.forecastDays" aria-label="Forecast Days" @input="config.forecast.forecastDays = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.forecast.forecastDays }}</span></div>
            </div>
            <div class="slider-field">
              <label class="field-label">Ensemble Days</label>
              <div class="slider-row"><input type="range" min="1" max="35" :value="config.ensemble.forecastDays" aria-label="Ensemble Days" @input="config.ensemble.forecastDays = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.ensemble.forecastDays }}</span></div>
            </div>
            <div class="slider-field">
              <label class="field-label">Air Quality Days</label>
              <div class="slider-row"><input type="range" min="1" max="7" :value="config.airQuality.forecastDays" aria-label="Air Quality Days" @input="config.airQuality.forecastDays = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.airQuality.forecastDays }}</span></div>
            </div>
            <div class="slider-field">
              <label class="field-label">Marine Days</label>
              <div class="slider-row"><input type="range" min="1" max="16" :value="config.marine.forecastDays" aria-label="Marine Days" @input="config.marine.forecastDays = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.marine.forecastDays }}</span></div>
            </div>
            <div class="slider-field">
              <label class="field-label">Flood Days</label>
              <div class="slider-row"><input type="range" min="1" max="210" :value="config.flood.forecastDays" aria-label="Flood Days" @input="config.flood.forecastDays = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.flood.forecastDays }}</span></div>
            </div>
          </div>
          <details class="settings-details" style="margin-top: 12px;">
            <summary>Ensemble models</summary>
            <div class="model-list">
              <label v-for="m in weatherModels" :key="m.id" class="model-item">
                <input type="checkbox" :checked="config.ensemble.models.includes(m.id)" @change="toggleModel(m.id, ($event.target as HTMLInputElement).checked)" />
                <span>{{ m.displayName }} <span class="text-muted">({{ m.resolutionKm }} km, {{ m.forecastDays }}d)</span></span>
              </label>
            </div>
          </details>
          <div v-if="config.features['SolarYield']" style="margin-top: 12px;">
            <h4>Solar panel</h4>
            <div class="slider-grid">
              <div class="slider-field">
                <label class="field-label">Azimuth</label>
                <div class="slider-row"><input type="range" min="0" max="360" :value="config.solar.azimuth" aria-label="Azimuth" @input="config.solar.azimuth = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.solar.azimuth }}°</span></div>
              </div>
              <div class="slider-field">
                <label class="field-label">Tilt</label>
                <div class="slider-row"><input type="range" min="0" max="90" :value="config.solar.tilt" aria-label="Tilt" @input="config.solar.tilt = Number(($event.target as HTMLInputElement).value)" class="slider" /><span class="slider-value" aria-hidden="true">{{ config.solar.tilt }}°</span></div>
              </div>
            </div>
          </div>
        </section>

        <!-- Storage, polling, resources -->
        <section class="card">
          <h3>Storage &amp; polling</h3>
          <div class="persistence-row">
            <label class="radio-card" :class="{ selected: config.persistence.provider === 'Sqlite' }">
              <input type="radio" value="Sqlite" v-model="config.persistence.provider" /> SQLite <span class="badge badge-default">default</span>
            </label>
            <label class="radio-card" :class="{ selected: config.persistence.provider === 'PostgreSql' }">
              <input type="radio" value="PostgreSql" v-model="config.persistence.provider" /> PostgreSQL
            </label>
          </div>
          <div v-if="config.persistence.provider === 'PostgreSql'" class="field" style="margin-top: 8px;">
            <label class="field-label">Connection String</label>
            <input v-model="config.persistence.connectionString" placeholder="Host=localhost;Database=njord;..." style="width: 100%;" />
          </div>
          <div class="settings-grid" style="margin-top: 12px;">
            <div class="field"><label class="field-label">Poll Interval</label><input v-model="config.pollInterval" placeholder="01:00:00" /></div>
            <div class="field"><label class="field-label">CPU Limit</label><input v-model="config.deploy.limits.cpus" placeholder="e.g. 1.0" /></div>
            <div class="field"><label class="field-label">Memory Limit</label><input v-model="config.deploy.limits.memory" placeholder="e.g. 256M" /></div>
          </div>
          <p v-if="memoryTip" class="tip">{{ memoryTip }}</p>
        </section>

        <!-- Import -->
        <section class="card">
          <h3>Import existing config</h3>
          <div class="import-box">
            <textarea v-model="importText.value" placeholder="Paste appsettings.json, env vars, or docker-compose environment block here..." rows="4" />
            <button @click="doImport" :disabled="!importText.value.trim()" class="btn-primary">Import</button>
            <p v-if="importText.error" class="error">{{ importText.error }}</p>
          </div>
        </section>

        <!-- Manual location add -->
        <section class="card">
          <h3>Add location manually</h3>
          <div class="location-fields">
            <div class="field"><label class="field-label">Name</label><input v-model="newLocation" placeholder="e.g. Lucerne" @keyup.enter="addLocation" /></div>
            <div class="field field-coord"><label class="field-label">Lat</label><input v-model.number="newLatitude" type="number" step="any" min="-90" max="90" placeholder="47.05" @keyup.enter="addLocation" /></div>
            <div class="field field-coord"><label class="field-label">Lon</label><input v-model.number="newLongitude" type="number" step="any" min="-180" max="180" placeholder="8.31" @keyup.enter="addLocation" /></div>
            <button @click="addLocation" :disabled="!canAddLocation" class="btn-primary btn-add-loc">Add</button>
          </div>
        </section>

      </div>
    </details>

  </div>
</template>

<style scoped>
.builder { max-width: 900px; display: flex; flex-direction: column; gap: 20px; }

/* ── Cards ── */
.card { padding: 24px; border: 1px solid var(--vp-c-divider); border-radius: 12px; background: var(--vp-c-bg); }
.card h3 { margin: 0 0 6px; font-size: 20px; font-weight: 700; }
.card h4 { margin: 12px 0 8px; font-size: 14px; font-weight: 600; color: var(--vp-c-text-1); }
.card-hero h3 { font-size: 22px; }

/* ── Location chips ── */
.loc-chip { display: inline-flex; align-items: center; gap: 8px; padding: 6px 12px; margin: 0 6px 8px 0; background: var(--vp-c-brand-soft); border: 1px solid var(--vp-c-brand-1); border-radius: 20px; font-size: 13px; }
.loc-chip-name { font-weight: 600; color: var(--vp-c-text-1); }
.loc-chip-coords { font-size: 11px; color: var(--vp-c-text-3); font-variant-numeric: tabular-nums; }
.loc-chip-x { background: none; border: none; color: var(--vp-c-text-3); cursor: pointer; font-size: 16px; padding: 0 2px; line-height: 1; }
.loc-chip-x:hover { color: var(--vp-c-danger-1); }

/* ── Tier summary line ── */
.tier-summary { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; font-size: 13px; color: var(--vp-c-text-2); padding: 8px 0 0; }
.tier-summary-sep { color: var(--vp-c-text-3); }
.tier-summary .level-ok { color: var(--vp-c-brand-1); font-weight: 600; }
.tier-summary .level-warn { color: var(--vp-c-warning-1); font-weight: 600; }
.tier-summary .level-over { color: var(--vp-c-danger-1); font-weight: 600; }

/* ── Advanced section ── */
.advanced-section { margin-top: 4px; }
.advanced-toggle { font-size: 14px; font-weight: 500; color: var(--vp-c-text-3); cursor: pointer; padding: 10px 0; list-style: none; }
.advanced-toggle::-webkit-details-marker { display: none; }
.advanced-toggle::before { content: '+ '; }
.advanced-section[open] > .advanced-toggle::before { content: '- '; }
.advanced-toggle:hover { color: var(--vp-c-text-1); }
.advanced-body { display: flex; flex-direction: column; gap: 16px; padding-top: 8px; }

/* ── Sub-cards ── */
.sub-card { padding: 16px; border: 1px solid var(--vp-c-divider); border-radius: 10px; background: var(--vp-c-bg-soft); margin-bottom: 12px; }
.sub-card h4 { margin: 0 0 10px; font-size: 14px; font-weight: 600; color: var(--vp-c-text-1); }
.sub-card h4:not(:first-child) { margin-top: 16px; }

/* ── Pills & Badges ── */
.pill { font-size: 12px; padding: 3px 12px; border-radius: 10px; background: var(--vp-c-brand-soft); color: var(--vp-c-brand-1); font-weight: 600; font-variant-numeric: tabular-nums; }
.badge { font-size: 10px; padding: 2px 7px; border-radius: 4px; font-weight: 500; white-space: nowrap; }
.badge-alert { background: #fef3cd; color: #856404; }
.badge-score { background: #d1ecf1; color: #0c5460; }
.badge-condition { background: #d4edda; color: #155724; }
.badge-trend { background: #e2d5f1; color: #4a235a; }
.badge-forecast { background: #cce5ff; color: #004085; }
.badge-weather { background: var(--vp-c-bg-mute); color: var(--vp-c-text-2); }
.badge-observation { background: var(--vp-c-bg-mute); color: var(--vp-c-text-2); }
.dark .badge-alert { background: #5a4b1e; color: #ffc107; }
.dark .badge-score { background: #1a3a42; color: #7dd3e0; }
.dark .badge-condition { background: #1a3a24; color: #6fcf97; }
.dark .badge-trend { background: #2d1a4a; color: #bb9fed; }
.dark .badge-forecast { background: #1a2d4a; color: #7db8f0; }
.badge-default { background: var(--vp-c-bg-mute); color: var(--vp-c-text-3); }
.badge-cost { color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.badge-free { color: var(--vp-c-text-3); background: var(--vp-c-bg-mute); }

/* ── Buttons ── */
button { padding: 6px 14px; border: 1px solid var(--vp-c-divider); border-radius: 6px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-1); cursor: pointer; font-size: 13px; transition: all 0.15s; }
button:hover { background: var(--vp-c-bg-mute); }
button:disabled { opacity: 0.4; cursor: not-allowed; }
.btn-primary { background: var(--vp-c-brand-1); color: white; border-color: var(--vp-c-brand-1); font-weight: 500; }
.btn-primary:hover { opacity: 0.9; background: var(--vp-c-brand-1); }
.btn-outline { padding: 4px 12px; font-size: 12px; border: 1px solid var(--vp-c-brand-1); border-radius: 6px; background: transparent; color: var(--vp-c-brand-1); font-weight: 500; }
.btn-outline:hover { background: var(--vp-c-brand-soft); }
.btn-icon { width: 32px; height: 32px; padding: 0; display: flex; align-items: center; justify-content: center; border-radius: 6px; font-size: 18px; line-height: 1; }
.btn-danger { color: var(--vp-c-danger-1); border-color: var(--vp-c-danger-1); }
.btn-danger:hover { background: rgba(220, 38, 38, 0.08); }
.btn-link { border: none; background: none; color: var(--vp-c-brand-1); cursor: pointer; font-size: 11px; padding: 2px 0; text-decoration: underline; text-underline-offset: 2px; }
.btn-link:hover { opacity: 0.8; background: none; }

/* ── Toggle Switch ── */
.toggle-wrap { display: flex; align-items: center; gap: 8px; cursor: pointer; user-select: none; }
.toggle-switch { position: relative; width: 36px; height: 20px; border-radius: 10px; background: var(--vp-c-divider); transition: background 0.2s; flex-shrink: 0; }
.toggle-switch.active { background: var(--vp-c-brand-1); }
.toggle-knob { position: absolute; top: 2px; left: 2px; width: 16px; height: 16px; border-radius: 50%; background: white; transition: transform 0.2s; box-shadow: 0 1px 3px rgba(0,0,0,0.15); }
.toggle-switch.active .toggle-knob { transform: translateX(16px); }
.toggle-switch-sm { width: 28px; height: 16px; border-radius: 8px; }
.toggle-switch-sm .toggle-knob { width: 12px; height: 12px; }
.toggle-switch-sm.active .toggle-knob { transform: translateX(12px); }
.toggle-small { gap: 4px; }

/* ── Forms ── */
input, textarea, select { padding: 7px 10px; border: 1px solid var(--vp-c-divider); border-radius: 6px; background: var(--vp-c-bg); color: var(--vp-c-text-1); font-size: 13px; transition: border-color 0.15s; }
input:focus, textarea:focus, select:focus { outline: none; border-color: var(--vp-c-brand-1); box-shadow: 0 0 0 3px var(--vp-c-brand-soft); }
textarea { width: 100%; font-family: var(--vp-font-family-mono); resize: vertical; }
select { appearance: auto; }
.field { display: flex; flex-direction: column; gap: 4px; }
.field-label { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--vp-c-text-3); }
.field-coord { flex: 0 0 100px; }
.settings-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 10px; }
.settings-grid input, .settings-grid select { width: 100%; }

/* ── Locations ── */
.location-card { padding: 10px 14px; margin-bottom: 8px; background: var(--vp-c-bg-soft); border: 1px solid var(--vp-c-divider); border-radius: 8px; }
.location-fields { display: flex; gap: 8px; align-items: flex-end; flex-wrap: wrap; }
.location-fields .field { flex: 1; min-width: 120px; }
.btn-add-loc { flex-shrink: 0; align-self: flex-end; }

/* ── Geo search ── */
.geo-search { position: relative; margin-bottom: 8px; }
.geo-input-wrap { position: relative; }
.geo-input { width: 100%; padding: 10px 14px; font-size: 14px; border: 2px dashed var(--vp-c-divider); border-radius: 8px; background: var(--vp-c-bg-soft); transition: border-color 0.15s; }
.geo-input:focus { border-color: var(--vp-c-brand-1); border-style: solid; }
.geo-input::placeholder { color: var(--vp-c-text-3); }
.geo-spinner { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); width: 16px; height: 16px; border: 2px solid var(--vp-c-divider); border-top-color: var(--vp-c-brand-1); border-radius: 50%; animation: spin 0.6s linear infinite; }
@keyframes spin { to { transform: translateY(-50%) rotate(360deg); } }
.geo-dropdown { position: absolute; top: 100%; left: 0; right: 0; z-index: 20; margin-top: 4px; background: var(--vp-c-bg); border: 1px solid var(--vp-c-divider); border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,0.1); overflow: hidden; }
.dark .geo-dropdown { box-shadow: 0 4px 16px rgba(0,0,0,0.3); }
.geo-option { display: flex; align-items: center; gap: 8px; width: 100%; padding: 10px 14px; border: none; background: none; color: var(--vp-c-text-1); font-size: 13px; cursor: pointer; text-align: left; transition: background 0.1s; }
.geo-option:hover { background: var(--vp-c-brand-soft); }
.geo-option + .geo-option { border-top: 1px solid var(--vp-c-divider); }
.geo-name { font-weight: 600; }
.geo-meta { color: var(--vp-c-text-3); font-size: 12px; }
.geo-coords { margin-left: auto; font-size: 11px; color: var(--vp-c-text-3); font-variant-numeric: tabular-nums; font-family: var(--vp-font-family-mono); }
.manual-add { margin-top: 4px; }
.manual-add summary { font-size: 12px; color: var(--vp-c-text-3); cursor: pointer; padding: 4px 0; }
.manual-add summary:hover { color: var(--vp-c-text-2); }

/* ── Domains strip ── */
.domains-strip { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-bottom: 16px; padding: 10px 14px; background: var(--vp-c-bg-soft); border-radius: 8px; }
.domains-label { font-size: 12px; color: var(--vp-c-text-3); font-weight: 500; }
.domain-chip { font-size: 11px; padding: 3px 10px; border-radius: 12px; background: var(--vp-c-brand-soft); color: var(--vp-c-brand-1); font-weight: 600; }
.domains-none { font-size: 12px; color: var(--vp-c-text-3); font-style: italic; }

/* ── Feature groups ── */
.feature-group { margin-bottom: 8px; border: 1px solid var(--vp-c-divider); border-radius: 10px; overflow: hidden; }
.group-header { display: flex; align-items: center; gap: 8px; padding: 12px 14px; background: var(--vp-c-bg-soft); cursor: pointer; user-select: none; transition: background 0.15s; }
.group-header:hover { background: var(--vp-c-bg-mute); }
.group-chevron { font-size: 12px; color: var(--vp-c-text-3); transition: transform 0.2s; display: inline-block; }
.group-chevron.open { transform: rotate(90deg); }
.group-icon { font-size: 18px; }
.group-label { font-size: 15px; font-weight: 700; color: var(--vp-c-text-1); }
.group-count { font-size: 12px; color: var(--vp-c-text-3); margin-left: auto; font-variant-numeric: tabular-nums; background: var(--vp-c-bg); padding: 2px 10px; border-radius: 8px; font-weight: 500; }
.group-body { padding: 8px; }

/* ── Sub-groups ── */
.sub-group { margin-bottom: 8px; }
.sub-group:last-child { margin-bottom: 0; }
.sub-group-header { display: flex; align-items: center; gap: 8px; padding: 6px 6px 8px; }
.sub-group-count { font-size: 11px; color: var(--vp-c-text-3); font-variant-numeric: tabular-nums; }

.feature-list { display: flex; flex-direction: column; gap: 6px; }
.feature-card { padding: 12px 14px; border: 1px solid var(--vp-c-divider); border-radius: 8px; font-size: 13px; transition: all 0.15s; background: var(--vp-c-bg); }
.feature-card.on { border-color: var(--vp-c-brand-1); border-left: 3px solid var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.feature-row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
.feature-name { font-weight: 600; }
.feature-badges { display: flex; gap: 4px; flex-shrink: 0; flex-wrap: wrap; }
.feature-desc { margin: 4px 0 2px; font-size: 12px; color: var(--vp-c-text-3); line-height: 1.5; }

/* ── Feature detail / options ── */
.feature-detail { margin-top: 10px; padding-top: 10px; border-top: 1px solid var(--vp-c-divider); animation: slideDown 0.15s ease-out; }
@keyframes slideDown { from { opacity: 0; transform: translateY(-4px); } to { opacity: 1; transform: translateY(0); } }
.detail-desc { font-size: 12px; color: var(--vp-c-text-2); margin: 0 0 8px; line-height: 1.5; }
.options-panel { padding: 10px 12px; background: var(--vp-c-bg-soft); border-radius: 8px; display: flex; flex-direction: column; gap: 6px; }
.option-row { display: flex; justify-content: space-between; align-items: center; font-size: 12px; color: var(--vp-c-text-2); gap: 8px; }
.option-label { min-width: 100px; font-weight: 500; }
.option-input { width: 90px; font-size: 12px; padding: 4px 8px; }
.option-input-sm { width: 70px; }
.array-inputs { display: flex; gap: 4px; }

/* ── Threshold severity steps ── */
.threshold-group { display: flex; flex-direction: column; gap: 6px; margin: 4px 0; }
.threshold-steps { display: flex; flex-direction: column; gap: 4px; padding-left: 4px; }
.threshold-step { display: flex; align-items: center; gap: 8px; font-size: 12px; }
.threshold-icon { width: 16px; text-align: center; font-size: 12px; flex-shrink: 0; line-height: 1; }
.threshold-level { width: 52px; font-weight: 600; color: var(--vp-c-text-2); }
.threshold-unit { font-size: 11px; color: var(--vp-c-text-3); min-width: 24px; }

/* ── Option slider row (WindowHours etc) ── */
.option-slider-row { display: flex; flex-direction: column; gap: 4px; margin: 4px 0; }
.option-slider-wrap { display: flex; align-items: center; gap: 8px; }
.option-slider-val { font-size: 13px; font-weight: 600; font-variant-numeric: tabular-nums; color: var(--vp-c-text-1); min-width: 32px; }
.slider-sm { height: 4px; }
.option-group { margin: 2px 0; }
.option-group-label { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--vp-c-text-3); cursor: pointer; user-select: none; display: flex; align-items: center; gap: 8px; }
.weight-error { color: var(--vp-c-danger-1, #e53935); font-weight: normal; font-size: 11px; text-transform: none; letter-spacing: normal; }
.entity-list { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; margin: 8px 0 6px; }
.entity-label { font-size: 11px; color: var(--vp-c-text-3); }
.entity-list code { font-size: 10px; padding: 2px 6px; border-radius: 4px; background: var(--vp-c-bg-mute); color: var(--vp-c-text-2); }
.domain-tags { display: flex; gap: 4px; }
.domain-tag { font-size: 10px; padding: 2px 7px; border-radius: 4px; background: var(--vp-c-bg-mute); color: var(--vp-c-text-3); }

/* ── Settings details ── */
.settings-details summary { cursor: pointer; color: var(--vp-c-text-2); font-size: 14px; font-weight: 500; padding: 4px 0; }
.settings-details summary:hover { color: var(--vp-c-text-1); }
details.sub-card > summary { margin-bottom: 10px; }

/* ── Budget card ── */
.budget-card { padding: 18px; border: 1px solid var(--vp-c-divider); border-radius: 10px; background: var(--vp-c-bg-soft); margin-bottom: 16px; }
.budget-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.budget-header h4 { margin: 0; font-size: 14px; font-weight: 600; }
.budget-pct-lg { font-size: 22px; font-weight: 700; font-variant-numeric: tabular-nums; color: var(--vp-c-brand-1); }
.budget-pct-lg.level-warn { color: var(--vp-c-warning-1); }
.budget-pct-lg.level-over { color: var(--vp-c-danger-1); }
.budget-bar-lg { height: 10px; background: var(--vp-c-divider); border-radius: 5px; overflow: hidden; margin-bottom: 14px; }
.budget-bar-lg-fill { height: 100%; background: var(--vp-c-brand-1); border-radius: 5px; transition: width 0.3s; }
.budget-bar-lg-fill.level-warn { background: var(--vp-c-warning-1); }
.budget-bar-lg-fill.level-over { background: var(--vp-c-danger-1); }
.budget-numbers { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; }
.budget-num-item { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 8px; background: var(--vp-c-bg); border-radius: 6px; }
.budget-num { font-size: 15px; font-weight: 700; color: var(--vp-c-text-1); font-variant-numeric: tabular-nums; }
.budget-num-label { font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--vp-c-text-3); font-weight: 500; }
.budget-hint { font-size: 12px; color: var(--vp-c-text-3); margin: 12px 0 0; }
@media (max-width: 640px) { .budget-numbers { grid-template-columns: repeat(2, 1fr); } }

/* ── Persistence ── */
.persistence-row { display: flex; gap: 8px; margin-bottom: 8px; }
.radio-card { display: flex; gap: 6px; align-items: center; padding: 8px 14px; border: 1px solid var(--vp-c-divider); border-radius: 8px; cursor: pointer; font-size: 13px; transition: all 0.15s; }
.radio-card.selected { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.radio-card input[type="radio"] { accent-color: var(--vp-c-brand-1); }

/* ── Model list ── */
.model-list { display: flex; flex-direction: column; gap: 4px; margin-top: 8px; }
.model-item { display: flex; gap: 8px; align-items: center; font-size: 13px; padding: 4px 0; cursor: pointer; }
.model-item input { accent-color: var(--vp-c-brand-1); }
.text-muted { color: var(--vp-c-text-3); font-size: 12px; }

/* ── Export ── */
.tab-bar { display: flex; gap: 2px; background: var(--vp-c-bg-soft); border-radius: 8px; padding: 3px; margin-bottom: 12px; }
.tab-bar button { border: none; border-radius: 6px; background: transparent; font-size: 12px; padding: 7px 14px; color: var(--vp-c-text-2); font-weight: 500; }
.tab-bar button:hover { background: var(--vp-c-bg-mute); color: var(--vp-c-text-1); }
.tab-bar button.active { background: var(--vp-c-bg); color: var(--vp-c-text-1); box-shadow: 0 1px 3px rgba(0,0,0,0.06); }
.dark .tab-bar button.active { box-shadow: 0 1px 3px rgba(0,0,0,0.2); }
.export-box { position: relative; }
.btn-copy-float { position: absolute; top: 10px; right: 10px; font-size: 11px; padding: 4px 12px; border-radius: 4px; background: var(--vp-c-bg); border: 1px solid var(--vp-c-divider); opacity: 0.8; }
.btn-copy-float:hover { opacity: 1; }
.export-output { padding: 16px; padding-right: 80px; background: var(--vp-c-bg-soft); border: 1px solid var(--vp-c-divider); border-radius: 8px; font-size: 12px; overflow-x: auto; max-height: 400px; white-space: pre; line-height: 1.6; }

/* ── Import ── */
.import-box { display: flex; flex-direction: column; gap: 8px; margin-top: 10px; }
.import-box button { align-self: flex-start; }

/* ── Focus for custom controls ── */
.toggle-switch:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: 2px; border-radius: 10px; }
.toggle-switch-sm:focus-visible { border-radius: 8px; }
.group-header:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: -2px; border-radius: 8px; }
.tier-card:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: 2px; }

/* ── Screen reader only ── */
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }

/* ── Utility ── */
.hint { font-size: 12px; color: var(--vp-c-text-2); margin: 0 0 10px; line-height: 1.5; }
.error { color: var(--vp-c-danger-1); font-size: 13px; margin-top: 4px; }
.tip { color: var(--vp-c-warning-1); font-size: 12px; margin-top: 6px; }

/* ── Tier cards ── */
.tier-row { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; margin-bottom: 14px; }
.tier-card { display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 16px 12px; border: 2px solid var(--vp-c-divider); border-radius: 10px; background: var(--vp-c-bg-soft); cursor: pointer; transition: all 0.15s; text-align: center; }
.tier-card:hover { border-color: var(--vp-c-brand-2); background: var(--vp-c-bg-mute); }
.tier-card.active { border-color: var(--vp-c-brand-1); background: var(--vp-c-brand-soft); }
.tier-label { font-size: 15px; font-weight: 700; color: var(--vp-c-text-1); }
.tier-count { font-size: 12px; font-weight: 600; color: var(--vp-c-brand-1); }
.tier-desc { font-size: 11px; color: var(--vp-c-text-3); line-height: 1.4; }
.tier-domains { font-size: 10px; color: var(--vp-c-text-3); }
@media (max-width: 640px) { .tier-row { grid-template-columns: 1fr; } }

/* ── Feature search ── */
.feature-search { width: 100%; padding: 8px 14px; margin-bottom: 12px; border: 1px solid var(--vp-c-divider); border-radius: 8px; background: var(--vp-c-bg-soft); color: var(--vp-c-text-1); font-size: 13px; }
.feature-search:focus { outline: none; border-color: var(--vp-c-brand-1); box-shadow: 0 0 0 3px var(--vp-c-brand-soft); }
.feature-search::placeholder { color: var(--vp-c-text-3); }

/* ── Suggestion banner ── */
.suggestion-banner { display: flex; align-items: center; gap: 12px; padding: 12px 16px; background: var(--vp-c-brand-soft); border: 1px solid var(--vp-c-brand-1); border-radius: 10px; flex-wrap: wrap; }
.suggestion-text { flex: 1; font-size: 13px; color: var(--vp-c-text-1); min-width: 200px; }
.suggestion-actions { display: flex; gap: 6px; }
.btn-ghost { border: none; background: none; color: var(--vp-c-text-3); cursor: pointer; font-size: 12px; padding: 4px 8px; }
.btn-ghost:hover { color: var(--vp-c-text-1); background: none; }

/* ── Sliders ── */
.slider-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 14px; }
.slider-field { display: flex; flex-direction: column; gap: 6px; }
.slider-row { display: flex; align-items: center; gap: 10px; }
.slider { flex: 1; -webkit-appearance: none; appearance: none; height: 6px; border-radius: 3px; background: var(--vp-c-divider); outline: none; cursor: pointer; }
.slider::-webkit-slider-thumb { -webkit-appearance: none; appearance: none; width: 18px; height: 18px; border-radius: 50%; background: var(--vp-c-brand-1); cursor: pointer; border: 2px solid var(--vp-c-bg); box-shadow: 0 1px 4px rgba(0,0,0,0.2); }
.slider::-moz-range-thumb { width: 18px; height: 18px; border-radius: 50%; background: var(--vp-c-brand-1); cursor: pointer; border: 2px solid var(--vp-c-bg); box-shadow: 0 1px 4px rgba(0,0,0,0.2); }
.slider:focus { outline: none; }
.slider:focus::-webkit-slider-thumb { box-shadow: 0 0 0 4px var(--vp-c-brand-soft); }
.slider-value { min-width: 36px; text-align: right; font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; color: var(--vp-c-text-1); }
</style>
