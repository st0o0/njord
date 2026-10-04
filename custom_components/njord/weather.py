"""Weather platform for njord."""

from __future__ import annotations

from datetime import UTC, datetime, timedelta

from homeassistant.components.weather import (
    Forecast,
    SingleCoordinatorWeatherEntity,
    WeatherEntity,
    WeatherEntityFeature,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import (
    UnitOfPrecipitationDepth,
    UnitOfPressure,
    UnitOfSpeed,
    UnitOfTemperature,
)
from homeassistant.core import HomeAssistant, callback
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.event import async_track_utc_time_change

from .condition_mapper import map_condition
from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator
from .helpers import device_info
from .horizon import current_horizon_offset
from .models import (
    ConsensusData,
    ForecastData,
    HorizonConsensusData,
    HourlyForecastData,
    ModelMetricsData,
    NjordLocation,
)


def _get_sw_version(hass: HomeAssistant, entry: ConfigEntry) -> str | None:
    status_coordinator: NjordStatusCoordinator | None = hass.data[DOMAIN][entry.entry_id].get("status_coordinator")
    if status_coordinator is not None and status_coordinator.data is not None:
        return status_coordinator.data.version or None
    return None


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up njord weather entities."""
    coordinator: NjordDataCoordinator = hass.data[DOMAIN][entry.entry_id]["coordinator"]
    sw_version = _get_sw_version(hass, entry)

    entities: list[WeatherEntity] = []
    for location, model in coordinator.data.forecasts:
        entities.append(NjordWeatherEntity(coordinator, entry, location, model, sw_version))

    disabled_groups: list[str] = entry.options.get("disabled_enrichment_groups", [])
    active = coordinator.data.active_enrichments
    if (active is None or "consensus" in active) and "consensus" not in disabled_groups:
        locations = {loc for loc, _ in coordinator.data.forecasts}
        for location in sorted(locations):
            enrichment = coordinator.data.enrichments.get(location)
            if enrichment and enrichment.consensus:
                entities.append(NjordConsensusWeatherEntity(coordinator, entry, location, sw_version))

    async_add_entities(entities)

    def weather_factory(location: NjordLocation) -> list[WeatherEntity]:
        new_entities: list[WeatherEntity] = []
        for model in location.models:
            new_entities.append(NjordWeatherEntity(coordinator, entry, location.name, model, sw_version))
        act = coordinator.data.active_enrichments
        if (act is None or "consensus" in act) and "consensus" not in disabled_groups:
            enrichment = coordinator.data.enrichments.get(location.name)
            if enrichment and enrichment.consensus:
                new_entities.append(NjordConsensusWeatherEntity(coordinator, entry, location.name, sw_version))
        return new_entities

    coordinator.register_entity_factory("weather", async_add_entities, weather_factory)


class NjordWeatherEntity(SingleCoordinatorWeatherEntity[NjordDataCoordinator]):
    """Weather entity for a njord location×model pair."""

    _attr_has_entity_name = True
    _attr_native_temperature_unit = UnitOfTemperature.CELSIUS
    _attr_native_pressure_unit = UnitOfPressure.HPA
    _attr_native_wind_speed_unit = UnitOfSpeed.METERS_PER_SECOND
    _attr_native_precipitation_unit = UnitOfPrecipitationDepth.MILLIMETERS

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        model: str,
        sw_version: str | None = None,
    ) -> None:
        """Initialize the weather entity."""
        super().__init__(coordinator)
        self._location = location
        self._model = model

        slug = f"{location}_{model}".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        info = coordinator.data.model_info.get(model)
        self._attr_name = info.display_name if info and info.display_name else model
        self._attr_device_info = device_info(entry, location, sw_version)

        data = coordinator.data.forecasts.get((location, model))
        features = WeatherEntityFeature(0)
        if data and data.hourly:
            features |= WeatherEntityFeature.FORECAST_HOURLY
        if data and data.daily:
            features |= WeatherEntityFeature.FORECAST_DAILY
        self._attr_supported_features = features

    async def async_added_to_hass(self) -> None:
        await super().async_added_to_hass()
        self.async_on_remove(async_track_utc_time_change(self.hass, self._async_hourly_refresh, minute=0, second=0))

    @callback
    def _async_hourly_refresh(self, now: datetime) -> None:
        self.async_write_ha_state()

    @property
    def available(self) -> bool:
        return self.coordinator.data is not None and (self._location, self._model) in self.coordinator.data.forecasts

    @property
    def _forecast_data(self) -> ForecastData | None:
        """Get current forecast data from coordinator."""
        if self.coordinator.data is None:
            return None
        return self.coordinator.data.forecasts.get((self._location, self._model))

    def _current_hourly(self) -> HourlyForecastData | None:
        data = self._forecast_data
        if data is None or not data.hourly:
            return None
        now = datetime.now(UTC)
        best = None
        for h in data.hourly:
            if h.valid_at <= now:
                best = h
        return best

    @property
    def condition(self) -> str | None:
        """Return the current condition."""
        h = self._current_hourly()
        if h is None or h.weather_code is None:
            return None
        return map_condition(h.weather_code, h.is_day if h.is_day is not None else True)

    @property
    def native_temperature(self) -> float | None:
        h = self._current_hourly()
        return h.temperature if h is not None else None

    @property
    def humidity(self) -> float | None:
        h = self._current_hourly()
        return h.humidity if h is not None else None

    @property
    def native_pressure(self) -> float | None:
        h = self._current_hourly()
        return h.pressure_msl if h is not None else None

    @property
    def native_wind_speed(self) -> float | None:
        h = self._current_hourly()
        return h.wind_speed if h is not None else None

    @property
    def wind_bearing(self) -> float | None:
        h = self._current_hourly()
        return h.wind_bearing if h is not None else None

    @property
    def native_apparent_temperature(self) -> float | None:
        h = self._current_hourly()
        return h.apparent_temperature if h is not None else None

    @property
    def cloud_cover(self) -> float | None:
        h = self._current_hourly()
        return h.cloud_cover if h is not None else None

    def _model_metrics(self) -> ModelMetricsData | None:
        if self.coordinator.data is None:
            return None
        enrichment = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None or enrichment.history is None:
            return None
        for m in enrichment.history.models:
            if m.model == self._model:
                return m
        return None

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        attrs: dict[str, object] = {}

        h = self._current_hourly()
        if h is not None and h.extra:
            attrs.update(h.extra)

        metrics = self._model_metrics()
        if metrics is not None:
            if metrics.mae_7d is not None:
                attrs["model_mae_7d"] = round(metrics.mae_7d, 2)
            if metrics.mae_30d is not None:
                attrs["model_mae_30d"] = round(metrics.mae_30d, 2)
            attrs["model_weight"] = round(metrics.weight, 3)
            if metrics.drift is not None:
                attrs["model_drift"] = round(metrics.drift, 2)

        if self.coordinator.data is not None:
            info = self.coordinator.data.model_info.get(self._model)
            if info is not None:
                if info.display_name:
                    attrs["model_display_name"] = info.display_name
                if info.provider:
                    attrs["model_provider"] = info.provider
                if info.region:
                    attrs["model_region"] = info.region
                if info.coverage_tier and info.coverage_tier != "unspecified":
                    attrs["model_coverage_tier"] = info.coverage_tier
                if info.resolution_km is not None:
                    attrs["model_resolution_km"] = info.resolution_km
                if info.max_forecast_hours is not None:
                    attrs["model_max_forecast_hours"] = info.max_forecast_hours
                if info.description:
                    attrs["model_description"] = info.description

        return attrs or None

    @callback
    def _async_forecast_hourly(self) -> list[Forecast] | None:
        data = self._forecast_data
        if data is None:
            return None

        now = datetime.now(UTC)
        forecasts: list[Forecast] = []
        for h in data.hourly:
            if h.valid_at < now:
                continue
            condition = None
            if h.weather_code is not None:
                condition = map_condition(h.weather_code, h.is_day if h.is_day is not None else True)

            entry = Forecast(
                datetime=h.valid_at.isoformat(),
                native_temperature=h.temperature,
                precipitation=h.precipitation,
                humidity=h.humidity,
                native_wind_speed=h.wind_speed,
                wind_bearing=h.wind_bearing,
                cloud_cover=h.cloud_cover,
                condition=condition,
            )
            if h.extra:
                entry.update(h.extra)
            forecasts.append(entry)
        return forecasts or None

    def _daily_condition_from_hourly(self, date_str: str) -> str | None:
        """Derive daily condition from midday hourly entry as fallback."""
        data = self._forecast_data
        if data is None or not data.hourly:
            return None
        midday = None
        for h in data.hourly:
            if h.valid_at.strftime("%Y-%m-%d") == date_str:
                if midday is None or abs(h.valid_at.hour - 12) < abs(midday.valid_at.hour - 12):
                    midday = h
        if midday is None or midday.weather_code is None:
            return None
        return map_condition(midday.weather_code, True)

    @callback
    def _async_forecast_daily(self) -> list[Forecast] | None:
        data = self._forecast_data
        if data is None or not data.daily:
            return None

        today = datetime.now(UTC).date()
        forecasts: list[Forecast] = []
        for d in data.daily:
            if datetime.strptime(d.date, "%Y-%m-%d").date() < today:
                continue
            condition = None
            if d.weather_code is not None:
                condition = map_condition(d.weather_code, True)
            else:
                condition = self._daily_condition_from_hourly(d.date)

            entry = Forecast(
                datetime=f"{d.date}T00:00:00+00:00",
                native_temperature=d.temperature_max,
                native_templow=d.temperature_min,
                precipitation=d.precipitation_sum,
                native_wind_speed=d.wind_speed_max,
                condition=condition,
            )
            if d.extra:
                entry.update(d.extra)
            forecasts.append(entry)
        return forecasts or None


_CONSENSUS_PARAM_MAP = {
    "temperature_2m": "temperature",
    "apparent_temperature": "apparent_temperature",
    "relative_humidity_2m": "humidity",
    "pressure_msl": "pressure",
    "wind_speed_10m": "wind_speed",
    "wind_direction_10m": "wind_bearing",
    "cloud_cover": "cloud_cover",
    "precipitation": "precipitation",
    "weather_code": "weather_code",
    "dew_point_2m": "dew_point",
    "visibility": "visibility",
}


_AGREEMENT_THRESHOLD = 0.5


class NjordConsensusWeatherEntity(SingleCoordinatorWeatherEntity[NjordDataCoordinator]):
    """Weather entity using multi-model consensus values with hourly granularity."""

    _attr_has_entity_name = True
    _attr_native_temperature_unit = UnitOfTemperature.CELSIUS
    _attr_native_pressure_unit = UnitOfPressure.HPA
    _attr_native_wind_speed_unit = UnitOfSpeed.METERS_PER_SECOND
    _attr_native_precipitation_unit = UnitOfPrecipitationDepth.MILLIMETERS

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._location = location

        slug = f"{location}_consensus".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Consensus"
        self._attr_device_info = device_info(entry, location, sw_version)

        features = WeatherEntityFeature(0)
        if self._sorted_horizons() and len(self._sorted_horizons()) >= 2:
            features |= WeatherEntityFeature.FORECAST_HOURLY | WeatherEntityFeature.FORECAST_DAILY
        self._attr_supported_features = features

    async def async_added_to_hass(self) -> None:
        await super().async_added_to_hass()
        self.async_on_remove(async_track_utc_time_change(self.hass, self._async_hourly_refresh, minute=0, second=0))

    @callback
    def _async_hourly_refresh(self, now: datetime) -> None:
        self.async_write_ha_state()

    def _consensus(self) -> ConsensusData | None:
        if self.coordinator.data is None:
            return None
        enrichment = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None:
            return None
        return enrichment.consensus

    def _current_horizon_offset(self) -> int:
        if self.coordinator.data is None:
            return 0
        enrichment = self.coordinator.data.enrichments.get(self._location)
        if enrichment is None:
            return 0
        return current_horizon_offset(enrichment.consensus_updated_at)

    def _sorted_horizons(self) -> list[str]:
        consensus = self._consensus()
        if consensus is None:
            return []
        for param in consensus.hourly_parameters:
            if param.parameter == "temperature_2m":
                return sorted(
                    [h.horizon for h in param.by_horizon],
                    key=lambda x: int(x[1:]),
                )
        return []

    def _horizon_values(self, horizon: str) -> dict[str, float | None]:
        consensus = self._consensus()
        if consensus is None:
            return {}
        result: dict[str, float | None] = {}
        for param in consensus.hourly_parameters:
            for h in param.by_horizon:
                if h.horizon == horizon:
                    result[param.parameter] = h.median
                    break
        return result

    def _get_horizon_value(self, parameter: str, horizon: str | None = None) -> float | None:
        if horizon is None:
            horizon = f"h{self._current_horizon_offset()}"
        consensus = self._consensus()
        if consensus is None:
            return None
        for param in consensus.hourly_parameters:
            if param.parameter == parameter:
                for h in param.by_horizon:
                    if h.horizon == horizon:
                        return h.median
        return None

    def _get_horizon_data(self, parameter: str, horizon: str | None = None) -> HorizonConsensusData | None:
        if horizon is None:
            horizon = f"h{self._current_horizon_offset()}"
        consensus = self._consensus()
        if consensus is None:
            return None
        for param in consensus.hourly_parameters:
            if param.parameter == parameter:
                for h in param.by_horizon:
                    if h.horizon == horizon:
                        return h
        return None

    @property
    def available(self) -> bool:
        return self._consensus() is not None

    @property
    def condition(self) -> str | None:
        wmo = self._get_horizon_value("weather_code")
        if wmo is None:
            return None
        is_day_val = self._get_horizon_value("is_day")
        is_day = is_day_val is not None and is_day_val >= 0.5
        return map_condition(int(round(wmo)), is_day)

    @property
    def native_temperature(self) -> float | None:
        return self._get_horizon_value("temperature_2m")

    @property
    def humidity(self) -> float | None:
        return self._get_horizon_value("relative_humidity_2m")

    @property
    def native_pressure(self) -> float | None:
        return self._get_horizon_value("pressure_msl")

    @property
    def native_wind_speed(self) -> float | None:
        return self._get_horizon_value("wind_speed_10m")

    @property
    def wind_bearing(self) -> float | None:
        return self._get_horizon_value("wind_direction_10m")

    @property
    def native_apparent_temperature(self) -> float | None:
        return self._get_horizon_value("apparent_temperature")

    @property
    def cloud_cover(self) -> float | None:
        return self._get_horizon_value("cloud_cover")

    def _reliable_hours(self) -> int:
        consensus = self._consensus()
        if consensus is None:
            return 0
        temp_param = None
        for param in consensus.hourly_parameters:
            if param.parameter == "temperature_2m":
                temp_param = param
                break
        if temp_param is None:
            return 0
        offset = self._current_horizon_offset()
        by_hours = sorted(temp_param.by_horizon, key=lambda h: int(h.horizon[1:]))
        count = 0
        for h in by_hours:
            if int(h.horizon[1:]) < offset:
                continue
            if h.agreement is None or h.agreement < _AGREEMENT_THRESHOLD:
                break
            count += 1
        return count

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        temp_h = self._get_horizon_data("temperature_2m")
        if temp_h is None:
            return None
        offset = self._current_horizon_offset()
        attrs: dict[str, object] = {
            "agreement": temp_h.agreement,
            "available_models": temp_h.available_models,
            "spread": temp_h.spread,
            "reliable_hours": self._reliable_hours(),
            "current_horizon": f"h{offset}",
        }
        if self.coordinator.data is not None:
            enrichment = self.coordinator.data.enrichments.get(self._location)
            if enrichment is not None and enrichment.consensus_updated_at is not None:
                attrs["consensus_age_hours"] = offset
        return attrs

    @callback
    def _async_forecast_hourly(self) -> list[Forecast] | None:
        consensus = self._consensus()
        if consensus is None:
            return None

        offset = self._current_horizon_offset()
        now = datetime.now(UTC).replace(minute=0, second=0, microsecond=0)
        forecasts: list[Forecast] = []

        for horizon in self._sorted_horizons():
            hours = int(horizon[1:])
            if hours <= offset:
                continue
            vals = self._horizon_values(horizon)
            if not vals:
                continue

            hours_from_now = hours - offset
            forecast_time = now + timedelta(hours=hours_from_now)
            condition = None
            wmo = vals.get("weather_code")
            if wmo is not None:
                condition = map_condition(int(round(wmo)), True)

            forecasts.append(
                Forecast(
                    datetime=forecast_time.isoformat(),
                    native_temperature=vals.get("temperature_2m"),
                    precipitation=vals.get("precipitation"),
                    humidity=vals.get("relative_humidity_2m"),
                    native_wind_speed=vals.get("wind_speed_10m"),
                    wind_bearing=vals.get("wind_direction_10m"),
                    cloud_cover=vals.get("cloud_cover"),
                    condition=condition,
                )
            )
        return forecasts

    def _daily_horizon_values(self, horizon: str) -> dict[str, float | None]:
        consensus = self._consensus()
        if consensus is None:
            return {}
        result: dict[str, float | None] = {}
        for param in consensus.daily_parameters:
            for h in param.by_horizon:
                if h.horizon == horizon:
                    result[param.parameter] = h.median
                    break
        return result

    def _sorted_daily_horizons(self) -> list[str]:
        consensus = self._consensus()
        if consensus is None:
            return []
        horizons: set[str] = set()
        for param in consensus.daily_parameters:
            for h in param.by_horizon:
                horizons.add(h.horizon)
        return sorted(horizons, key=lambda x: int(x[1:]))

    @callback
    def _async_forecast_daily(self) -> list[Forecast] | None:
        consensus = self._consensus()
        if consensus is None or not consensus.daily_parameters:
            return None

        today = datetime.now(UTC).date()
        forecasts: list[Forecast] = []

        for horizon in self._sorted_daily_horizons():
            day_offset = int(horizon[1:])
            forecast_date = today + timedelta(days=day_offset)
            vals = self._daily_horizon_values(horizon)
            if not vals:
                continue

            condition = None
            wmo = vals.get("weather_code")
            if wmo is not None:
                condition = map_condition(int(round(wmo)), True)

            forecasts.append(
                Forecast(
                    datetime=f"{forecast_date.isoformat()}T00:00:00+00:00",
                    native_temperature=vals.get("temperature_2m_max"),
                    native_templow=vals.get("temperature_2m_min"),
                    precipitation=vals.get("precipitation_sum"),
                    native_wind_speed=vals.get("wind_speed_10m_max"),
                    condition=condition,
                )
            )
        return forecasts or None
