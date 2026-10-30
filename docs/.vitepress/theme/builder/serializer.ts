import type { NjordConfig, OptionValue } from './types'
import { FEATURES, defaultConfig, defaultFeatures } from './types'

// Only toggles that differ from their default are exported.
function changedFeatures(features: Record<string, boolean>): [string, boolean][] {
  const defaults = defaultFeatures()
  return FEATURES
    .filter(f => (features[f.key] ?? defaults[f.key]) !== defaults[f.key])
    .map(f => [f.key, features[f.key]])
}

function sameValue(a: OptionValue, b: OptionValue): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

// Option values of enabled features that differ from the feature default, as [feature key, option name, value].
function changedOptions(config: NjordConfig): [string, string, OptionValue][] {
  const result: [string, string, OptionValue][] = []
  for (const f of FEATURES) {
    if (!config.features[f.key]) continue
    for (const o of f.options ?? []) {
      const value = config.featureOptions[f.key]?.[o.name]
      if (value !== undefined && !sameValue(value, o.default)) result.push([f.key, o.name, value])
    }
  }
  return result
}

function parseOptionValue(featureKey: string, option: string, value: unknown): OptionValue | null {
  const def = FEATURES.find(f => f.key === featureKey)?.options?.find(o => o.name.toLowerCase() === option.toLowerCase())
  if (!def) return null
  if (def.type === 'boolean') {
    if (typeof value === 'boolean') return value
    return String(value).trim().toLowerCase() === 'true'
  }
  if (def.type === 'numberArray') {
    if (!Array.isArray(value)) return null
    const numbers = value.map(Number)
    return numbers.some(Number.isNaN) ? null : numbers
  }
  const num = Number(value)
  return Number.isNaN(num) ? null : num
}

function optionName(featureKey: string, option: string): string | null {
  return FEATURES.find(f => f.key === featureKey)?.options?.find(o => o.name.toLowerCase() === option.toLowerCase())?.name ?? null
}

function parseFeatureKey(value: string): string | null {
  return FEATURES.find(f => f.key.toLowerCase() === value.toLowerCase())?.key ?? null
}

// Fills everything an import or a shared URL does not mention with defaults, nested objects included.
export function mergeWithDefaults(partial: Partial<NjordConfig>): NjordConfig {
  const d = defaultConfig()
  return {
    ...d,
    ...partial,
    features: { ...defaultFeatures(), ...partial.features },
    featureOptions: { ...partial.featureOptions },
    forecast: { ...d.forecast, ...partial.forecast },
    ensemble: { ...d.ensemble, ...partial.ensemble },
    airQuality: { ...d.airQuality, ...partial.airQuality },
    marine: { ...d.marine, ...partial.marine },
    flood: { ...d.flood, ...partial.flood },
    solar: { ...d.solar, ...partial.solar },
    persistence: { ...d.persistence, ...partial.persistence },
    deploy: { limits: { ...d.deploy.limits, ...partial.deploy?.limits } },
  }
}

function parseBool(value: unknown): boolean {
  return String(value).trim().toLowerCase() === 'true'
}

