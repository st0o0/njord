"""Data update coordinator for njord."""

from __future__ import annotations

import asyncio
import logging
from collections.abc import Callable
from dataclasses import dataclass, field, replace
from datetime import UTC, datetime, timedelta

from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.issue_registry import IssueSeverity, async_create_issue, async_delete_issue
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator, UpdateFailed

from .const import DOMAIN
from .grpc_client import NjordClient
from .models import EnrichmentData, ForecastData, ModelInfoData, NjordLocation, ServerStatusData

_LOGGER = logging.getLogger(__name__)

_STREAM_DISCONNECT_GRACE = 60.0

_ENRICHMENT_MERGE_FIELDS = (
    "alerts",
    "indices",
    "trends",
    "derived",
    "history",
    "consensus",
    "consensus_updated_at",
    "derived_updated_at",
)
_ENRICHMENT_DEFAULTS: dict[str, object] = {
    "alerts": [],
    "indices": None,
    "trends": None,
    "derived": None,
    "history": None,
    "consensus": None,
    "consensus_updated_at": None,
    "derived_updated_at": None,
}


def merge_enrichment(existing: EnrichmentData | None, event: EnrichmentData) -> EnrichmentData:
    """Merge a partial enrichment event into existing data.

    Only fields that differ from defaults in the event are applied.
    """
    if existing is None:
        existing = EnrichmentData(location=event.location)

    updates: dict[str, object] = {}
    for field_name in _ENRICHMENT_MERGE_FIELDS:
        event_value = getattr(event, field_name)
        default_value = _ENRICHMENT_DEFAULTS[field_name]
        if event_value != default_value:
            updates[field_name] = event_value

    if not updates:
        return existing

    return replace(existing, **updates)


@dataclass
class NjordCoordinatorData:
    forecasts: dict[tuple[str, str], ForecastData] = field(default_factory=dict)
    enrichments: dict[str, EnrichmentData] = field(default_factory=dict)
    model_info: dict[str, ModelInfoData] = field(default_factory=dict)
    active_enrichments: set[str] | None = None


EntityFactory = Callable[[NjordLocation], list]


