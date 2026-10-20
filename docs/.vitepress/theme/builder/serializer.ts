import type { NjordConfig } from './types'
import { ENRICHMENT_TOGGLES, defaultConfig, defaultEnrichments } from './types'

// Only toggles that differ from their default are exported.
function changedEnrichments(enrichments: Record<string, boolean>): [string, boolean][] {
  const defaults = defaultEnrichments()
  return ENRICHMENT_TOGGLES
    .filter(t => (enrichments[t.key] ?? defaults[t.key]) !== defaults[t.key])
    .map(t => [t.key, enrichments[t.key]])
}

function parseEnrichmentKey(value: string): string | null {
  return ENRICHMENT_TOGGLES.find(t => t.key.toLowerCase() === value.toLowerCase())?.key ?? null
}

// Fills everything an import or a shared URL does not mention with defaults, nested objects included.
export function mergeWithDefaults(partial: Partial<NjordConfig>): NjordConfig {
  const d = defaultConfig()
  return {
    ...d,
    ...partial,
    enrichments: { ...defaultEnrichments(), ...partial.enrichments },
    weather: { ...d.weather, ...partial.weather },
    airQuality: { ...d.airQuality, ...partial.airQuality },
    marine: { ...d.marine, ...partial.marine },
    flood: { ...d.flood, ...partial.flood },
    mqtt: { ...d.mqtt, ...partial.mqtt },
    solarYield: { ...d.solarYield, ...partial.solarYield },
    persistence: { ...d.persistence, ...partial.persistence },
    deploy: { limits: { ...d.deploy.limits, ...partial.deploy?.limits } },
  }
}

