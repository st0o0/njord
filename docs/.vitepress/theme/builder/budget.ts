import { requiredDomains, weatherVariableCount, type NjordConfig } from './types'

const MINUTES_PER_DAY = 24 * 60
const DAYS_PER_MONTH = 30
const GUARD_FACTOR = 0.8
const FREE_TIER_MONTHLY = 300_000
const FLOOD_ENSEMBLE_WEIGHT = 5

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

// Mirrors BudgetCalculator.Validate: per location and poll cycle the Weather domain costs
// ceil(variables / 10) * ceil(forecastDays / 14) calls (variables = union of the enabled handlers' weather
// requirements), every other required domain costs one call.
export function computeBudget(config: NjordConfig): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerDay = pollMinutes > 0 ? MINUTES_PER_DAY / pollMinutes : 0
  const cyclesPerMonth = cyclesPerDay * DAYS_PER_MONTH

  const domains = requiredDomains(config.enrichments)
  const weatherRequired = domains.includes('Weather')
  const apiCallWeight = weatherRequired
    ? Math.ceil(weatherVariableCount(config.enrichments) / 10) * Math.ceil(config.weather.forecastDays / 14)
    : 0
  const otherDomains = domains.filter(d => d !== 'Weather').length

  const domainCallsPerCycle = domains.length * config.locations.length
  const floodExtraWeight = domains.includes('Flood') && config.flood.includeEnsemble ? FLOOD_ENSEMBLE_WEIGHT - 1 : 0
  const callsPerCycle = config.locations.length * (apiCallWeight + otherDomains + floodExtraWeight)

  const limit = config.budgetOverride?.requestsPerMonth ?? FREE_TIER_MONTHLY
  const guard = Math.round(limit * GUARD_FACTOR)
  const callsPerDay = Math.round(callsPerCycle * cyclesPerDay)
  const projected = Math.round(callsPerCycle * cyclesPerMonth)
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

function parseTimeSpan(ts: string): number {
  const parts = ts.split(':').map(Number)
  if (parts.length === 3) return parts[0] * 60 + parts[1] + parts[2] / 60
  return 60
}
