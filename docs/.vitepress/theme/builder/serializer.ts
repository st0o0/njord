import type { NjordConfig } from './types'
import { defaultConfig } from './types'

export function exportAsJson(config: NjordConfig): string {
  const out: Record<string, unknown> = { Njord: {} }
  const n = out.Njord as Record<string, unknown>

  n.PollInterval = config.pollInterval
  n.ForecastDays = config.forecastDays
  if (config.discoveryInterval !== '00:20:00') n.DiscoveryInterval = config.discoveryInterval
  n.Horizons = config.horizons
  if (config.models.length) n.Models = config.models
  if (config.locations.length) {
    n.Locations = config.locations.map(l => {
      const loc: Record<string, unknown> = { Name: l.name, Latitude: l.latitude, Longitude: l.longitude }
      if (l.models.length) loc.Models = l.models
      return loc
    })
  }
  const defaults = defaultConfig()
  if (JSON.stringify(config.parameters) !== JSON.stringify(defaults.parameters)) {
    n.Parameters = { Groups: config.parameters.groups }
    if (config.parameters.extra.length) (n.Parameters as Record<string, unknown>).Extra = config.parameters.extra
    if (config.parameters.exclude.length) (n.Parameters as Record<string, unknown>).Exclude = config.parameters.exclude
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

  if (Object.keys(config.enrichment).length) n.Enrichment = config.enrichment
  if (config.budgetOverride) n.BudgetOverride = config.budgetOverride
  if (config.persistence.provider !== 'Sqlite') {
    n.Persistence = { Provider: config.persistence.provider, ConnectionString: config.persistence.connectionString }
  }

  return JSON.stringify(out, null, 2)
}

export function exportAsEnvVars(config: NjordConfig): string {
  const lines: string[] = []
  const add = (key: string, value: string) => lines.push(`${key}=${value}`)

  add('Njord__PollInterval', config.pollInterval)
  add('Njord__ForecastDays', String(config.forecastDays))
  config.horizons.forEach((h, i) => add(`Njord__Horizons__${i}`, String(h)))
  config.models.forEach((m, i) => add(`Njord__Models__${i}`, m))

  config.locations.forEach((loc, i) => {
    add(`Njord__Locations__${i}__Name`, loc.name)
    add(`Njord__Locations__${i}__Latitude`, String(loc.latitude))
    add(`Njord__Locations__${i}__Longitude`, String(loc.longitude))
    loc.models.forEach((m, j) => add(`Njord__Locations__${i}__Models__${j}`, m))
  })

  config.parameters.groups.forEach((g, i) => add(`Njord__Parameters__Groups__${i}`, g))

  if (config.mqtt.enabled) {
    add('Njord__Mqtt__Enabled', 'true')
    add('Njord__Mqtt__Host', config.mqtt.host || '<your-mqtt-host>')
    add('Njord__Mqtt__Port', String(config.mqtt.port))
    if (config.mqtt.username) add('Njord__Mqtt__Username', config.mqtt.username)
    if (config.mqtt.password) add('Njord__Mqtt__Password', config.mqtt.password)
  }

  for (const [feature, settings] of Object.entries(config.enrichment)) {
    for (const [key, value] of Object.entries(settings as Record<string, unknown>)) {
      add(`Njord__Enrichment__${feature}__${key}`, String(value))
    }
  }

  if (config.budgetOverride) {
    add('Njord__BudgetOverride__RequestsPerMonth', String(config.budgetOverride.requestsPerMonth))
    add('Njord__BudgetOverride__RequestsPerMinute', String(config.budgetOverride.requestsPerMinute))
  }

  if (config.persistence.provider !== 'Sqlite') {
    add('Njord__Persistence__Provider', config.persistence.provider)
    if (config.persistence.connectionString) add('Njord__Persistence__ConnectionString', config.persistence.connectionString)
  }

  if (config.discoveryInterval !== '00:20:00') {
    add('Njord__DiscoveryInterval', config.discoveryInterval)
  }

  return lines.join('\n')
}

export function exportAsCompose(config: NjordConfig): string {
  const envLines = exportAsEnvVars(config)
    .split('\n')
    .filter(l => l.trim())
    .map(l => `      - ${l}`)
    .join('\n')

  return `services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    volumes:
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

  if (n.PollInterval) config.pollInterval = n.PollInterval
  if (n.ForecastDays) config.forecastDays = n.ForecastDays
  if (n.DiscoveryInterval) config.discoveryInterval = n.DiscoveryInterval
  if (n.Horizons) config.horizons = n.Horizons
  if (n.Models) config.models = n.Models
  if (n.Locations) {
    config.locations = n.Locations.map((l: Record<string, unknown>) => ({
      name: l.Name as string ?? '',
      latitude: l.Latitude as number ?? 0,
      longitude: l.Longitude as number ?? 0,
      models: (l.Models as string[]) ?? [],
    }))
  }
  if (n.Parameters) {
    config.parameters = {
      groups: (n.Parameters.Groups as string[]) ?? ['Weather'],
      extra: (n.Parameters.Extra as string[]) ?? [],
      exclude: (n.Parameters.Exclude as string[]) ?? [],
    }
  }
  if (n.Mqtt) {
    config.mqtt = {
      enabled: n.Mqtt.Enabled ?? false,
      host: n.Mqtt.Host ?? '',
      port: n.Mqtt.Port ?? 1883,
      username: n.Mqtt.Username ?? '',
      password: '',
      discoveryPrefix: n.Mqtt.DiscoveryPrefix ?? 'homeassistant',
      discoveryEnabled: n.Mqtt.DiscoveryEnabled ?? true,
      baseTopic: n.Mqtt.BaseTopic ?? 'njord',
    }
  }
  if (n.Enrichment) config.enrichment = n.Enrichment
  if (n.BudgetOverride) config.budgetOverride = n.BudgetOverride
  if (n.Persistence) config.persistence = { provider: n.Persistence.Provider ?? 'Sqlite', connectionString: n.Persistence.ConnectionString ?? '' }

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

    if (parts[0] === 'PollInterval') config.pollInterval = value
    else if (parts[0] === 'ForecastDays') config.forecastDays = parseInt(value) || 4
    else if (parts[0] === 'Horizons') config.horizons[parseInt(parts[1]) || 0] = parseInt(value)
    else if (parts[0] === 'Models' && parts.length === 2) {
      const idx = parseInt(parts[1])
      while (config.models.length <= idx) config.models.push('')
      config.models[idx] = value
    }
    else if (parts[0] === 'Locations') {
      const idx = parseInt(parts[1])
      while (config.locations.length <= idx) config.locations.push({ name: '', latitude: 0, longitude: 0, models: [] })
      if (parts[2] === 'Name') config.locations[idx].name = value
      else if (parts[2] === 'Latitude') config.locations[idx].latitude = parseFloat(value) || 0
      else if (parts[2] === 'Longitude') config.locations[idx].longitude = parseFloat(value) || 0
      else if (parts[2] === 'Models') {
        const mIdx = parseInt(parts[3])
        while (config.locations[idx].models.length <= mIdx) config.locations[idx].models.push('')
        config.locations[idx].models[mIdx] = value
      }
    }
    else if (parts[0] === 'Mqtt') {
      if (parts[1] === 'Host') config.mqtt.host = value
      else if (parts[1] === 'Port') config.mqtt.port = parseInt(value) || 1883
      else if (parts[1] === 'Username') config.mqtt.username = value
    }
    else if (parts[0] === 'Parameters' && parts[1] === 'Groups') {
      const idx = parseInt(parts[2])
      while (config.parameters.groups.length <= idx) config.parameters.groups.push('')
      config.parameters.groups[idx] = value
    }
    else if (parts[0] === 'Enrichment' && parts.length >= 3) {
      const feature = parts[1]
      const setting = parts[2]
      if (!config.enrichment[feature]) config.enrichment[feature] = {}
      const asNum = Number(value)
      config.enrichment[feature][setting] = value === 'true' ? true : value === 'false' ? false : !isNaN(asNum) ? asNum : value
    }
    else if (parts[0] === 'Mqtt' && parts[1] === 'Enabled') {
      config.mqtt.enabled = value === 'true'
    }
    else if (parts[0] === 'DiscoveryInterval') config.discoveryInterval = value
    else if (parts[0] === 'Persistence') {
      if (parts[1] === 'Provider') config.persistence.provider = value
      else if (parts[1] === 'ConnectionString') config.persistence.connectionString = value
    }
  }

  config.models = config.models.filter(Boolean)
  config.parameters.groups = config.parameters.groups.filter(Boolean)

  return config
}

export function importFromCompose(text: string): Partial<NjordConfig> {
  const cleaned = text
    .split('\n')
    .map(line => line.replace(/^\s*-\s+/, ''))
    .join('\n')
  return importFromEnvVars(cleaned)
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

export function decodeHash(hash: string): Partial<NjordConfig> | null {
  try {
    return JSON.parse(atob(hash))
  } catch {
    return null
  }
}