class NjordDataCoordinator(DataUpdateCoordinator[NjordCoordinatorData]):
    """Stream-driven coordinator for njord forecast, enrichment, and config data."""

    def __init__(self, hass: HomeAssistant, client: NjordClient) -> None:
        super().__init__(
            hass,
            _LOGGER,
            name="njord",
            update_interval=None,
        )
        self.client = client
        self._known_locations: set[str] = set()
        self._stream_tasks: list[asyncio.Task] = []
        self._entity_factories: dict[str, tuple[AddEntitiesCallback, EntityFactory]] = {}
        self.stream_states: dict[str, bool] = {
            "forecast": False,
            "enrichment": False,
            "config": False,
        }
        self._stream_disconnect_times: dict[str, float] = {}
        self._stream_issue_created: dict[str, bool] = {}

    async def _async_update_data(self) -> NjordCoordinatorData:
        """Unary first-refresh: fetch all data via single calls."""
        try:
            catalog = await self.client.get_catalog()
        except Exception as err:
            raise UpdateFailed(f"Failed to get njord catalog: {err}") from err

        result = NjordCoordinatorData()
        result.model_info.update(catalog.model_info)

        for location in catalog.locations:
            self._known_locations.add(location.name)

            for model in location.models:
                try:
                    forecast = await self.client.get_forecast(location.name, model)
                    result.forecasts[(location.name, model)] = forecast
                except Exception as err:
                    _LOGGER.warning(
                        "Failed to get forecast for %s/%s: %s",
                        location.name,
                        model,
                        err,
                    )
                    result.forecasts[(location.name, model)] = ForecastData(
                        location=location.name, model=model, updated_at=datetime.min.replace(tzinfo=UTC)
                    )

            try:
                enrichment = await self.client.get_enrichments(location.name)
                result.enrichments[location.name] = enrichment
            except Exception as err:
                _LOGGER.warning(
                    "Failed to get enrichments for %s: %s",
                    location.name,
                    err,
                )

        return result

    def register_entity_factory(
        self,
        platform: str,
        add_entities: AddEntitiesCallback,
        factory: EntityFactory,
    ) -> None:
        self._entity_factories[platform] = (add_entities, factory)

    def start_streams(self) -> None:
        self._stream_tasks = [
            self.hass.async_create_background_task(self._run_forecast_stream(), "njord_forecast_stream"),
            self.hass.async_create_background_task(self._run_enrichment_stream(), "njord_enrichment_stream"),
            self.hass.async_create_background_task(self._run_config_stream(), "njord_config_stream"),
        ]

    async def stop_streams(self) -> None:
        for task in self._stream_tasks:
            task.cancel()
        await asyncio.gather(*self._stream_tasks, return_exceptions=True)
        self._stream_tasks.clear()

    def _on_stream_connect(self, name: str) -> None:
        self.stream_states[name] = True
        self._stream_disconnect_times.pop(name, None)
        if self._stream_issue_created.get(name):
            async_delete_issue(self.hass, DOMAIN, f"stream_{name}_disconnected")
            self._stream_issue_created[name] = False
        self.async_set_updated_data(self.data)

    def _on_stream_disconnect(self, name: str) -> None:
        self.stream_states[name] = False
        if name not in self._stream_disconnect_times:
            self._stream_disconnect_times[name] = self.hass.loop.time()
        elif not self._stream_issue_created.get(name):
            elapsed = self.hass.loop.time() - self._stream_disconnect_times[name]
            if elapsed >= _STREAM_DISCONNECT_GRACE:
                async_create_issue(
                    self.hass,
                    DOMAIN,
                    f"stream_{name}_disconnected",
                    is_fixable=False,
                    severity=IssueSeverity.WARNING,
                    translation_key=f"stream_{name}_disconnected",
                )
                self._stream_issue_created[name] = True
        self.async_set_updated_data(self.data)

    async def _run_forecast_stream(self) -> None:
        try:
            async for update in self.client.stream_forecasts(
                location=None,
                on_reconnect=lambda: self._on_stream_connect("forecast"),
                on_disconnect=lambda: self._on_stream_disconnect("forecast"),
            ):
                self.data.forecasts[(update.location, update.model)] = update
                self.async_set_updated_data(self.data)
        except asyncio.CancelledError:
            return
        except Exception:
            _LOGGER.exception("Forecast stream task failed")
            self._on_stream_disconnect("forecast")

    async def _run_enrichment_stream(self) -> None:
        try:
            async for event in self.client.stream_enrichments(
                location=None,
                on_reconnect=lambda: self._on_stream_connect("enrichment"),
                on_disconnect=lambda: self._on_stream_disconnect("enrichment"),
            ):
                existing = self.data.enrichments.get(event.location)
                self.data.enrichments[event.location] = merge_enrichment(existing, event)
                self.async_set_updated_data(self.data)
        except asyncio.CancelledError:
            return
        except Exception:
            _LOGGER.exception("Enrichment stream task failed")
            self._on_stream_disconnect("enrichment")

    async def _run_config_stream(self) -> None:
        try:
            async for config in self.client.stream_config(
                on_reconnect=lambda: self._on_stream_connect("config"),
                on_disconnect=lambda: self._on_stream_disconnect("config"),
            ):
                new_locations = [loc for loc in config.locations if loc.name not in self._known_locations]
                for location in new_locations:
                    await self._create_entities_for_location(location)
        except asyncio.CancelledError:
            return
        except Exception:
            _LOGGER.exception("Config stream task failed")
            self._on_stream_disconnect("config")

    async def _create_entities_for_location(self, location: NjordLocation) -> None:
        self._known_locations.add(location.name)

        for model in location.models:
            try:
                forecast = await self.client.get_forecast(location.name, model)
                self.data.forecasts[(location.name, model)] = forecast
            except Exception as err:
                _LOGGER.warning(
                    "Failed to get initial forecast for new location %s/%s: %s",
                    location.name,
                    model,
                    err,
                )

        try:
            enrichment = await self.client.get_enrichments(location.name)
            self.data.enrichments[location.name] = enrichment
        except Exception as err:
            _LOGGER.warning(
                "Failed to get initial enrichments for new location %s: %s",
                location.name,
                err,
            )

        for platform, (add_entities, factory) in self._entity_factories.items():
            try:
                entities = factory(location)
                if entities:
                    add_entities(entities)
            except Exception as err:
                _LOGGER.error(
                    "Failed to create %s entities for %s: %s",
                    platform,
                    location.name,
                    err,
                )

        self.async_set_updated_data(self.data)


class NjordStatusCoordinator(DataUpdateCoordinator[ServerStatusData]):
    """Polling coordinator for njord server status (budget, uptime)."""

    def __init__(self, hass: HomeAssistant, client: NjordClient, poll_interval: int = 30) -> None:
        super().__init__(
            hass,
            _LOGGER,
            name="njord_status",
            update_interval=timedelta(seconds=poll_interval),
        )
        self.client = client

    async def _async_update_data(self) -> ServerStatusData:
        try:
            status = await self.client.get_status()
        except Exception as err:
            raise UpdateFailed(f"Failed to get njord status: {err}") from err

        try:
            targets = await self.client.get_targets()
        except Exception:
            targets = []

        return replace(status, targets=targets)