export function exportAsJson(config: NjordConfig): string {
  const out: Record<string, unknown> = { Njord: {} }
  const n = out.Njord as Record<string, unknown>

  const defaults = defaultConfig()
  const polling: Record<string, unknown> = { PollInterval: config.pollInterval }
  if (config.discoveryInterval !== defaults.discoveryInterval) polling.DiscoveryInterval = config.discoveryInterval
  if (config.budgetOverride) polling.BudgetOverride = config.budgetOverride
  n.Polling = polling
  const forecast: Record<string, unknown> = {}
  if (config.forecast.forecastDays !== defaults.forecast.forecastDays) forecast.ForecastDays = config.forecast.forecastDays
  if (config.forecast.pastHours !== defaults.forecast.pastHours) forecast.PastHours = config.forecast.pastHours
  if (config.forecast.models.join(',') !== defaults.forecast.models.join(',')) forecast.Models = config.forecast.models
  if (Object.keys(forecast).length) n.Forecast = forecast
  const ensemble: Record<string, unknown> = {}
  if (config.ensemble.forecastDays !== defaults.ensemble.forecastDays) ensemble.ForecastDays = config.ensemble.forecastDays
  if (config.ensemble.models.join(',') !== defaults.ensemble.models.join(',')) ensemble.Models = config.ensemble.models
  if (Object.keys(ensemble).length) n.Ensemble = ensemble
  const aq: Record<string, unknown> = {}
  if (config.airQuality.forecastDays !== defaults.airQuality.forecastDays) aq.ForecastDays = config.airQuality.forecastDays
  if (config.airQuality.region !== defaults.airQuality.region) aq.Region = config.airQuality.region
  if (Object.keys(aq).length) n.AirQuality = aq
  if (config.marine.forecastDays !== defaults.marine.forecastDays) n.Marine = { ForecastDays: config.marine.forecastDays }
  const flood: Record<string, unknown> = {}
  if (config.flood.forecastDays !== defaults.flood.forecastDays) flood.ForecastDays = config.flood.forecastDays
  if (config.flood.includeEnsemble) flood.IncludeEnsemble = true
  if (Object.keys(flood).length) n.Flood = flood
  const solar: Record<string, unknown> = {}
  if (config.solar.tilt !== defaults.solar.tilt) solar.Tilt = config.solar.tilt
  if (config.solar.azimuth !== defaults.solar.azimuth) solar.Azimuth = config.solar.azimuth
  if (Object.keys(solar).length) n.Solar = solar
  const features: Record<string, Record<string, unknown>> = {}
  for (const [key, enabled] of changedFeatures(config.features)) {
    ;(features[key] ??= {}).Enabled = enabled
  }
  for (const [key, option, value] of changedOptions(config)) {
    ;(features[key] ??= {})[option] = value
  }
  if (Object.keys(features).length) n.Features = features
  if (config.locations.length) {
    n.Locations = config.locations.map(l => {
      const loc: Record<string, unknown> = { Name: l.name, Latitude: l.latitude, Longitude: l.longitude }
      return loc
    })
  }

  const persistence: Record<string, unknown> = {}
  if (config.persistence.provider !== 'Sqlite') {
    persistence.Provider = config.persistence.provider
    persistence.ConnectionString = config.persistence.connectionString
  }
  if (config.dataPath !== defaults.dataPath) persistence.DataPath = config.dataPath
  if (Object.keys(persistence).length) n.Persistence = persistence

  return JSON.stringify(out, null, 2)
}

