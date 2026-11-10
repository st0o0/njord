import { FEATURES, requiredDomains, weatherVariableCount, airQualityVariableCount, marineVariableCount, floodVariableCount, seasonalVariableCount, type NjordConfig } from './types'

const MINUTES_PER_DAY = 24 * 60
const DAYS_PER_MONTH = 30
const GUARD_FACTOR = 0.8
const FREE_TIER_MONTHLY = 300_000
const FLOOD_ENSEMBLE_WEIGHT = 5
const SEASONAL_CALLS_PER_MONTH = 30
const SEASONAL_MINIMUM_REQUEST_WEIGHT = 1.3

export interface BudgetResult {
  domainCallsPerCycle: number
  cyclesPerDay: number
  cyclesPerMonth: number
  apiCallWeight: number
  callsPerDay: number
  projected: number
  limit: number
  guard: number
  percentUsed: number
  overBudget: boolean
}

// Mirrors BudgetGuard (Njord.Polling): every domain uses CallWeight.Of(varCount, days)
// = ceil(vars / 10) * (days / 14). Seasonal is 30 calls/month * max(weight, 1.3).
export function computeBudget(config: NjordConfig): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerDay = pollMinutes > 0 ? MINUTES_PER_DAY / pollMinutes : 0
  const cyclesPerMonth = cyclesPerDay * DAYS_PER_MONTH

  const domains = requiredDomains(config.features)
  const locationCount = Math.max(config.locations.length, 1)
  const domainCallsPerCycle = domains.filter(d => d !== 'Seasonal').length * locationCount
  const weightPerCycle = callsPerLocationCycle(config, config.features)
  const apiCallWeight = weightPerCycle

  const limit = config.budgetOverride?.requestsPerMonth ?? FREE_TIER_MONTHLY
  const guard = Math.round(limit * GUARD_FACTOR)
  const seasonalPerMonth = seasonalWeightPerMonth(config, config.features)
  const callsPerDay = Math.round(locationCount * weightPerCycle * cyclesPerDay + locationCount * seasonalPerMonth / DAYS_PER_MONTH)
  const projected = Math.round(locationCount * weightPerCycle * cyclesPerMonth + locationCount * seasonalPerMonth)
  const percentUsed = limit > 0 ? (projected / limit) * 100 : 0

  return {
    domainCallsPerCycle,
    cyclesPerDay: Math.round(cyclesPerDay * 10) / 10,
    cyclesPerMonth: Math.round(cyclesPerMonth),
    apiCallWeight,
    callsPerDay,
    projected,
    limit,
    guard,
    percentUsed,
    overBudget: projected > guard,
  }
}

function weatherWeight(config: NjordConfig, features: Record<string, boolean>): number {
  return requiredDomains(features).includes('Forecast')
    ? Math.ceil(weatherVariableCount(features) / 10) * (config.forecast.forecastDays / 14) * Math.max(1, config.forecast.models.filter(Boolean).length)
    : 0
}

function ensembleSpreadVarCount(features: Record<string, boolean>): number {
  const params = new Set<string>()
  for (const f of FEATURES) {
    if (!features[f.key]) continue
    f.ensembleParams?.forEach(p => { if (p !== 'Models') params.add(p) })
  }
  return params.size
}

function ensembleWeight(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  if (!domains.includes('Ensemble')) return 0
  const modelCount = Math.max(1, config.ensemble.models.filter(Boolean).length)
  // Several configured models are fetched together (passive comparison), so the weather variables ride along.
  const variables = modelCount > 1 ? weatherVariableCount(features) + ensembleSpreadVarCount(features) : ensembleSpreadVarCount(features)
  return Math.ceil(variables / 10) * (config.ensemble.forecastDays / 14) * modelCount
}

function callWeight(variableCount: number, forecastDays: number): number {
  return Math.ceil(variableCount / 10) * (forecastDays / 14)
}

function airQualityWeight(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  if (!domains.includes('AirQuality')) return 0
  return callWeight(airQualityVariableCount(features), config.airQuality.forecastDays)
}

function floodWeight(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  if (!domains.includes('Flood')) return 0
  return callWeight(floodVariableCount(features), config.flood.forecastDays)
    + (config.flood.includeEnsemble ? FLOOD_ENSEMBLE_WEIGHT - 1 : 0)
}

function marineWeight(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  if (!domains.includes('Marine')) return 0
  return callWeight(marineVariableCount(features), config.marine.forecastDays)
}

function seasonalWeightPerMonth(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  if (!domains.includes('Seasonal')) return 0
  const perRequest = Math.max(
    callWeight(seasonalVariableCount(features), config.seasonal.forecastMonths * 30),
    SEASONAL_MINIMUM_REQUEST_WEIGHT)
  return SEASONAL_CALLS_PER_MONTH * perRequest
}

function callsPerLocationCycle(config: NjordConfig, features: Record<string, boolean>): number {
  return weatherWeight(config, features) + ensembleWeight(config, features)
    + airQualityWeight(config, features) + floodWeight(config, features) + marineWeight(config, features)
}

// Marginal calls per day and location of enabling a feature (or saved by disabling it) given the other enabled
// features; 0 when everything it needs is already fetched. Uses the same formula as the budget itself.
export function featureCostPerDay(config: NjordConfig, key: string): number {
  const cycles = MINUTES_PER_DAY / (parseTimeSpan(config.pollInterval) || 60)
  const on = { ...config.features, [key]: true }
  const off = { ...config.features, [key]: false }
  const seasonalDelta = (seasonalWeightPerMonth(config, on) - seasonalWeightPerMonth(config, off)) / DAYS_PER_MONTH
  return Math.round((callsPerLocationCycle(config, on) - callsPerLocationCycle(config, off)) * cycles + seasonalDelta)
}

export function budgetLevel(percentUsed: number): 'ok' | 'warn' | 'over' {
  if (percentUsed > 80) return 'over'
  if (percentUsed > 60) return 'warn'
  return 'ok'
}

function parseTimeSpan(ts: string): number {
  const parts = ts.split(':').map(Number)
  if (parts.length === 3) return parts[0] * 60 + parts[1] + parts[2] / 60
  return 60
}
