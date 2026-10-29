import featureData from '../../../data/features.json'

export const DOMAINS = ['Forecast', 'Ensemble', 'AirQuality', 'Flood', 'Marine', 'Seasonal', 'Satellite'] as const

export type DomainName = (typeof DOMAINS)[number]

export const CATEGORIES = [
  { key: 'Forecast', label: 'Forecast Alerts' },
  { key: 'AirQuality', label: 'Air quality' },
  { key: 'Lifestyle', label: 'Lifestyle' },
  { key: 'Insight', label: 'Insight' },
  { key: 'Energy', label: 'Energy' },
  { key: 'Flood', label: 'Flood' },
  { key: 'Marine', label: 'Marine' },
] as const

export interface FeatureEntry {
  key: string
  name: string
  category: string
  output: 'Alert' | 'Score' | 'Condition' | 'Trend' | 'Forecast'
  description: string
  longDescription?: string
  entities?: string[]
  enabledByDefault: boolean
  domains: DomainName[]
  weatherParams?: string[]
  optionalWeatherParams?: string[]
  airQualityParams?: string[]
  ensembleParams?: string[]
  marineParams?: string[]
  optionalAirQualityParams?: string[]
  optionalMarineParams?: string[]
  options?: FeatureOption[]
}

export interface FeatureGroup {
  key: string
  label: string
  icon: string
  features: FeatureEntry[]
}

export const USER_GROUPS: { key: string; label: string; icon: string; categories: string[]; outputs?: string[] }[] = [
  { key: 'safety', label: 'Safety Alerts', icon: '🛡️', categories: ['Forecast'], outputs: ['Alert'] },
  { key: 'airquality', label: 'Air Quality', icon: '🌿', categories: ['AirQuality'] },
  { key: 'lifestyle', label: 'Outdoor & Lifestyle', icon: '☀️', categories: ['Lifestyle'] },
  { key: 'energy', label: 'Energy', icon: '⚡', categories: ['Energy'] },
  { key: 'flood-marine', label: 'Flood & Marine', icon: '🌊', categories: ['Flood', 'Marine'] },
  { key: 'insights', label: 'Insights & Forecasts', icon: '📊', categories: ['Insight', 'Forecast'], outputs: ['Forecast', 'Trend', 'Condition'] },
]

// A per-feature setting below Njord:Features:<Name>; null/absent in the config means the built-in default.
export interface FeatureOption {
  name: string
  type: 'number' | 'numberArray'
  default: number | number[]
}

export type OptionValue = number | number[]

// docs/data/features.json mirrors the registered FeatureRules (descriptor requirements plus the options of each
// feature config class) and is maintained by hand together with them (see the njord-enrichment-feature skill).
// Config keys are Njord:Features:<name>:Enabled and Njord:Features:<name>:<option>.
export const FEATURES: FeatureEntry[] = featureData as FeatureEntry[]

export function featureLabel(key: string): string {
  return key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace('Pm25', 'PM2.5')
}

export function defaultFeatures(): Record<string, boolean> {
  return Object.fromEntries(FEATURES.map(f => [f.key, f.enabledByDefault]))
}

export function userFeatureGroups(): FeatureGroup[] {
  return USER_GROUPS.map(g => ({
    key: g.key,
    label: g.label,
    icon: g.icon,
    features: FEATURES.filter(f => {
      if (g.key === 'safety') return f.category === 'Forecast' && f.output === 'Alert'
      if (g.key === 'insights') return (f.category === 'Insight') || (f.category === 'Forecast' && (f.output === 'Forecast' || f.output === 'Trend' || f.output === 'Condition'))
      return g.categories.includes(f.category) && !(g.outputs && !g.outputs.includes(f.output))
    }),
  })).filter(g => g.features.length > 0)
}

export function recommendedFeatures(): Record<string, boolean> {
  const recommended = new Set([
    'Frost', 'Heat', 'Storm', 'HeavyRain',
    'Uv', 'Fog', 'Snow', 'PressureDrop',
    'Thunderstorm', 'Ice', 'WindChill', 'Visibility',
    'TropicalNight', 'Humidity', 'RainProbability',
    'Pollen', 'Pm25',
    'Commute',
  ])
  return Object.fromEntries(FEATURES.map(f => [f.key, recommended.has(f.key)]))
}

export function entityCount(features: Record<string, boolean>, locationCount: number): number {
  let count = 0
  for (const f of FEATURES) {
    if (!features[f.key]) continue
    count += f.entities?.length ?? 1
  }
  return count * Math.max(locationCount, 1) + 4 + 2 + 1
}

export function requiredDomains(features: Record<string, boolean>): DomainName[] {
  const result = new Set<DomainName>()
  for (const f of FEATURES) {
    if (features[f.key]) f.domains.forEach(d => result.add(d))
  }
  return DOMAINS.filter(d => result.has(d))
}

export function weatherVariableCount(features: Record<string, boolean>): number {
  const params = new Set<string>()
  for (const f of FEATURES) {
    if (!features[f.key]) continue
    f.weatherParams?.forEach(p => params.add(p))
    f.optionalWeatherParams?.forEach(p => params.add(p))
  }
  // weather_code is always requested when the weather domain is required (base parameter)
  if (params.size > 0) params.add('WeatherCode')
  return params.size
}

export interface LocationConfig {
  name: string
  latitude: number
  longitude: number
}

export interface DeployLimits {
  cpus: string
  memory: string
}

export interface NjordConfig {
  pollInterval: string
  forecast: { forecastDays: number; pastHours: number; models: string[] }
  ensemble: { forecastDays: number; models: string[] }
  airQuality: { forecastDays: number; region: string }
  marine: { forecastDays: number }
  flood: { forecastDays: number; includeEnsemble: boolean }
  discoveryInterval: string
  features: Record<string, boolean>
  locations: LocationConfig[]
  // Only values that differ from the feature default: featureOptions['Frost'].Thresholds = [0, -5, -15]
  featureOptions: Record<string, Record<string, OptionValue>>
  budgetOverride: { requestsPerMonth: number; requestsPerMinute: number } | null
  persistence: { provider: string; connectionString: string }
  dataPath: string
  solarYield: { peakKw: number; azimuth: number; tilt: number }
  deploy: { limits: DeployLimits }
}

// Mirrors the defaults of src/Njord/appsettings.json (scalars) and the option classes (lists);
// the builder omits every value that equals its default, so keep these in sync with the shipped file.
export function defaultConfig(): NjordConfig {
  return {
    pollInterval: '01:00:00',
    forecast: { forecastDays: 4, pastHours: 0, models: [] },
    ensemble: { forecastDays: 4, models: ['ecmwf_ifs025_ensemble_mean'] },
    airQuality: { forecastDays: 5, region: 'Global' },
    marine: { forecastDays: 8 },
    flood: { forecastDays: 92, includeEnsemble: false },
    discoveryInterval: '00:20:00',
    features: defaultFeatures(),
    locations: [],
    featureOptions: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    dataPath: 'data/njord.db',
    solarYield: { peakKw: 5.0, azimuth: 180, tilt: 30 },
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
