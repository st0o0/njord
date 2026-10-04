"""Sensor platform for njord enrichment data."""

from __future__ import annotations

from homeassistant.components.sensor import SensorDeviceClass, SensorEntity, SensorStateClass
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import (
    PERCENTAGE,
    UnitOfLength,
    UnitOfPressure,
    UnitOfSpeed,
    UnitOfTemperature,
    UnitOfTime,
)
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity import EntityCategory
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator
from .helpers import device_info, server_device_info
from .horizon import current_horizon_offset, get_horizon_entry
from .models import AlertData, EnrichmentData, HorizonDerivedData, NjordLocation

ALERT_TYPES = [
    "frost",
    "heat",
    "storm",
    "heavy_rain",
    "uv",
    "fog",
    "snow",
    "pressure_drop",
    "thunderstorm",
    "ice",
    "wind_chill",
    "visibility",
    "tropical_night",
    "humidity",
]

ALERT_NAMES = {
    "frost": "Frost Alert",
    "heat": "Heat Alert",
    "storm": "Storm Alert",
    "heavy_rain": "Heavy Rain Alert",
    "uv": "UV Alert",
    "fog": "Fog Alert",
    "snow": "Snow Alert",
    "pressure_drop": "Pressure Drop Alert",
    "thunderstorm": "Thunderstorm Alert",
    "ice": "Ice Alert",
    "wind_chill": "Wind Chill Alert",
    "visibility": "Visibility Alert",
    "tropical_night": "Tropical Night Alert",
    "humidity": "Humidity Alert",
}

ALERT_UNITS: dict[str, str] = {
    "frost": UnitOfTemperature.CELSIUS,
    "heat": UnitOfTemperature.CELSIUS,
    "storm": UnitOfSpeed.METERS_PER_SECOND,
    "heavy_rain": "mm",
    "uv": "UV",
    "fog": UnitOfLength.METERS,
    "snow": UnitOfLength.CENTIMETERS,
    "pressure_drop": UnitOfPressure.HPA,
    "thunderstorm": UnitOfSpeed.METERS_PER_SECOND,
    "ice": UnitOfTemperature.CELSIUS,
    "wind_chill": UnitOfTemperature.CELSIUS,
    "visibility": UnitOfLength.METERS,
    "tropical_night": UnitOfTemperature.CELSIUS,
    "humidity": PERCENTAGE,
}

ALERT_DEVICE_CLASSES: dict[str, SensorDeviceClass] = {
    "frost": SensorDeviceClass.TEMPERATURE,
    "heat": SensorDeviceClass.TEMPERATURE,
    "storm": SensorDeviceClass.WIND_SPEED,
    "thunderstorm": SensorDeviceClass.WIND_SPEED,
    "pressure_drop": SensorDeviceClass.PRESSURE,
    "fog": SensorDeviceClass.DISTANCE,
    "snow": SensorDeviceClass.DISTANCE,
    "ice": SensorDeviceClass.TEMPERATURE,
    "wind_chill": SensorDeviceClass.TEMPERATURE,
    "visibility": SensorDeviceClass.DISTANCE,
    "tropical_night": SensorDeviceClass.TEMPERATURE,
    "humidity": SensorDeviceClass.HUMIDITY,
}

ALERT_PRECISION: dict[str, int] = {
    "frost": 1,
    "heat": 1,
    "storm": 0,
    "heavy_rain": 1,
    "uv": 0,
    "fog": 0,
    "snow": 0,
    "pressure_drop": 0,
    "thunderstorm": 0,
    "ice": 1,
    "wind_chill": 1,
    "visibility": 0,
    "tropical_night": 1,
    "humidity": 0,
}

ALERT_ICONS = {
    "frost": "mdi:snowflake-alert",
    "heat": "mdi:thermometer-alert",
    "storm": "mdi:weather-hurricane",
    "heavy_rain": "mdi:weather-pouring",
    "uv": "mdi:sun-wireless",
    "fog": "mdi:weather-fog",
    "snow": "mdi:snowflake",
    "pressure_drop": "mdi:gauge-low",
    "thunderstorm": "mdi:weather-lightning",
    "ice": "mdi:snowflake-thermometer",
    "wind_chill": "mdi:thermometer-minus",
    "visibility": "mdi:eye-off",
    "tropical_night": "mdi:weather-night",
    "humidity": "mdi:water-percent",
}