export function exportAsEnvVars(config: NjordConfig): string {
  const lines: string[] = []
  const add = (key: string, value: string) => lines.push(`${key}=${value}`)

  const defaults = defaultConfig()
  add('Njord__Polling__PollInterval', config.pollInterval)
  if (config.forecast.forecastDays !== defaults.forecast.forecastDays) add('Njord__Forecast__ForecastDays', String(config.forecast.forecastDays))
  if (config.forecast.pastHours !== defaults.forecast.pastHours) add('Njord__Forecast__PastHours', String(config.forecast.pastHours))
  if (config.forecast.models.join(',') !== defaults.forecast.models.join(',')) config.forecast.models.forEach((m, i) => add(`Njord__Forecast__Models__${i}`, m))
  if (config.ensemble.forecastDays !== defaults.ensemble.forecastDays) add('Njord__Ensemble__ForecastDays', String(config.ensemble.forecastDays))
  if (config.ensemble.models.join(',') !== defaults.ensemble.models.join(',')) config.ensemble.models.forEach((m, i) => add(`Njord__Ensemble__Models__${i}`, m))
  if (config.airQuality.forecastDays !== defaults.airQuality.forecastDays) add('Njord__AirQuality__ForecastDays', String(config.airQuality.forecastDays))
  if (config.airQuality.region !== defaults.airQuality.region) add('Njord__AirQuality__Region', config.airQuality.region)
  if (config.marine.forecastDays !== defaults.marine.forecastDays) add('Njord__Marine__ForecastDays', String(config.marine.forecastDays))
  if (config.flood.forecastDays !== defaults.flood.forecastDays) add('Njord__Flood__ForecastDays', String(config.flood.forecastDays))
  if (config.flood.includeEnsemble) add('Njord__Flood__IncludeEnsemble', 'true')
  for (const [key, enabled] of changedFeatures(config.features)) {
    add(`Njord__Features__${key}__Enabled`, String(enabled))
  }
  for (const [key, option, value] of changedOptions(config)) {
    const prefix = `Njord__Features__${key}__${option}`
    if (Array.isArray(value)) value.forEach((v, i) => add(`${prefix}__${i}`, String(v)))
    else add(prefix, String(value))
  }

  config.locations.forEach((loc, i) => {
    add(`Njord__Locations__${i}__Name`, loc.name)
    if (loc.latitude !== undefined) add(`Njord__Locations__${i}__Latitude`, String(loc.latitude))
    if (loc.longitude !== undefined) add(`Njord__Locations__${i}__Longitude`, String(loc.longitude))
  })

  if (config.solar.tilt !== defaults.solar.tilt) add('Njord__Solar__Tilt', String(config.solar.tilt))
  if (config.solar.azimuth !== defaults.solar.azimuth) add('Njord__Solar__Azimuth', String(config.solar.azimuth))

  if (config.budgetOverride) {
    add('Njord__Polling__BudgetOverride__RequestsPerMonth', String(config.budgetOverride.requestsPerMonth))
    add('Njord__Polling__BudgetOverride__RequestsPerMinute', String(config.budgetOverride.requestsPerMinute))
  }

  if (config.persistence.provider !== 'Sqlite') {
    add('Njord__Persistence__Provider', config.persistence.provider)
    if (config.persistence.connectionString) add('Njord__Persistence__ConnectionString', config.persistence.connectionString)
  }

  if (config.discoveryInterval !== defaults.discoveryInterval) {
    add('Njord__Polling__DiscoveryInterval', config.discoveryInterval)
  }
  if (config.dataPath !== defaults.dataPath) add('Njord__Persistence__DataPath', config.dataPath)

  return lines.join('\n')
}

export function exportAsCompose(config: NjordConfig): string {
  const envLines = exportAsEnvVars(config)
    .split('\n')
    .filter(l => l.trim())
    .map(l => `      - ${l}`)
    .join('\n')

  const hasLimits = config.deploy.limits.cpus || config.deploy.limits.memory
  const deployBlock = hasLimits ? `    deploy:
      resources:
        limits:
${config.deploy.limits.cpus ? `          cpus: "${config.deploy.limits.cpus}"\n` : ''}${config.deploy.limits.memory ? `          memory: ${config.deploy.limits.memory}\n` : ''}` : ''

  return `services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
      - "8081:8081"
${deployBlock}    volumes:
      - njord-data:/app/data
    environment:
${envLines}

volumes:
  njord-data:
`
}

