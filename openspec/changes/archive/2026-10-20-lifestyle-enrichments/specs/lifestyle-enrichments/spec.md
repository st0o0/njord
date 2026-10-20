## ADDED Requirements

### Requirement: Ventilation window advises when to open windows
The VentilationWindowHandler SHALL scan the next 24h for contiguous hours where PM2.5 < 15, pollen is low, temperature is 15–28°C, and precipitation probability is below 20%. It SHALL emit a VentilationAdvised event with the best window start and end times.

#### Scenario: Good air quality and comfortable temperature
- **WHEN** the next 24h have a 4-hour block with PM2.5 = 8, no pollen, temp = 22°C, no rain
- **THEN** VentilationAdvised SHALL be emitted with BestStart and BestEnd spanning those 4 hours

#### Scenario: Poor air quality blocks ventilation
- **WHEN** PM2.5 exceeds 15 for all hours in the next 24h
- **THEN** no VentilationAdvised event SHALL be emitted

### Requirement: Exercise advisory rates outdoor exercise safety
The ExerciseAdvisoryHandler SHALL score the next 12h on air quality (PM2.5, ozone) and weather comfort (temperature, humidity, UV). It SHALL emit ExerciseAdvised with level Safe (≥70), Caution (≥45), or Avoid (<45), plus reasons and best window.

#### Scenario: Clean air and mild weather
- **WHEN** PM2.5 = 5, ozone = 30, temp = 20°C, humidity = 50%, UV = 3
- **THEN** ExerciseAdvised SHALL have level "Safe"

#### Scenario: High PM2.5 triggers caution
- **WHEN** PM2.5 = 40, other conditions mild
- **THEN** ExerciseAdvised SHALL have level "Caution" or "Avoid" with PM2.5 in reasons

### Requirement: Laundry score rates outdoor drying conditions
The LaundryScoreHandler SHALL compute a 0–100 score from temperature, humidity, wind, precipitation probability, and sunshine. It SHALL emit LaundryScoreComputed with score, best start time, and best consecutive hours.

#### Scenario: Warm dry windy day
- **WHEN** temp = 28°C, humidity = 35%, wind = 5 m/s, precip = 0%, sunshine = 100%
- **THEN** LaundryScoreComputed SHALL have score ≥ 80

### Requirement: Commute safety checks near-term travel conditions
The CommuteSafetyHandler SHALL check the next 2h for ice risk, fog, heavy rain, and strong wind. It SHALL emit CommuteSafety with level Safe/Caution/Dangerous and a list of identified risks.

#### Scenario: Icy conditions
- **WHEN** temp = 0°C and dew point = -1°C in the next 2h
- **THEN** CommuteSafety SHALL have level "Dangerous" with "Ice" in risks

### Requirement: Pollen trend tracks direction of change
The PollenTrendHandler SHALL compare current pollen values with the previous forecast cycle. It SHALL emit PollenTrendComputed with Rising/Falling/Stable per pollen type and optional peak timing.

#### Scenario: Birch pollen rising
- **WHEN** current birch pollen = 80 and previous = 40
- **THEN** PollenTrendComputed SHALL report birch as "Rising"

### Requirement: Night ventilation recommends passive cooling windows
The NightVentilationHandler SHALL check if tomorrow's peak exceeds 28°C and tonight's minimum is below 20°C. If so, it SHALL emit NightVentilationAdvised with open and close times.

#### Scenario: Hot day follows cool night
- **WHEN** tomorrow peak = 34°C and tonight low = 16°C
- **THEN** NightVentilationAdvised SHALL be emitted with the cool-hour window

### Requirement: Confidence trend tracks ensemble spread direction
The ConfidenceTrendHandler SHALL compare average ensemble StdDev between current and previous forecast. It SHALL emit ConfidenceTrend with direction Converging (>20% decrease), Diverging (>20% increase), or Stable.

#### Scenario: Spread narrowing
- **WHEN** current average StdDev = 1.5 and previous = 2.5
- **THEN** ConfidenceTrend SHALL have direction "Converging"