INDEX_TYPES = [
    ("laundry", "Laundry Index", "mdi:tshirt-crew"),
    ("outdoor", "Outdoor Index", "mdi:pine-tree"),
    ("running", "Running Index", "mdi:run"),
    ("cycling", "Cycling Index", "mdi:bike"),
    ("bbq", "BBQ Index", "mdi:grill"),
    ("irrigation", "Irrigation Index", "mdi:sprinkler"),
    ("solar", "Solar Index", "mdi:solar-power"),
    ("night_ventilation", "Night Ventilation Index", "mdi:air-filter"),
]


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
    """Set up njord sensor entities."""
    coordinator: NjordDataCoordinator = hass.data[DOMAIN][entry.entry_id]["coordinator"]
    sw_version = _get_sw_version(hass, entry)
    disabled_groups: list[str] = entry.options.get("disabled_enrichment_groups", [])

    entities: list[SensorEntity] = []
    locations = {loc for loc, _ in coordinator.data.forecasts}
    active = coordinator.data.active_enrichments

    for location in sorted(locations):
        if (active is None or "alerts" in active) and "alerts" not in disabled_groups:
            for alert_type in ALERT_TYPES:
                entities.append(NjordAlertSensor(coordinator, entry, location, alert_type, sw_version))

        if (active is None or "indices" in active) and "indices" not in disabled_groups:
            for key, name, icon in INDEX_TYPES:
                entities.append(NjordIndexSensor(coordinator, entry, location, key, name, icon, sw_version))
            entities.append(NjordVpdSensor(coordinator, entry, location, sw_version))
            entities.append(NjordFrostHoursSensor(coordinator, entry, location, sw_version))
            entities.append(NjordFrostConfidenceSensor(coordinator, entry, location, sw_version))

        if (active is None or "trends" in active) and "trends" not in disabled_groups:
            entities.append(NjordTrendSensor(coordinator, entry, location, sw_version))

        if (active is None or "derived" in active) and "derived" not in disabled_groups:
            entities.append(NjordSunshineSensor(coordinator, entry, location, sw_version))
            entities.append(NjordDiurnalAmplitudeSensor(coordinator, entry, location, sw_version))
            entities.append(NjordBeaufortSensor(coordinator, entry, location, sw_version))
            entities.append(NjordWindChillSensor(coordinator, entry, location, sw_version))
            entities.append(NjordDewpointComfortSensor(coordinator, entry, location, sw_version))

        if (active is None or "history" in active) and "history" not in disabled_groups:
            entities.append(NjordModelPerformanceSensor(coordinator, entry, location, sw_version))

    status_coordinator: NjordStatusCoordinator | None = hass.data[DOMAIN][entry.entry_id].get("status_coordinator")
    if status_coordinator is not None:
        entities.append(NjordMonthlyUsageSensor(status_coordinator, entry, sw_version))
        entities.append(NjordDailyUsageSensor(status_coordinator, entry, sw_version))
        entities.append(NjordVersionSensor(status_coordinator, entry, sw_version))
        entities.append(NjordUptimeSensor(status_coordinator, entry, sw_version))

        if status_coordinator.data is not None:
            for target in status_coordinator.data.targets:
                entities.append(NjordTargetSensor(status_coordinator, entry, target.location, target.model, sw_version))

    async_add_entities(entities)

    def sensor_factory(location: NjordLocation) -> list[SensorEntity]:
        act = coordinator.data.active_enrichments
        new_entities: list[SensorEntity] = []
        if (act is None or "alerts" in act) and "alerts" not in disabled_groups:
            for alert_type in ALERT_TYPES:
                new_entities.append(NjordAlertSensor(coordinator, entry, location.name, alert_type, sw_version))
        if (act is None or "indices" in act) and "indices" not in disabled_groups:
            for key, name, icon in INDEX_TYPES:
                new_entities.append(NjordIndexSensor(coordinator, entry, location.name, key, name, icon, sw_version))
            new_entities.append(NjordVpdSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordFrostHoursSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordFrostConfidenceSensor(coordinator, entry, location.name, sw_version))
        if (act is None or "trends" in act) and "trends" not in disabled_groups:
            new_entities.append(NjordTrendSensor(coordinator, entry, location.name, sw_version))
        if (act is None or "derived" in act) and "derived" not in disabled_groups:
            new_entities.append(NjordSunshineSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordDiurnalAmplitudeSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordBeaufortSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordWindChillSensor(coordinator, entry, location.name, sw_version))
            new_entities.append(NjordDewpointComfortSensor(coordinator, entry, location.name, sw_version))
        if (act is None or "history" in act) and "history" not in disabled_groups:
            new_entities.append(NjordModelPerformanceSensor(coordinator, entry, location.name, sw_version))
        return new_entities

    coordinator.register_entity_factory("sensor", async_add_entities, sensor_factory)