export function importFromJson(json: string): Partial<NjordConfig> {
  const parsed = JSON.parse(json)
  const n = parsed.Njord ?? parsed
  const config: Partial<NjordConfig> = {}
  config.features = defaultFeatures()
  config.featureOptions = {}

  const pollInterval = n.Polling?.PollInterval
  const discoveryInterval = n.Polling?.DiscoveryInterval
  const budgetOverride = n.Polling?.BudgetOverride
  if (pollInterval) config.pollInterval = pollInterval
  if (n.Forecast) {
    config.forecast = {
      forecastDays: Number(n.Forecast.ForecastDays) || 4,
      pastHours: Number(n.Forecast.PastHours) || 0,
      models: Array.isArray(n.Forecast.Models) ? n.Forecast.Models.map(String) : [],
    }
  }
  if (n.Ensemble) {
    config.ensemble = {
      forecastDays: Number(n.Ensemble.ForecastDays) || 4,
      models: Array.isArray(n.Ensemble.Models) && n.Ensemble.Models.length
        ? n.Ensemble.Models.map(String)
        : defaultConfig().ensemble.models,
    }
  }
  if (n.Solar) {
    const d = defaultConfig().solar
    config.solar = {
      tilt: n.Solar.Tilt !== undefined ? Number(n.Solar.Tilt) : d.tilt,
      azimuth: n.Solar.Azimuth !== undefined ? Number(n.Solar.Azimuth) : d.azimuth,
    }
  }
  if (n.AirQuality) config.airQuality = { forecastDays: Number(n.AirQuality.ForecastDays) || 5, region: n.AirQuality.Region ?? 'Global' }
  if (n.Marine) config.marine = { forecastDays: Number(n.Marine.ForecastDays) || 8 }
  if (n.Flood) {
    config.flood = {
      forecastDays: Number(n.Flood.ForecastDays) || defaultConfig().flood.forecastDays,
      includeEnsemble: n.Flood.IncludeEnsemble === true,
    }
  }
  if (discoveryInterval) config.discoveryInterval = discoveryInterval
  if (n.Features && typeof n.Features === 'object') {
    for (const [name, settings] of Object.entries(n.Features as Record<string, unknown>)) {
      const key = parseFeatureKey(name)
      if (!key || !settings || typeof settings !== 'object') continue
      for (const [setting, value] of Object.entries(settings as Record<string, unknown>)) {
        if (setting.toLowerCase() === 'enabled') {
          config.features[key] = typeof value === 'boolean' ? value : parseBool(value)
          continue
        }
        const option = optionName(key, setting)
        const parsed = option ? parseOptionValue(key, option, value) : null
        if (option && parsed !== null) (config.featureOptions[key] ??= {})[option] = parsed
      }
    }
  }
  if (n.Locations) {
    config.locations = n.Locations.map((l: Record<string, unknown>) => ({
      name: l.Name as string ?? '',
      latitude: typeof l.Latitude === 'number' ? l.Latitude : 0,
      longitude: typeof l.Longitude === 'number' ? l.Longitude : 0,
    }))
  }
  if (budgetOverride) config.budgetOverride = budgetOverride
  if (n.Persistence) {
    config.persistence = { provider: n.Persistence.Provider ?? 'Sqlite', connectionString: n.Persistence.ConnectionString ?? '' }
    if (n.Persistence.DataPath) config.dataPath = n.Persistence.DataPath
  }

  return config
}

