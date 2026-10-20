import { requiredDomains, weatherVariableCount, type NjordConfig } from './types'

const MINUTES_PER_DAY = 24 * 60
const DAYS_PER_MONTH = 30
const GUARD_FACTOR = 0.8
const FREE_TIER_MONTHLY = 300_000
const FLOOD_ENSEMBLE_WEIGHT = 5
// The seasonal domain learns a roughly daily update cycle, so it costs about one call per location and day.
const SEASONAL_CALLS_PER_DAY = 1

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

// Mirrors BudgetGuard (Njord.Polling): per location and poll cycle the Weather domain costs
// ceil(variables / 10) * ceil(forecastDays / 14) calls (variables = union of the enabled features' weather
// requirements), every other required domain costs one call.
export function computeBudget(config: NjordConfig): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerDay = pollMinutes > 0 ? MINUTES_PER_DAY / pollMinutes : 0
  const cyclesPerMonth = cyclesPerDay * DAYS_PER_MONTH

  const domains = requiredDomains(config.features)
  const apiCallWeight = weatherWeight(config, config.features)
  const domainCallsPerCycle = domains.filter(d => d !== 'Seasonal').length * config.locations.length
  const callsPerCycle = config.locations.length * callsPerLocationCycle(config, config.features)

  const limit = config.budgetOverride?.requestsPerMonth ?? FREE_TIER_MONTHLY
  const guard = Math.round(limit * GUARD_FACTOR)
  const extraPerDay = domains.includes('Seasonal') ? config.locations.length * SEASONAL_CALLS_PER_DAY : 0
  const callsPerDay = Math.round(callsPerCycle * cyclesPerDay + extraPerDay)
  const projected = Math.round(callsPerCycle * cyclesPerMonth + extraPerDay * DAYS_PER_MONTH)
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
  return requiredDomains(features).includes('Weather')
    ? Math.ceil(weatherVariableCount(features) / 10) * Math.ceil(config.weather.forecastDays / 14)
    : 0
}

function callsPerLocationCycle(config: NjordConfig, features: Record<string, boolean>): number {
  const domains = requiredDomains(features)
  const otherDomains = domains.filter(d => d !== 'Weather' && d !== 'Seasonal').length
  const floodExtraWeight = domains.includes('Flood') && config.flood.includeEnsemble ? FLOOD_ENSEMBLE_WEIGHT - 1 : 0
  return weatherWeight(config, features) + otherDomains + floodExtraWeight
}

// Marginal calls per day and location of enabling a feature (or saved by disabling it) given the other enabled
// features; 0 when everything it needs is already fetched. Uses the same formula as the budget itself.
export function featureCostPerDay(config: NjordConfig, key: string): number {
  const cycles = MINUTES_PER_DAY / (parseTimeSpan(config.pollInterval) || 60)
  const on = { ...config.features, [key]: true }
  const off = { ...config.features, [key]: false }
  const seasonal = (requiredDomains(on).includes('Seasonal') ? SEASONAL_CALLS_PER_DAY : 0) - (requiredDomains(off).includes('Seasonal') ? SEASONAL_CALLS_PER_DAY : 0)
  return Math.round((callsPerLocationCycle(config, on) - callsPerLocationCycle(config, off)) * cycles) + seasonal
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