class _NjordEnrichmentSensor(CoordinatorEntity[NjordDataCoordinator], SensorEntity):
    """Base class for enrichment sensors."""

    _attr_has_entity_name = True
    _attr_entity_registry_enabled_default = False

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._location = location
        self._attr_device_info = device_info(entry, location, sw_version)

    def _enrichment(self) -> EnrichmentData | None:
        if self.coordinator.data is None:
            return None
        return self.coordinator.data.enrichments.get(self._location)


class NjordAlertSensor(_NjordEnrichmentSensor):
    """Sensor for a weather alert showing the trigger value."""

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        alert_type: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        self._alert_type = alert_type
        slug = f"{location}_{alert_type}_alert".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = ALERT_NAMES.get(alert_type, f"{alert_type} Alert")
        self._attr_icon = ALERT_ICONS.get(alert_type, "mdi:alert")
        self._attr_native_unit_of_measurement = ALERT_UNITS.get(alert_type)
        device_class = ALERT_DEVICE_CLASSES.get(alert_type)
        if device_class is not None:
            self._attr_device_class = device_class
            self._attr_state_class = SensorStateClass.MEASUREMENT
        precision = ALERT_PRECISION.get(alert_type)
        if precision is not None:
            self._attr_suggested_display_precision = precision

    def _get_alert(self) -> AlertData | None:
        enrichment = self._enrichment()
        if enrichment is None:
            return None
        for alert in enrichment.alerts:
            if alert.type == self._alert_type:
                return alert
        return None

    @property
    def available(self) -> bool:
        return self._enrichment() is not None

    @property
    def native_value(self) -> float | None:
        alert = self._get_alert()
        if alert is None:
            return None
        return alert.trigger_value

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        alert = self._get_alert()
        if alert is None:
            return None
        attrs: dict[str, object] = {
            "severity": alert.severity,
            "confidence": alert.confidence,
            "threshold": alert.threshold,
        }
        if alert.peak_value is not None:
            attrs["peak_value"] = alert.peak_value
        if alert.hours_until is not None:
            attrs["hours_until"] = alert.hours_until
        if alert.duration_hours is not None:
            attrs["duration_hours"] = alert.duration_hours
        return attrs


class NjordIndexSensor(_NjordEnrichmentSensor):
    """Sensor for an activity index (0-100)."""

    _attr_native_unit_of_measurement = "%"
    _attr_suggested_display_precision = 0

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        index_key: str,
        index_name: str,
        icon: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        self._index_key = index_key
        slug = f"{location}_{index_key}_index".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_translation_key = f"{index_key}_index"
        self._attr_name = index_name
        self._attr_icon = icon

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.indices is not None

    @property
    def native_value(self) -> int | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None:
            return None
        return getattr(enrichment.indices, self._index_key, None)

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None:
            return None
        forecast = [
            {"day_offset": d.day_offset, "score": getattr(d, self._index_key, 0)} for d in enrichment.indices.forecast
        ]
        return {"forecast": forecast}


class NjordVpdSensor(_NjordEnrichmentSensor):
    """Sensor for Vapour Pressure Deficit."""

    _attr_native_unit_of_measurement = "kPa"
    _attr_suggested_display_precision = 2
    _attr_icon = "mdi:water-percent"
    _attr_translation_key = "vpd"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_vpd".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "VPD"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.indices is not None and enrichment.indices.vpd is not None

    @property
    def native_value(self) -> float | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None or enrichment.indices.vpd is None:
            return None
        return enrichment.indices.vpd.kpa

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None or enrichment.indices.vpd is None:
            return None
        return {"category": enrichment.indices.vpd.category}