export function importFromEnvVars(text: string): Partial<NjordConfig> {
  const config = defaultConfig()
  let forecastModelsSeen = false
  let ensembleModelsSeen = false
  const lines = text.split('\n').filter(l => l.startsWith('Njord__'))

  for (const line of lines) {
    const eq = line.indexOf('=')
    if (eq < 0) continue
    const key = line.substring(0, eq)
    const value = line.substring(eq + 1).trim()
    const parts = key.split('__').slice(1)

    if (parts[0] === 'Polling' && parts[1] === 'PollInterval') config.pollInterval = value
    else if (parts[0] === 'Polling' && parts[1] === 'DiscoveryInterval') config.discoveryInterval = value
    else if (parts[0] === 'Polling' && parts[1] === 'BudgetOverride') {
      const budgetParts = parts.slice(2)
      const budget = config.budgetOverride ?? { requestsPerMonth: 0, requestsPerMinute: 0 }
      if (budgetParts[0] === 'RequestsPerMonth') budget.requestsPerMonth = parseInt(value) || 0
      else if (budgetParts[0] === 'RequestsPerMinute') budget.requestsPerMinute = parseInt(value) || 0
      config.budgetOverride = budget
    }
    else if (parts[0] === 'Forecast') {
      if (parts[1] === 'ForecastDays') config.forecast.forecastDays = parseInt(value) || 4
      else if (parts[1] === 'PastHours') config.forecast.pastHours = parseInt(value) || 0
      else if (parts[1] === 'Models' && value) {
        if (!forecastModelsSeen) { config.forecast.models = []; forecastModelsSeen = true }
        config.forecast.models[parseInt(parts[2]) || 0] = value
      }
    }
    else if (parts[0] === 'Ensemble') {
      if (parts[1] === 'ForecastDays') config.ensemble.forecastDays = parseInt(value) || 4
      else if (parts[1] === 'Models' && value) {
        if (!ensembleModelsSeen) { config.ensemble.models = []; ensembleModelsSeen = true }
        config.ensemble.models[parseInt(parts[2]) || 0] = value
      }
    }
    else if (parts[0] === 'Flood' && parts[1] === 'IncludeEnsemble') {
      config.flood.includeEnsemble = value.toLowerCase() === 'true'
    }
    else if (parts[0] === 'Solar') {
      const num = parseFloat(value)
      if (!Number.isNaN(num)) {
        if (parts[1] === 'Azimuth') config.solar.azimuth = num
        else if (parts[1] === 'Tilt') config.solar.tilt = num
      }
    }
    else if (parts[0] === 'AirQuality' && parts[1] === 'ForecastDays') config.airQuality.forecastDays = parseInt(value) || 5
    else if (parts[0] === 'AirQuality' && parts[1] === 'Region') config.airQuality.region = value
    else if (parts[0] === 'Marine' && parts[1] === 'ForecastDays') config.marine.forecastDays = parseInt(value) || 8
    else if (parts[0] === 'Flood' && parts[1] === 'ForecastDays') config.flood.forecastDays = parseInt(value) || 92
    else if (parts[0] === 'Features' && parts.length >= 3) {
      const key = parseFeatureKey(parts[1])
      if (!key) continue
      if (parts[2].toLowerCase() === 'enabled') {
        config.features[key] = parseBool(value)
        continue
      }
      const option = optionName(key, parts[2])
      if (!option) continue
      const parsed = parseOptionValue(key, option, value)
      if (parsed === null) continue
      if (typeof parsed === 'boolean') {
        ;(config.featureOptions[key] ??= {})[option] = parsed
        continue
      }
      const num = Number(value)
      if (Number.isNaN(num)) continue
      if (parts.length === 4 && /^\d+$/.test(parts[3])) {
        const current = config.featureOptions[key]?.[option]
        const arr = Array.isArray(current) ? current : []
        arr[Number(parts[3])] = num
        ;(config.featureOptions[key] ??= {})[option] = arr
      } else if (parts.length === 3) {
        ;(config.featureOptions[key] ??= {})[option] = num
      }
    }
    else if (parts[0] === 'Locations') {
      const idx = parseInt(parts[1])
      while (config.locations.length <= idx) config.locations.push({ name: '' })
      if (parts[2] === 'Name') config.locations[idx].name = value
      else if (parts[2] === 'Latitude') {
        const parsed = parseFloat(value)
        config.locations[idx].latitude = Number.isNaN(parsed) ? undefined : parsed
      }
      else if (parts[2] === 'Longitude') {
        const parsed = parseFloat(value)
        config.locations[idx].longitude = Number.isNaN(parsed) ? undefined : parsed
      }
    }
    else if (parts[0] === 'Persistence') {
      if (parts[1] === 'Provider') config.persistence.provider = value
      else if (parts[1] === 'ConnectionString') config.persistence.connectionString = value
      else if (parts[1] === 'DataPath') config.dataPath = value
    }
  }

  return config
}

export function importFromCompose(text: string): Partial<NjordConfig> {
  const cleaned = text
    .split('\n')
    .map(line => line.replace(/^\s*-\s+/, ''))
    .join('\n')
  const config = importFromEnvVars(cleaned)

  const cpusMatch = text.match(/cpus:\s*"?([0-9.]+)"?/)
  const memMatch = text.match(/memory:\s*(\d+[MmGg]?)/)
  if (cpusMatch || memMatch) {
    config.deploy = {
      limits: {
        cpus: cpusMatch?.[1] ?? '',
        memory: memMatch?.[1] ?? '',
      },
    }
  }

  return config
}

export function autoImport(text: string): Partial<NjordConfig> {
  const trimmed = text.trim()
  if (trimmed.startsWith('{')) return importFromJson(trimmed)
  if (/^\s*-\s+\w+__/m.test(trimmed)) return importFromCompose(trimmed)
  return importFromEnvVars(trimmed)
}

export function encodeHash(config: NjordConfig): string {
  return btoa(JSON.stringify(config))
}

export function decodeHash(hash: string): NjordConfig | null {
  try {
    return mergeWithDefaults(JSON.parse(atob(hash)))
  } catch {
    return null
  }
}