function solarYieldEnabled(config: NjordConfig): boolean {
  return !!config.enrichments.SolarYield
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
  const weather: Record<string, unknown> = {}
  if (config.weather.forecastDays !== defaults.weather.forecastDays) weather.ForecastDays = config.weather.forecastDays
  if (config.weather.model !== defaults.weather.model) weather.Model = config.weather.model
  if (Object.keys(weather).length) n.Weather = weather
  if (config.airQuality.forecastDays !== defaults.airQuality.forecastDays) n.AirQuality = { ForecastDays: config.airQuality.forecastDays }
  if (config.marine.forecastDays !== defaults.marine.forecastDays) n.Marine = { ForecastDays: config.marine.forecastDays }
  if (config.flood.forecastDays !== defaults.flood.forecastDays) n.Flood = { ForecastDays: config.flood.forecastDays }
  if (solarYieldEnabled(config)) {
    const solar: Record<string, unknown> = {}
    if (config.solarYield.peakKw !== defaults.solarYield.peakKw) solar.PeakKw = config.solarYield.peakKw
    if (config.solarYield.azimuth !== defaults.solarYield.azimuth) solar.Azimuth = config.solarYield.azimuth
    if (config.solarYield.tilt !== defaults.solarYield.tilt) solar.Tilt = config.solarYield.tilt
    if (Object.keys(solar).length) n.SolarYield = solar
  }
  const changed = changedEnrichments(config.enrichments)
  if (changed.length) n.Enrichments = Object.fromEntries(changed)
  if (config.locations.length) {
    n.Locations = config.locations.map(l => {
      const loc: Record<string, unknown> = { Name: l.name, Latitude: l.latitude, Longitude: l.longitude }
      return loc
    })
  }

  const mqtt: Record<string, unknown> = { Enabled: config.mqtt.enabled }
  if (config.mqtt.enabled) {
    mqtt.Host = config.mqtt.host || '<your-mqtt-host>'
    mqtt.Port = config.mqtt.port
    if (config.mqtt.username) mqtt.Username = config.mqtt.username
  }
  n.Mqtt = mqtt
  if (config.mqtt.discoveryPrefix !== 'homeassistant') (n.Mqtt as Record<string, unknown>).DiscoveryPrefix = config.mqtt.discoveryPrefix
  if (config.mqtt.baseTopic !== 'njord') (n.Mqtt as Record<string, unknown>).BaseTopic = config.mqtt.baseTopic

  if (Object.keys(config.enrichment).length) {
    const enrichment: Record<string, unknown> = {}
    for (const [feature, settings] of Object.entries(config.enrichment)) {
      const featureObj: Record<string, unknown> = {}
      for (const [key, value] of Object.entries(settings as Record<string, unknown>)) {
        if (key === 'Enabled' && value === true) continue
        if (key.includes(':')) {
          const [section, prop] = key.split(':')
          if (!featureObj[section]) featureObj[section] = {}
          ;(featureObj[section] as Record<string, unknown>)[prop] = value
        } else {
          featureObj[key] = value
        }
      }
      if (Object.keys(featureObj).length) enrichment[feature] = featureObj
    }
    if (Object.keys(enrichment).length) n.Enrichment = enrichment
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
  if (config.weather.forecastDays !== defaults.weather.forecastDays) add('Njord__Weather__ForecastDays', String(config.weather.forecastDays))
  if (config.weather.model !== defaults.weather.model) add('Njord__Weather__Model', config.weather.model)
  if (config.airQuality.forecastDays !== defaults.airQuality.forecastDays) add('Njord__AirQuality__ForecastDays', String(config.airQuality.forecastDays))
  if (config.marine.forecastDays !== defaults.marine.forecastDays) add('Njord__Marine__ForecastDays', String(config.marine.forecastDays))
  if (config.flood.forecastDays !== defaults.flood.forecastDays) add('Njord__Flood__ForecastDays', String(config.flood.forecastDays))
  for (const [key, enabled] of changedEnrichments(config.enrichments)) {
    add(`Njord__Enrichments__${key}`, String(enabled))
  }

  config.locations.forEach((loc, i) => {
    add(`Njord__Locations__${i}__Name`, loc.name)
    if (loc.latitude !== undefined) add(`Njord__Locations__${i}__Latitude`, String(loc.latitude))
    if (loc.longitude !== undefined) add(`Njord__Locations__${i}__Longitude`, String(loc.longitude))
  })

  if (config.mqtt.enabled) {
    add('Njord__Mqtt__Enabled', 'true')
    add('Njord__Mqtt__Host', config.mqtt.host || '<your-mqtt-host>')
    add('Njord__Mqtt__Port', String(config.mqtt.port))
    if (config.mqtt.username) add('Njord__Mqtt__Username', config.mqtt.username)
    if (config.mqtt.password) add('Njord__Mqtt__Password', config.mqtt.password)
  }

  for (const [feature, settings] of Object.entries(config.enrichment)) {
    for (const [key, value] of Object.entries(settings as Record<string, unknown>)) {
      if (key === 'Enabled' && value === true) continue
      const envKey = key.includes(':') ? key.replace(':', '__') : key
      if (Array.isArray(value)) value.forEach((v, i) => add(`Njord__Enrichment__${feature}__${envKey}__${i}`, String(v)))
      else add(`Njord__Enrichment__${feature}__${envKey}`, String(value))
    }
  }

  if (solarYieldEnabled(config)) {
    if (config.solarYield.peakKw !== defaults.solarYield.peakKw) add('Njord__SolarYield__PeakKw', String(config.solarYield.peakKw))
    if (config.solarYield.azimuth !== defaults.solarYield.azimuth) add('Njord__SolarYield__Azimuth', String(config.solarYield.azimuth))
    if (config.solarYield.tilt !== defaults.solarYield.tilt) add('Njord__SolarYield__Tilt', String(config.solarYield.tilt))
  }

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
  config.enrichments = config.enrichments ?? defaultEnrichments()

  // Root-level keys of versions before 0.5.0 are still read (the export writes the new keys); the
  // import reports them through findOutdatedKeys.
  const pollInterval = n.Polling?.PollInterval ?? n.PollInterval
  const discoveryInterval = n.Polling?.DiscoveryInterval ?? n.DiscoveryInterval
  const budgetOverride = n.Polling?.BudgetOverride ?? n.BudgetOverride
  if (pollInterval) config.pollInterval = pollInterval
  if (n.Weather) {
    config.weather = {
      forecastDays: Number(n.Weather.ForecastDays) || 4,
      model: n.Weather.Model ?? defaultConfig().weather.model,
    }
  }
  if (n.SolarYield) {
    const d = defaultConfig().solarYield
    config.solarYield = {
      peakKw: Number(n.SolarYield.PeakKw) || d.peakKw,
      azimuth: n.SolarYield.Azimuth !== undefined ? Number(n.SolarYield.Azimuth) : d.azimuth,
      tilt: n.SolarYield.Tilt !== undefined ? Number(n.SolarYield.Tilt) : d.tilt,
    }
  }
  if (n.AirQuality) config.airQuality = { forecastDays: Number(n.AirQuality.ForecastDays) || 5 }
  if (n.Marine) config.marine = { forecastDays: Number(n.Marine.ForecastDays) || 8 }
  if (n.Flood) config.flood = { forecastDays: Number(n.Flood.ForecastDays) || 92 }
  if (discoveryInterval) config.discoveryInterval = discoveryInterval
  if (n.Enrichments && typeof n.Enrichments === 'object') {
    for (const [name, value] of Object.entries(n.Enrichments as Record<string, unknown>)) {
      const key = parseEnrichmentKey(name)
      if (key) config.enrichments[key] = typeof value === 'boolean' ? value : parseBool(value)
    }
  }
  if (n.Locations) {
    config.locations = n.Locations.map((l: Record<string, unknown>) => ({
      name: l.Name as string ?? '',
      latitude: typeof l.Latitude === 'number' ? l.Latitude : 0,
      longitude: typeof l.Longitude === 'number' ? l.Longitude : 0,
    }))
  }
  if (n.Mqtt) {
    config.mqtt = {
      enabled: n.Mqtt.Enabled ?? false,
      host: n.Mqtt.Host ?? '',
      port: n.Mqtt.Port ?? 1883,
      username: n.Mqtt.Username ?? '',
      password: '',
      discoveryPrefix: n.Mqtt.DiscoveryPrefix ?? 'homeassistant',
      baseTopic: n.Mqtt.BaseTopic ?? 'njord',
    }
  }
  if (n.Enrichment) config.enrichment = n.Enrichment
  if (budgetOverride) config.budgetOverride = budgetOverride
  if (n.Persistence) {
    config.persistence = { provider: n.Persistence.Provider ?? 'Sqlite', connectionString: n.Persistence.ConnectionString ?? '' }
    if (n.Persistence.DataPath) config.dataPath = n.Persistence.DataPath
  }

  return config
}

export function importFromEnvVars(text: string): Partial<NjordConfig> {
  const config = defaultConfig()
  const lines = text.split('\n').filter(l => l.startsWith('Njord__'))

  for (const line of lines) {
    const eq = line.indexOf('=')
    if (eq < 0) continue
    const key = line.substring(0, eq)
    const value = line.substring(eq + 1).trim()
    const parts = key.split('__').slice(1)

    // Root-level keys of versions before 0.5.0 are still read; findOutdatedKeys reports them.
    if (parts[0] === 'Polling' && parts[1] === 'PollInterval' || parts[0] === 'PollInterval') config.pollInterval = value
    else if (parts[0] === 'Polling' && parts[1] === 'DiscoveryInterval' || parts[0] === 'DiscoveryInterval') config.discoveryInterval = value
    else if (parts[0] === 'Polling' && parts[1] === 'BudgetOverride' || parts[0] === 'BudgetOverride') {
      const budgetParts = parts[0] === 'Polling' ? parts.slice(2) : parts.slice(1)
      const budget = config.budgetOverride ?? { requestsPerMonth: 0, requestsPerMinute: 0 }
      if (budgetParts[0] === 'RequestsPerMonth') budget.requestsPerMonth = parseInt(value) || 0
      else if (budgetParts[0] === 'RequestsPerMinute') budget.requestsPerMinute = parseInt(value) || 0
      config.budgetOverride = budget
    }
    else if (parts[0] === 'Weather') {
      if (parts[1] === 'ForecastDays') config.weather.forecastDays = parseInt(value) || 4
      else if (parts[1] === 'Model' && value) config.weather.model = value
    }
    else if (parts[0] === 'SolarYield') {
      const num = parseFloat(value)
      if (!Number.isNaN(num)) {
        if (parts[1] === 'PeakKw') config.solarYield.peakKw = num
        else if (parts[1] === 'Azimuth') config.solarYield.azimuth = num
        else if (parts[1] === 'Tilt') config.solarYield.tilt = num
      }
    }
    else if (parts[0] === 'AirQuality' && parts[1] === 'ForecastDays') config.airQuality.forecastDays = parseInt(value) || 5
    else if (parts[0] === 'Marine' && parts[1] === 'ForecastDays') config.marine.forecastDays = parseInt(value) || 8
    else if (parts[0] === 'Flood' && parts[1] === 'ForecastDays') config.flood.forecastDays = parseInt(value) || 92
    else if (parts[0] === 'Enrichments' && parts.length === 2) {
      const key = parseEnrichmentKey(parts[1])
      if (key) config.enrichments[key] = parseBool(value)
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
    else if (parts[0] === 'Mqtt') {
      if (parts[1] === 'Enabled') config.mqtt.enabled = value.toLowerCase() === 'true'
      else if (parts[1] === 'Host') config.mqtt.host = value
      else if (parts[1] === 'Port') config.mqtt.port = parseInt(value) || 1883
      else if (parts[1] === 'Username') config.mqtt.username = value
      else if (parts[1] === 'Password') config.mqtt.password = value
      else if (parts[1] === 'DiscoveryPrefix') config.mqtt.discoveryPrefix = value
      else if (parts[1] === 'BaseTopic') config.mqtt.baseTopic = value
    }
    else if (parts[0] === 'Enrichment' && parts.length >= 3) {
      const feature = parts[1]
      const setting = parts[2]
      if (!config.enrichment[feature]) config.enrichment[feature] = {}
      const asNum = Number(value)
      if (parts.length === 4 && /^\d+$/.test(parts[3])) {
        const arr = Array.isArray(config.enrichment[feature][setting]) ? config.enrichment[feature][setting] as unknown[] : []
        arr[Number(parts[3])] = asNum
        config.enrichment[feature][setting] = arr
        continue
      }
      config.enrichment[feature][setting] = value === 'true' ? true : value === 'false' ? false : !isNaN(asNum) ? asNum : value
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

// Root-level keys that moved in 0.5.0. njord refuses to start while one of them is configured.
const OUTDATED_KEYS: { old: string; current: string }[] = [
  { old: 'PollInterval', current: 'Polling__PollInterval' },
  { old: 'DiscoveryInterval', current: 'Polling__DiscoveryInterval' },
  { old: 'BudgetOverride', current: 'Polling__BudgetOverride' },
  { old: 'PersistencePath', current: 'Persistence__DataPath' },
]

// EnabledDomains was replaced by enrichment toggles: the fetched domains are derived from the enabled enrichments.
const REMOVED_KEYS = ['EnabledDomains']

export function findOutdatedKeys(text: string): string[] {
  const trimmed = text.trim()
  const present = new Set<string>()
  const removed = new Set<string>()
  if (trimmed.startsWith('{')) {
    let parsed: Record<string, Record<string, unknown>> | null = null
    try {
      parsed = JSON.parse(trimmed)
    } catch {
      return []
    }
    const n = (parsed?.Njord ?? parsed) as Record<string, unknown>
    for (const key of OUTDATED_KEYS) if (n && key.old in n) present.add(key.old)
    for (const key of REMOVED_KEYS) if (n && key in n) removed.add(key)
  } else {
    for (const line of trimmed.split('\n')) {
      const name = line.replace(/^\s*-\s+/, '').split('=')[0].trim()
      for (const key of OUTDATED_KEYS) {
        if (name === `Njord__${key.old}` || name.startsWith(`Njord__${key.old}__`)) present.add(key.old)
      }
      for (const key of REMOVED_KEYS) {
        if (name === `Njord__${key}` || name.startsWith(`Njord__${key}__`)) removed.add(key)
      }
    }
  }
  return [
    ...OUTDATED_KEYS
      .filter(key => present.has(key.old))
      .map(key => `Njord__${key.old} is outdated since 0.5.0, use Njord__${key.current} (JSON: Njord:${key.current.replace('__', ':')})`),
    ...REMOVED_KEYS
      .filter(key => removed.has(key))
      .map(key => `Njord__${key} was removed, the fetched domains follow the enabled enrichments: use Njord__Enrichments__<Name> (JSON: Njord:Enrichments)`),
  ]
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