class NjordTrendSensor(_NjordEnrichmentSensor):
    """Sensor for weather trend stability."""

    _attr_icon = "mdi:trending-up"
    _attr_translation_key = "weather_trend"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_weather_trend".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Weather Trend"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.trends is not None

    @property
    def native_value(self) -> str | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.trends is None:
            return None
        return enrichment.trends.weather_change_description

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.trends is None:
            return None
        t = enrichment.trends
        attrs: dict[str, object] = {}
        if t.stability_label is not None:
            attrs["stability_label"] = t.stability_label
        if t.precip_starts_in_hours is not None:
            attrs["precip_starts_in_hours"] = t.precip_starts_in_hours
        if t.precip_ends_in_hours is not None:
            attrs["precip_ends_in_hours"] = t.precip_ends_in_hours
        if t.temp_max_in_hours is not None:
            attrs["temp_max_in_hours"] = t.temp_max_in_hours
        if t.temp_min_in_hours is not None:
            attrs["temp_min_in_hours"] = t.temp_min_in_hours
        if t.reliable_hours is not None:
            attrs["reliable_hours"] = t.reliable_hours
        if t.stability_ratio is not None:
            attrs["stability_ratio"] = t.stability_ratio
        if t.decay_rate is not None:
            attrs["decay_rate"] = t.decay_rate
        if t.parameter_trends:
            attrs["parameter_trends"] = [
                {"parameter": p.parameter, "direction": p.direction, "delta": p.delta} for p in t.parameter_trends
            ]
        return attrs if attrs else None


class NjordSunshineSensor(_NjordEnrichmentSensor):
    """Sensor for sunshine percentage."""

    _attr_native_unit_of_measurement = "%"
    _attr_suggested_display_precision = 0
    _attr_icon = "mdi:white-balance-sunny"
    _attr_translation_key = "sunshine"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_sunshine".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Sunshine"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.derived is not None

    @property
    def native_value(self) -> float | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.derived is None:
            return None
        return enrichment.derived.sunshine_pct


class NjordDiurnalAmplitudeSensor(_NjordEnrichmentSensor):
    """Sensor for diurnal temperature amplitude."""

    _attr_device_class = SensorDeviceClass.TEMPERATURE
    _attr_native_unit_of_measurement = UnitOfTemperature.CELSIUS
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:thermometer-lines"
    _attr_translation_key = "diurnal_amplitude"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_diurnal_amplitude".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Diurnal Amplitude"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.derived is not None

    @property
    def native_value(self) -> float | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.derived is None:
            return None
        return enrichment.derived.diurnal_amplitude


class NjordModelPerformanceSensor(_NjordEnrichmentSensor):
    """Diagnostic sensor for model performance."""

    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_device_class = SensorDeviceClass.TEMPERATURE
    _attr_native_unit_of_measurement = UnitOfTemperature.CELSIUS
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:chart-line"
    _attr_translation_key = "model_performance"

    def __init__(
        self,
        coordinator: NjordDataCoordinator,
        entry: ConfigEntry,
        location: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_model_performance".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Model Performance"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.history is not None

    @property
    def native_value(self) -> float | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.history is None:
            return None
        return enrichment.history.weighted_temperature

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.history is None:
            return None
        h = enrichment.history
        attrs: dict[str, object] = {
            "models": [
                {
                    "model": m.model,
                    "mae_7d": m.mae_7d,
                    "mae_30d": m.mae_30d,
                    "weight": m.weight,
                    "drift": m.drift,
                }
                for m in h.models
            ],
        }
        if h.seasonal_best is not None:
            attrs["seasonal_best"] = h.seasonal_best
        if h.anomaly is not None:
            attrs["anomaly"] = h.anomaly
        if h.anomaly_deviation is not None:
            attrs["anomaly_deviation"] = h.anomaly_deviation
        return attrs


class NjordFrostHoursSensor(_NjordEnrichmentSensor):
    """Sensor for frost hours."""

    _attr_device_class = SensorDeviceClass.DURATION
    _attr_native_unit_of_measurement = UnitOfTime.HOURS
    _attr_suggested_display_precision = 0
    _attr_icon = "mdi:snowflake-thermometer"
    _attr_translation_key = "frost_hours"

    def __init__(self, coordinator, entry, location, sw_version=None):
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_frost_hours".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Frost Hours"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.indices is not None and enrichment.indices.frost is not None

    @property
    def native_value(self) -> int | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None or enrichment.indices.frost is None:
            return None
        return enrichment.indices.frost.hours_until


class NjordFrostConfidenceSensor(_NjordEnrichmentSensor):
    """Sensor for frost confidence as percentage."""

    _attr_native_unit_of_measurement = "%"
    _attr_suggested_display_precision = 0
    _attr_icon = "mdi:snowflake-check"
    _attr_translation_key = "frost_confidence"

    def __init__(self, coordinator, entry, location, sw_version=None):
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_frost_confidence".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Frost Confidence"

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.indices is not None and enrichment.indices.frost is not None

    @property
    def native_value(self) -> float | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.indices is None or enrichment.indices.frost is None:
            return None
        return enrichment.indices.frost.confidence * 100


class NjordMonthlyUsageSensor(CoordinatorEntity[NjordStatusCoordinator], SensorEntity):
    """Diagnostic sensor for monthly API usage percentage."""

    _attr_has_entity_name = True
    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_native_unit_of_measurement = "%"
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:calendar-month"
    _attr_state_class = SensorStateClass.MEASUREMENT

    def __init__(self, coordinator: NjordStatusCoordinator, entry: ConfigEntry, sw_version: str | None = None) -> None:
        super().__init__(coordinator)
        self._attr_unique_id = f"{entry.entry_id}_monthly_usage"
        self._attr_name = "Monthly Usage"
        self._attr_device_info = server_device_info(entry, sw_version)

    @property
    def available(self) -> bool:
        return (
            self.coordinator.data is not None
            and self.coordinator.data.budget is not None
            and self.coordinator.data.budget.monthly_limit > 0
        )

    @property
    def native_value(self) -> float | None:
        status = self.coordinator.data
        if status is None or status.budget is None or status.budget.monthly_limit == 0:
            return None
        return round(status.budget.monthly_used / status.budget.monthly_limit * 100, 1)

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        status = self.coordinator.data
        if status is None or status.budget is None:
            return None
        return {
            "limit": status.budget.monthly_limit,
            "used": status.budget.monthly_used,
        }


class NjordDailyUsageSensor(CoordinatorEntity[NjordStatusCoordinator], SensorEntity):
    """Diagnostic sensor for daily API usage percentage."""

    _attr_has_entity_name = True
    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_native_unit_of_measurement = "%"
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:calendar-today"
    _attr_state_class = SensorStateClass.MEASUREMENT

    def __init__(self, coordinator: NjordStatusCoordinator, entry: ConfigEntry, sw_version: str | None = None) -> None:
        super().__init__(coordinator)
        self._attr_unique_id = f"{entry.entry_id}_daily_usage"
        self._attr_name = "Daily Usage"
        self._attr_device_info = server_device_info(entry, sw_version)

    @property
    def available(self) -> bool:
        return (
            self.coordinator.data is not None
            and self.coordinator.data.budget is not None
            and self.coordinator.data.budget.daily_limit > 0
        )

    @property
    def native_value(self) -> float | None:
        status = self.coordinator.data
        if status is None or status.budget is None or status.budget.daily_limit == 0:
            return None
        return round(status.budget.daily_used / status.budget.daily_limit * 100, 1)

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        status = self.coordinator.data
        if status is None or status.budget is None:
            return None
        return {
            "limit": status.budget.daily_limit,
            "used": status.budget.daily_used,
        }


class NjordVersionSensor(CoordinatorEntity[NjordStatusCoordinator], SensorEntity):
    """Diagnostic sensor for njord server version."""

    _attr_has_entity_name = True
    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_icon = "mdi:tag"

    def __init__(self, coordinator: NjordStatusCoordinator, entry: ConfigEntry, sw_version: str | None = None) -> None:
        super().__init__(coordinator)
        self._attr_unique_id = f"{entry.entry_id}_version"
        self._attr_name = "Version"
        self._attr_device_info = server_device_info(entry, sw_version)

    @property
    def available(self) -> bool:
        return self.coordinator.data is not None

    @property
    def native_value(self) -> str | None:
        status = self.coordinator.data
        if status is None:
            return None
        return status.version.split("+")[0]


class NjordUptimeSensor(CoordinatorEntity[NjordStatusCoordinator], SensorEntity):
    """Diagnostic sensor for njord server uptime."""

    _attr_has_entity_name = True
    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_device_class = SensorDeviceClass.DURATION
    _attr_native_unit_of_measurement = UnitOfTime.HOURS
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:server"
    _attr_translation_key = "uptime"

    def __init__(self, coordinator: NjordStatusCoordinator, entry: ConfigEntry, sw_version: str | None = None) -> None:
        super().__init__(coordinator)
        self._attr_unique_id = f"{entry.entry_id}_uptime"
        self._attr_name = "Uptime"
        self._attr_device_info = server_device_info(entry, sw_version)

    @property
    def available(self) -> bool:
        return self.coordinator.data is not None

    @property
    def native_value(self) -> float | None:
        status = self.coordinator.data
        if status is None:
            return None
        return round(status.uptime_seconds / 3600, 1)


class _NjordDerivedHorizonSensor(_NjordEnrichmentSensor):
    """Base class for sensors reading from DerivedData.by_horizon."""

    def _current_derived_horizon(self) -> HorizonDerivedData | None:
        enrichment = self._enrichment()
        if enrichment is None or enrichment.derived is None:
            return None
        offset = current_horizon_offset(enrichment.derived_updated_at)
        return get_horizon_entry(enrichment.derived.by_horizon, offset)

    @property
    def available(self) -> bool:
        enrichment = self._enrichment()
        return enrichment is not None and enrichment.derived is not None


class NjordBeaufortSensor(_NjordDerivedHorizonSensor):
    """Sensor for Beaufort wind scale (0-12)."""

    _attr_suggested_display_precision = 0
    _attr_icon = "mdi:windsock"
    _attr_translation_key = "beaufort"

    def __init__(self, coordinator, entry, location, sw_version=None):
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_beaufort".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Beaufort"

    @property
    def native_value(self) -> int | None:
        h = self._current_derived_horizon()
        return h.beaufort if h is not None else None


class NjordWindChillSensor(_NjordDerivedHorizonSensor):
    """Sensor for wind chill temperature."""

    _attr_device_class = SensorDeviceClass.TEMPERATURE
    _attr_native_unit_of_measurement = UnitOfTemperature.CELSIUS
    _attr_suggested_display_precision = 1
    _attr_icon = "mdi:snowflake-thermometer"
    _attr_translation_key = "wind_chill"

    def __init__(self, coordinator, entry, location, sw_version=None):
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_wind_chill".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Wind Chill"

    @property
    def native_value(self) -> float | None:
        h = self._current_derived_horizon()
        return h.wind_chill if h is not None else None


class NjordDewpointComfortSensor(_NjordDerivedHorizonSensor):
    """Sensor for dewpoint comfort category."""

    _attr_icon = "mdi:water-thermometer"
    _attr_translation_key = "dewpoint_comfort"

    def __init__(self, coordinator, entry, location, sw_version=None):
        super().__init__(coordinator, entry, location, sw_version)
        slug = f"{location}_dewpoint_comfort".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = "Dewpoint Comfort"

    @property
    def native_value(self) -> str | None:
        h = self._current_derived_horizon()
        return h.dewpoint_comfort if h is not None else None


class NjordTargetSensor(CoordinatorEntity[NjordStatusCoordinator], SensorEntity):
    """Diagnostic sensor showing poll state for a location/model target."""

    _attr_has_entity_name = True
    _attr_entity_category = EntityCategory.DIAGNOSTIC
    _attr_entity_registry_enabled_default = False
    _attr_device_class = SensorDeviceClass.TIMESTAMP
    _attr_icon = "mdi:radar"

    def __init__(
        self,
        coordinator: NjordStatusCoordinator,
        entry: ConfigEntry,
        location: str,
        model: str,
        sw_version: str | None = None,
    ) -> None:
        super().__init__(coordinator)
        self._target_location = location
        self._target_model = model
        slug = f"{location}_{model}_target".replace("-", "_").replace(" ", "_").lower()
        self._attr_unique_id = f"{entry.entry_id}_{slug}"
        self._attr_name = f"{model} {location}"
        self._attr_translation_key = "target_poll"
        self._attr_device_info = server_device_info(entry, sw_version)

    def _find_target(self):
        if self.coordinator.data is None:
            return None
        for t in self.coordinator.data.targets:
            if t.location == self._target_location and t.model == self._target_model:
                return t
        return None

    @property
    def available(self) -> bool:
        return self._find_target() is not None

    @property
    def native_value(self):
        t = self._find_target()
        if t is None or t.next_poll is None:
            return None
        return t.next_poll

    @property
    def extra_state_attributes(self) -> dict[str, object] | None:
        t = self._find_target()
        if t is None:
            return None
        attrs: dict[str, object] = {
            "phase": t.phase,
            "model": t.model,
            "miss_count": t.miss_count,
        }
        if t.last_change is not None:
            attrs["last_change"] = t.last_change.isoformat()
        if t.cycle_seconds is not None:
            attrs["cycle_seconds"] = t.cycle_seconds
        return attrs
